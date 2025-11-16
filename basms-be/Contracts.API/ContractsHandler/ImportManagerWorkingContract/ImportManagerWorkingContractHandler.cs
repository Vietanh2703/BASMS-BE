namespace Contracts.API.ContractsHandler.ImportManagerWorkingContract;

/// <summary>
///     Command để import manager working contract từ Word document đã ký
///     Chỉ cần DocumentId - sẽ tự động extract thông tin và tạo Manager user
/// </summary>
public record ImportManagerWorkingContractCommand(
    Guid DocumentId
) : ICommand<ImportManagerWorkingContractResult>;

/// <summary>
///     Result của việc import manager working contract
/// </summary>
public record ImportManagerWorkingContractResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public Guid? ContractId { get; init; }
    public Guid? UserId { get; init; } // User ID của Manager đã tạo
    public string? ContractNumber { get; init; }
    public string? ContractTitle { get; init; }
    public string? EmployeeName { get; init; }
    public string? EmployeeEmail { get; init; }
}

internal class ImportManagerWorkingContractHandler(
    IDbConnectionFactory connectionFactory,
    IS3Service s3Service,
    IWordContractService wordService,
    IDigitalSignatureService signatureService,
    IRequestClient<CreateUserRequest> createUserClient,
    ILogger<ImportManagerWorkingContractHandler> logger)
    : ICommandHandler<ImportManagerWorkingContractCommand, ImportManagerWorkingContractResult>
{
    public async Task<ImportManagerWorkingContractResult> Handle(
        ImportManagerWorkingContractCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation(
                "Importing manager working contract from DocumentId: {DocumentId}",
                request.DocumentId);

            // ================================================================
            // KIỂM TRA DOCUMENT TỒN TẠI
            // ================================================================

            using var connection = await connectionFactory.CreateConnectionAsync();

            var document = await connection.QueryFirstOrDefaultAsync<ContractDocument>(
                "SELECT * FROM contract_documents WHERE Id = @Id AND IsDeleted = 0",
                new { Id = request.DocumentId });

            if (document == null)
                return new ImportManagerWorkingContractResult
                {
                    Success = false,
                    ErrorMessage = $"Document with ID {request.DocumentId} not found"
                };

            // ================================================================
            // KIỂM TRA CHỮ KÝ (Ít nhất 1 chữ ký: Bên B - Người lao động)
            // ================================================================

            var (downloadSuccess, fileStream, downloadError) = await s3Service.DownloadFileAsync(
                document.FileUrl,
                cancellationToken);

            if (!downloadSuccess || fileStream == null)
                return new ImportManagerWorkingContractResult
                {
                    Success = false,
                    ErrorMessage = downloadError ?? "Failed to download file from S3"
                };

            // Kiểm tra số chữ ký
            var (countSuccess, signatureCount, countError) = await signatureService.CountSignaturesAsync(
                fileStream,
                cancellationToken);

            if (!countSuccess)
                return new ImportManagerWorkingContractResult
                {
                    Success = false,
                    ErrorMessage = countError ?? "Failed to count signatures"
                };

            if (signatureCount < 1)
                return new ImportManagerWorkingContractResult
                {
                    Success = false,
                    ErrorMessage =
                        $"Document must have at least 1 signature (Manager - Party B). Found: {signatureCount}"
                };

            logger.LogInformation("✓ Document has {Count} signature(s) - proceeding with import", signatureCount);

            // ================================================================
            // EXTRACT TEXT TỪ WORD DOCUMENT
            // ================================================================

            // Download lại file để extract text
            var (downloadSuccess2, fileStream2, downloadError2) = await s3Service.DownloadFileAsync(
                document.FileUrl,
                cancellationToken);

            if (!downloadSuccess2 || fileStream2 == null)
                return new ImportManagerWorkingContractResult
                {
                    Success = false,
                    ErrorMessage = "Failed to download file for text extraction"
                };

            var (extractSuccess, text, extractError) = await wordService.ExtractTextFromWordAsync(
                fileStream2,
                cancellationToken);

            if (!extractSuccess || string.IsNullOrEmpty(text))
                return new ImportManagerWorkingContractResult
                {
                    Success = false,
                    ErrorMessage = extractError ?? "Failed to extract text from Word document"
                };

            logger.LogInformation("✓ Extracted {Length} characters from document", text.Length);

            // ================================================================
            // PARSE THÔNG TIN HỢP ĐỒNG VÀ NGƯỜI LAO ĐỘNG
            // ================================================================

            var contractData = ParseContractData(text);
            if (contractData == null)
                return new ImportManagerWorkingContractResult
                {
                    Success = false,
                    ErrorMessage = "Failed to parse contract data from document"
                };

            var employeeData = ParseEmployeeData(text);
            if (employeeData == null)
                return new ImportManagerWorkingContractResult
                {
                    Success = false,
                    ErrorMessage = "Failed to parse employee data from document"
                };

            logger.LogInformation(
                "✓ Parsed contract data: Number={Number}, StartDate={Start}, EndDate={End}",
                contractData.ContractNumber,
                contractData.StartDate,
                contractData.EndDate);

            logger.LogInformation(
                "✓ Parsed employee data: Name={Name}, Email={Email}, IdentityNumber={Identity}",
                employeeData.FullName,
                employeeData.Email,
                employeeData.IdentityNumber);

            // ================================================================
            // TẠO MANAGER MỚI
            // ================================================================

            if (string.IsNullOrEmpty(employeeData.Email))
                return new ImportManagerWorkingContractResult
                {
                    Success = false,
                    ErrorMessage = "Employee email is required but not found in document"
                };

            var randomPassword = GenerateRandomPassword();
            logger.LogInformation("Generated random password for manager user (will be sent via email)");

            var createUserRequest = new CreateUserRequest
            {
                FullName = employeeData.FullName ?? "Unknown",
                Email = employeeData.Email,
                Password = randomPassword,
                IdentityNumber = employeeData.IdentityNumber,
                IdentityIssueDate = employeeData.IdentityIssueDate,
                IdentityIssuePlace = employeeData.IdentityIssuePlace,
                Phone = ConvertPhoneToInternationalFormat(employeeData.Phone),
                Address = employeeData.Address,
                BirthDay = employeeData.BirthDay,
                BirthMonth = employeeData.BirthMonth,
                BirthYear = employeeData.BirthYear,
                Gender = "male", // Default, có thể cải thiện sau
                RoleName = "manager", 
                AuthProvider = "email",
                Status = "active",
                EmailVerified = false,
                PhoneVerified = false,
                LoginCount = 0
            };

            var createUserResponse = await createUserClient.GetResponse<CreateUserResponse>(
                createUserRequest,
                cancellationToken);

            if (!createUserResponse.Message.Success)
                return new ImportManagerWorkingContractResult
                {
                    Success = false,
                    ErrorMessage = $"Failed to create manager user: {createUserResponse.Message.ErrorMessage}"
                };

            var userId = createUserResponse.Message.UserId;
            logger.LogInformation(
                "✓ Created user (Manager): UserId={UserId}, Email={Email}. " +
                "UserCreatedConsumer in Shifts.API will automatically create Manager record.",
                userId,
                employeeData.Email);

            // ================================================================
            // TẠO CONTRACT TRONG DATABASE (không cần CustomerId)
            // ================================================================
            // Manager working contract là hợp đồng lao động với Manager
            // Không sử dụng CustomerId vì đây không phải hợp đồng dịch vụ với khách hàng

            var contract = new Contract
            {
                Id = Guid.NewGuid(),
                CustomerId = null, // Manager working contract không có CustomerId
                DocumentId = request.DocumentId, // Link đến document
                ContractNumber = contractData.ContractNumber,
                ContractTitle = contractData.ContractTitle,
                ContractType = "manager_working_contract", // Loại hợp đồng lao động Manager
                ServiceScope = "management",
                CoverageModel = "fixed_schedule",
                StartDate = contractData.StartDate,
                EndDate = contractData.EndDate ?? contractData.StartDate.AddMonths(36), // Max 36 tháng
                DurationMonths = contractData.DurationMonths,
                Status = "signed", // Đã ký
                SignedDate = DateTime.UtcNow,
                ContractFileUrl = document.FileUrl,
                AutoGenerateShifts = false, // Manager không cần auto-generate shifts
                GenerateShiftsAdvanceDays = 0,
                WorkOnPublicHolidays = true,
                WorkOnCustomerClosedDays = true,
                FollowsCustomerCalendar = false,
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow
            };

            await connection.InsertAsync(contract);

            logger.LogInformation(
                "✓ Created manager working contract: ContractId={ContractId}, Number={Number}, UserId={UserId}",
                contract.Id,
                contract.ContractNumber,
                userId);

            return new ImportManagerWorkingContractResult
            {
                Success = true,
                ContractId = contract.Id,
                UserId = userId,
                ContractNumber = contract.ContractNumber,
                ContractTitle = contract.ContractTitle,
                EmployeeName = employeeData.FullName,
                EmployeeEmail = employeeData.Email
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to import manager working contract");
            return new ImportManagerWorkingContractResult
            {
                Success = false,
                ErrorMessage = $"Import failed: {ex.Message}"
            };
        }
    }

    private string? ConvertPhoneToInternationalFormat(string? phone)
    {
        if (string.IsNullOrEmpty(phone))
            return null;

        // Remove spaces và non-digit characters
        var cleanPhone = Regex.Replace(phone, @"[^\d]", "");

        // Nếu bắt đầu bằng 0, thay bằng +84
        if (cleanPhone.StartsWith("0")) return "+84" + cleanPhone.Substring(1);

        // Nếu đã có +84, giữ nguyên
        if (cleanPhone.StartsWith("84")) return "+" + cleanPhone;

        // Trường hợp khác, giữ nguyên
        return cleanPhone;
    }

    /// <summary>
    ///     Parse thông tin từ text của Word document
    /// </summary>
    private ContractDataParsed? ParseContractData(string text)
    {
        try
        {
            // Extract Contract Number: Số: ………./HĐLĐ
            var contractNumberMatch = Regex.Match(text, @"Số:\s*([^\n]+)/HĐLĐ");
            var contractNumber = contractNumberMatch.Success
                ? contractNumberMatch.Groups[1].Value.Trim()
                : Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();

            // Extract Sign Date: Hôm nay, ngày ……… tháng …… năm ……
            var signDateMatch = Regex.Match(text, @"Hôm nay,\s*ngày\s+(\d+)\s+tháng\s+(\d+)\s+năm\s+(\d+)");
            var signDate = DateTime.UtcNow;
            if (signDateMatch.Success)
            {
                var day = int.Parse(signDateMatch.Groups[1].Value);
                var month = int.Parse(signDateMatch.Groups[2].Value);
                var year = int.Parse(signDateMatch.Groups[3].Value);
                signDate = new DateTime(year, month, day);
            }

            // Extract Employee Name từ "Bên B" section
            var employeeNameMatch = Regex.Match(text, @"–\s*Họ và tên:\s*([^\n]+?)(?:\s*–|\s*\n)");
            var employeeName = employeeNameMatch.Success
                ? employeeNameMatch.Groups[1].Value.Trim()
                : "Unknown Manager";

            // Create Title: "Hợp đồng lao động - [Tên NLĐ] - [dd/MM/yyyy]"
            // Limit to 255 characters to fit database column
            var contractTitle = $"Hợp đồng lao động - {employeeName} - {signDate:dd/MM/yyyy}";
            if (contractTitle.Length > 255) contractTitle = contractTitle.Substring(0, 252) + "...";

            // Extract Start Date and End Date
            // Pattern: thời hạn từ ngày …./…./…… đến ngày …./…./……
            var dateRangeMatch = Regex.Match(
                text,
                @"thời hạn từ ngày\s+(\d{1,2})/(\d{1,2})/(\d{4})\s+đến ngày\s+(\d{1,2})/(\d{1,2})/(\d{4})");

            var startDate = signDate;
            DateTime? endDate = null;
            var durationMonths = 12;

            if (dateRangeMatch.Success)
            {
                startDate = new DateTime(
                    int.Parse(dateRangeMatch.Groups[3].Value),
                    int.Parse(dateRangeMatch.Groups[2].Value),
                    int.Parse(dateRangeMatch.Groups[1].Value));

                endDate = new DateTime(
                    int.Parse(dateRangeMatch.Groups[6].Value),
                    int.Parse(dateRangeMatch.Groups[5].Value),
                    int.Parse(dateRangeMatch.Groups[4].Value));

                durationMonths = (endDate.Value.Year - startDate.Year) * 12 +
                    endDate.Value.Month - startDate.Month;

                // Đảm bảo không vượt quá 36 tháng
                if (durationMonths > 36)
                {
                    durationMonths = 36;
                    endDate = startDate.AddMonths(36);
                }
            }

            return new ContractDataParsed
            {
                ContractNumber = contractNumber,
                ContractTitle = contractTitle,
                StartDate = startDate,
                EndDate = endDate,
                DurationMonths = durationMonths
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to parse contract data");
            return null;
        }
    }

    /// <summary>
    ///     Parse thông tin người lao động (Bên B) từ text
    /// </summary>
    private EmployeeDataParsed? ParseEmployeeData(string text)
    {
        try
        {
            // Tìm phần "Bên B" trong text
            var benBMatch = Regex.Match(text, @"BÊN B\s*\(Người lao động\):(.*?)(?=ĐIỀU\s+\d+|Điều\s+\d+|$)",
                RegexOptions.Singleline | RegexOptions.IgnoreCase);
            if (!benBMatch.Success)
                benBMatch = Regex.Match(text, @"Bên B[^\n]*?:(.*?)(?=ĐIỀU\s+\d+|Điều\s+\d+|$)",
                    RegexOptions.Singleline | RegexOptions.IgnoreCase);

            var benBText = benBMatch.Success ? benBMatch.Groups[1].Value : text;

            // ================================================================
            // ✅ IMPROVED: Email extraction với nhiều pattern và validation
            // ================================================================
            string? email = null;

            // Log Bên B text để debug
            logger.LogInformation("Extracting email from Bên B text (length: {Length})", benBText.Length);
            var emailSection = ExtractEmailSection(benBText);
            if (!string.IsNullOrEmpty(emailSection))
            {
                logger.LogInformation("Email section found: '{EmailSection}'", emailSection);
            }

            // Pattern 1: "– Email: xxx@xxx.xxx" (có dấu gạch ngang, chặt chẽ)
            var emailMatch1 = Regex.Match(benBText, @"–\s*Email:\s*([a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,})",
                RegexOptions.IgnoreCase);
            if (emailMatch1.Success)
            {
                email = emailMatch1.Groups[1].Value.Trim().ToLower();
                logger.LogInformation("✓ Extracted email (pattern 1 - với dấu gạch ngang): {Email}", email);
            }

            // Pattern 2: "Email: xxx@xxx.xxx" (không có dấu gạch ngang)
            if (string.IsNullOrEmpty(email))
            {
                var emailMatch2 = Regex.Match(benBText, @"Email:\s*([a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,})",
                    RegexOptions.IgnoreCase);
                if (emailMatch2.Success)
                {
                    email = emailMatch2.Groups[1].Value.Trim().ToLower();
                    logger.LogInformation("✓ Extracted email (pattern 2 - không dấu gạch ngang): {Email}", email);
                }
            }

            // Pattern 3: Tìm email bất kỳ trong section Email
            if (string.IsNullOrEmpty(email))
            {
                var allEmails = Regex.Matches(benBText, @"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}",
                    RegexOptions.IgnoreCase);

                if (allEmails.Count > 0)
                {
                    // Tìm email gần nhất với "Email:" label
                    var emailLabelIndex = benBText.IndexOf("Email:", StringComparison.OrdinalIgnoreCase);

                    if (emailLabelIndex >= 0)
                    {
                        // Lấy email đầu tiên SAU vị trí "Email:"
                        foreach (Match match in allEmails)
                        {
                            if (match.Index > emailLabelIndex)
                            {
                                email = match.Value.Trim().ToLower();
                                logger.LogInformation("✓ Extracted email (pattern 3 - tìm sau Email:): {Email}", email);
                                break;
                            }
                        }
                    }

                    // Nếu vẫn chưa có, lấy email đầu tiên tìm được
                    if (string.IsNullOrEmpty(email))
                    {
                        email = allEmails[0].Value.Trim().ToLower();
                        logger.LogInformation("✓ Extracted email (pattern 3 - email đầu tiên): {Email}", email);
                    }
                }
            }

            // ✅ Validate và cleanup email
            if (!string.IsNullOrEmpty(email))
            {
                email = CleanupEmail(email);

                if (!IsValidEmail(email))
                {
                    logger.LogWarning("⚠ Invalid email format after extraction: {Email}", email);
                    email = null;
                }
                else
                {
                    logger.LogInformation("✓ Final validated email: {Email}", email);
                }
            }

            if (string.IsNullOrEmpty(email))
                logger.LogWarning("⚠ Failed to extract valid email from Bên B text");

            // Extract các trường khác (giữ nguyên logic cũ)
            var nameMatch = Regex.Match(benBText, @"–\s*Họ và tên:\s*([^\n]+?)(?:\s*–|\s*\n)");
            var fullName = nameMatch.Success ? nameMatch.Groups[1].Value.Trim() : null;

            var birthMatch = Regex.Match(benBText,
                @"–\s*Sinh ngày:\s*(\d{1,2})/(\d{1,2})/(\d{4})\s+tại:\s*([^\n]+?)(?:\s*–|\s*\n)");
            int? birthDay = null, birthMonth = null, birthYear = null;
            string? birthPlace = null;
            if (birthMatch.Success)
            {
                birthDay = int.Parse(birthMatch.Groups[1].Value);
                birthMonth = int.Parse(birthMatch.Groups[2].Value);
                birthYear = int.Parse(birthMatch.Groups[3].Value);
                birthPlace = birthMatch.Groups[4].Value.Trim();
            }

            var cccdMatch = Regex.Match(benBText,
                @"–\s*Số CCCD:\s*(\d+)\s+ngày cấp:\s*(\d{1,2})/(\d{1,2})/(\d{4})\s+nơi cấp:\s*([^\n]+?)(?:\s*–|\s*\n)");
            string? identityNumber = null;
            DateTime? identityIssueDate = null;
            string? identityIssuePlace = null;
            if (cccdMatch.Success)
            {
                identityNumber = cccdMatch.Groups[1].Value.Trim();
                var issueDay = int.Parse(cccdMatch.Groups[2].Value);
                var issueMonth = int.Parse(cccdMatch.Groups[3].Value);
                var issueYear = int.Parse(cccdMatch.Groups[4].Value);
                identityIssueDate = new DateTime(issueYear, issueMonth, issueDay);
                identityIssuePlace = cccdMatch.Groups[5].Value.Trim();
            }

            var addressMatch = Regex.Match(benBText, @"–\s*Hộ khẩu thường trú:\s*([^\n]+?)(?:\s*–|\s*\n)");
            var address = addressMatch.Success ? addressMatch.Groups[1].Value.Trim() : null;

            var phoneMatch = Regex.Match(benBText, @"–\s*Điện thoại:\s*([0-9]+)");
            var phone = phoneMatch.Success ? phoneMatch.Groups[1].Value.Trim() : null;

            return new EmployeeDataParsed
            {
                FullName = fullName,
                BirthDay = birthDay,
                BirthMonth = birthMonth,
                BirthYear = birthYear,
                BirthPlace = birthPlace,
                IdentityNumber = identityNumber,
                IdentityIssueDate = identityIssueDate,
                IdentityIssuePlace = identityIssuePlace,
                Address = address,
                Phone = phone,
                Email = email
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to parse employee data");
            return null;
        }
    }


    /// <summary>
    ///     Generate random password (9 characters)
    /// </summary>
    private string GenerateRandomPassword()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, 9)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }

    /// <summary>
    ///     Extract email section từ text để debug
    /// </summary>
    private string? ExtractEmailSection(string text)
    {
        // Tìm section từ "Email:" đến ký tự xuống dòng hoặc dấu gạch ngang
        var match = Regex.Match(text, @"Email:([^\n–]+)", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    /// <summary>
    ///     Cleanup email: loại bỏ khoảng trắng, ký tự đặc biệt thừa
    /// </summary>
    private string CleanupEmail(string email)
    {
        if (string.IsNullOrEmpty(email))
            return email;

        // Remove all whitespace
        email = Regex.Replace(email, @"\s+", "");

        // Remove any trailing/leading special characters (ngoại trừ @ và .)
        email = email.Trim('.', ',', ';', ':', '!', '?', '-', '_');

        // Remove any control characters
        email = Regex.Replace(email, @"[\x00-\x1F\x7F]", "");

        return email.ToLower();
    }

    /// <summary>
    ///     Validate email format theo RFC 5322
    /// </summary>
    private bool IsValidEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        try
        {
            // RFC 5322 compliant regex
            var pattern = @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$";
            var isMatch = Regex.IsMatch(email, pattern, RegexOptions.IgnoreCase);

            // Additional validation: must have at least one char before @
            // and must have valid domain (at least one dot after @)
            if (isMatch)
            {
                var parts = email.Split('@');
                if (parts.Length == 2 && parts[0].Length > 0 && parts[1].Contains('.'))
                {
                    return true;
                }
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    ///     Helper class để hold parsed contract data
    /// </summary>
    private class ContractDataParsed
    {
        public string ContractNumber { get; set; } = string.Empty;
        public string ContractTitle { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int DurationMonths { get; set; }
    }

    /// <summary>
    ///     Helper class để hold parsed employee data
    /// </summary>
    private class EmployeeDataParsed
    {
        public string? FullName { get; set; }
        public int? BirthDay { get; set; }
        public int? BirthMonth { get; set; }
        public int? BirthYear { get; set; }
        public string? BirthPlace { get; set; }
        public string? IdentityNumber { get; set; }
        public DateTime? IdentityIssueDate { get; set; }
        public string? IdentityIssuePlace { get; set; }
        public string? Address { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
    }
}
