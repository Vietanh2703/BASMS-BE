using BuildingBlocks.Messaging.Events;

namespace Contracts.API.ContractsHandler.ImportContractFromDocument;

// ================================================================
// COMMAND & RESULT
// ================================================================

/// <summary>
/// Command để import contract từ file Word/PDF
/// Upload document file, parse information, and save to database
/// </summary>
public record ImportContractFromDocumentCommand(
    Stream FileStream,
    string FileName,
    Guid CreatedBy
) : ICommand<ImportContractFromDocumentResult>;

/// <summary>
/// Kết quả import
/// </summary>
public record ImportContractFromDocumentResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }

    // IDs đã tạo
    public Guid? ContractId { get; init; }
    public Guid? CustomerId { get; init; }
    public List<Guid> LocationIds { get; init; } = new();
    public List<Guid> ShiftScheduleIds { get; init; } = new();

    // Thông tin đã parse
    public string? ContractNumber { get; init; }
    public string? CustomerName { get; init; }
    public int LocationsCreated { get; init; }
    public int SchedulesCreated { get; init; }

    // Text gốc và warnings
    public string RawText { get; init; } = string.Empty;
    public List<string> Warnings { get; init; } = new();
    public int ConfidenceScore { get; init; }
}

// ================================================================
// HANDLER
// ================================================================

internal class ImportContractFromDocumentHandler(
    IDbConnectionFactory connectionFactory,
    ILogger<ImportContractFromDocumentHandler> logger,
    IRequestClient<CreateUserRequest> createUserClient,
    Contracts.API.Extensions.EmailHandler emailHandler)
    : ICommandHandler<ImportContractFromDocumentCommand, ImportContractFromDocumentResult>
{
    public async Task<ImportContractFromDocumentResult> Handle(
        ImportContractFromDocumentCommand request,
        CancellationToken cancellationToken)
    {
        var warnings = new List<string>();

        try
        {
            logger.LogInformation("Importing contract from document: {FileName}", request.FileName);

            // ================================================================
            // BƯỚC 1: EXTRACT TEXT TỪ FILE
            // ================================================================
            string rawText;
            var fileExtension = Path.GetExtension(request.FileName).ToLower();

            if (fileExtension == ".docx")
            {
                rawText = await ExtractTextFromWordAsync(request.FileStream);
            }
            else if (fileExtension == ".pdf")
            {
                rawText = await ExtractTextFromPdfAsync(request.FileStream);
            }
            else
            {
                return new ImportContractFromDocumentResult
                {
                    Success = false,
                    ErrorMessage = $"File type không được hỗ trợ: {fileExtension}. Chỉ hỗ trợ .docx và .pdf"
                };
            }

            if (string.IsNullOrWhiteSpace(rawText))
            {
                return new ImportContractFromDocumentResult
                {
                    Success = false,
                    ErrorMessage = "Không thể đọc text từ file. File có thể bị lỗi hoặc rỗng."
                };
            }

            logger.LogInformation("Extracted {Length} characters from document", rawText.Length);

            // ================================================================
            // BƯỚC 2: PARSE THÔNG TIN TỪ TEXT
            // ================================================================
            var contractNumber = ExtractContractNumber(rawText);
            var (startDate, endDate) = ExtractDates(rawText);
            var customerName = ExtractCustomerName(rawText);
            var customerAddress = ExtractAddress(rawText);
            var customerPhone = ExtractPhoneNumber(rawText); // Đã convert 0 → +84
            var customerEmail = ExtractEmail(rawText); // Lấy từ Bên B
            var taxCode = ExtractTaxCode(rawText);
            var contactPersonName = ExtractContactPersonName(rawText); // Tên sau "Ông/Bà"
            var contactPersonTitle = ExtractContactPersonTitle(rawText); // Chức vụ
            var guardsRequired = ExtractGuardsRequired(rawText);
            var coverageType = ExtractCoverageType(rawText);
            var shiftSchedules = ExtractShiftSchedules(rawText);
            var workOnHolidays = CheckWorkOnHolidays(rawText);
            var workOnWeekends = CheckWorkOnWeekends(rawText);

            // Phân tích loại hợp đồng và thời hạn
            var contractTypeInfo = AnalyzeContractType(rawText, startDate, endDate);

            // Log extracted info for debugging
            logger.LogInformation(
                "Parsed: Contract={Contract}, Customer={Customer}, Email={Email}, Phone={Phone}, Contact={Contact}, Title={Title}, Type={Type}, Duration={Duration}",
                contractNumber, customerName, customerEmail, customerPhone, contactPersonName, contactPersonTitle, 
                contractTypeInfo.ContractType, contractTypeInfo.DurationMonths);

            // Validation
            if (string.IsNullOrEmpty(contractNumber))
            {
                warnings.Add("Không tìm thấy số hợp đồng - sẽ tự động generate");
                contractNumber = $"CTR-{DateTime.Now:yyyyMMdd}-{Guid.NewGuid().ToString().Substring(0, 4).ToUpper()}";
            }

            if (string.IsNullOrEmpty(customerName))
            {
                return new ImportContractFromDocumentResult
                {
                    Success = false,
                    ErrorMessage = "Không tìm thấy tên khách hàng trong file. Vui lòng kiểm tra lại.",
                    RawText = rawText,
                    Warnings = warnings
                };
            }

            if (!startDate.HasValue || !endDate.HasValue)
            {
                warnings.Add("Không tìm thấy ngày bắt đầu/kết thúc - sử dụng giá trị mặc định");
                startDate ??= DateTime.Now.Date;
                endDate ??= startDate.Value.AddMonths(12);
            }

            // ================================================================
            // BƯỚC 3: TẠO USER ACCOUNT CHO CUSTOMER (VIA USERS.API)
            // ================================================================
            Guid? userId = null;
            string? generatedPassword = null;

            if (!string.IsNullOrEmpty(customerEmail))
            {
                try
                {
                    // Generate password mạnh
                    generatedPassword = GenerateStrongPassword();

                    // Gửi request tới Users.API để tạo user với role "customer"
                    var createUserRequest = new CreateUserRequest
                    {
                        Email = customerEmail,
                        Password = generatedPassword,
                        FullName = customerName,
                        Phone = customerPhone,
                        Address = customerAddress,
                        RoleName = "customer",
                        AuthProvider = "email"
                    };

                    logger.LogInformation("Sending CreateUserRequest to Users.API for email: {Email}", customerEmail);

                    var response = await createUserClient.GetResponse<CreateUserResponse>(
                        createUserRequest,
                        cancellationToken,
                        timeout: RequestTimeout.After(s: 30));

                    var createUserResponse = response.Message;

                    if (createUserResponse.Success)
                    {
                        userId = createUserResponse.UserId;
                        logger.LogInformation(
                            "User account created successfully for customer: {Email}, UserId: {UserId}",
                            customerEmail, userId);
                    }
                    else
                    {
                        logger.LogWarning(
                            "Failed to create user account for customer: {Email}. Error: {Error}. Will continue without user account.",
                            customerEmail, createUserResponse.ErrorMessage);
                        warnings.Add($"Không thể tạo tài khoản đăng nhập: {createUserResponse.ErrorMessage}");
                    }
                }
                catch (Exception userEx)
                {
                    logger.LogError(userEx, "Error creating user account for customer: {Email}", customerEmail);
                    warnings.Add($"Lỗi khi tạo tài khoản đăng nhập: {userEx.Message}");
                    // Continue without user account - không fail toàn bộ import
                }
            }
            else
            {
                warnings.Add("Không có email - không thể tạo tài khoản đăng nhập cho khách hàng");
            }

            // ================================================================
            // BƯỚC 4: LƯU VÀO DATABASE
            // ================================================================
            using var connection = await connectionFactory.CreateConnectionAsync();
            using var transaction = connection.BeginTransaction();

            try
            {
                // 4.1: Tạo hoặc tìm Customer
                var customerId = await CreateOrFindCustomerAsync(
                    connection, transaction,
                    customerName, customerAddress, customerPhone, customerEmail, taxCode,
                    contactPersonName, contactPersonTitle, userId);

                logger.LogInformation("Customer created/found: {CustomerId} with contact: {ContactName} - {ContactTitle}",
                    customerId, contactPersonName, contactPersonTitle);

                // 3.2: Tạo Contract
                var durationMonths = ((endDate.Value.Year - startDate.Value.Year) * 12) +
                                    endDate.Value.Month - startDate.Value.Month;

                var contract = new Models.Contract
                {
                    Id = Guid.NewGuid(),
                    ContractNumber = contractNumber,
                    ContractTitle = $"Hợp đồng bảo vệ - {customerName}",
                    CustomerId = customerId,
                    ContractType = contractTypeInfo.ContractType,
                    ServiceScope = contractTypeInfo.ServiceScope,
                    StartDate = startDate.Value,
                    EndDate = endDate.Value,
                    DurationMonths = contractTypeInfo.DurationMonths,
                    Status = "draft", // Draft để manager review trước khi activate
                    WorkOnPublicHolidays = workOnHolidays ?? false,
                    WorkOnCustomerClosedDays = false,
                    AutoGenerateShifts = contractTypeInfo.AutoGenerateShifts,
                    GenerateShiftsAdvanceDays = contractTypeInfo.GenerateShiftsAdvanceDays,
                    IsRenewable = contractTypeInfo.IsRenewable,
                    AutoRenewal = contractTypeInfo.AutoRenewal,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = request.CreatedBy
                };

                await connection.InsertAsync(contract, transaction);
                logger.LogInformation("Contract created: {ContractId} - {ContractNumber} (Type: {Type}, Duration: {Duration} months)",
                    contract.Id, contract.ContractNumber, contract.ContractType, contract.DurationMonths);

                // 3.3: Tạo Default Location nếu có thông tin guards required
                var locationIds = new List<Guid>();
                if (guardsRequired > 0)
                {
                    var location = new Models.CustomerLocation
                    {
                        Id = Guid.NewGuid(),
                        CustomerId = customerId,
                        LocationCode = $"LOC-{DateTime.Now:yyyyMMdd}-001",
                        LocationName = $"Địa điểm mặc định - {customerName}",
                        LocationType = "office",
                        Address = customerAddress ?? "",
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    };

                    await connection.InsertAsync(location, transaction);
                    locationIds.Add(location.Id);

                    // Link location với contract
                    var contractLocation = new Models.ContractLocation
                    {
                        Id = Guid.NewGuid(),
                        ContractId = contract.Id,
                        LocationId = location.Id,
                        GuardsRequired = guardsRequired,
                        CoverageType = coverageType ?? "24x7",
                        ServiceStartDate = startDate.Value,
                        IsPrimaryLocation = true,
                        PriorityLevel = 1,
                        AutoGenerateShifts = true,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    };

                    await connection.InsertAsync(contractLocation, transaction);
                    logger.LogInformation("Location created and linked to contract: {LocationId}", location.Id);
                }
                else
                {
                    warnings.Add("Không tìm thấy số lượng bảo vệ - chưa tạo location");
                }

                // 3.4: Tạo Shift Schedules từ thông tin đã parse
                var scheduleIds = new List<Guid>();
                foreach (var shiftInfo in shiftSchedules)
                {
                    if (!shiftInfo.StartTime.HasValue || !shiftInfo.EndTime.HasValue)
                        continue;

                    var schedule = new Models.ContractShiftSchedule
                    {
                        Id = Guid.NewGuid(),
                        ContractId = contract.Id,
                        ContractLocationId = null, // Áp dụng cho tất cả locations
                        ScheduleName = shiftInfo.ShiftName ?? "Ca làm việc",
                        ScheduleType = "regular",
                        ShiftStartTime = shiftInfo.StartTime.Value,
                        ShiftEndTime = shiftInfo.EndTime.Value,
                        CrossesMidnight = shiftInfo.EndTime.Value < shiftInfo.StartTime.Value,
                        DurationHours = CalculateDuration(shiftInfo.StartTime.Value, shiftInfo.EndTime.Value),
                        BreakMinutes = 60,
                        GuardsPerShift = shiftInfo.GuardsPerShift ?? guardsRequired,
                        RecurrenceType = "weekly",
                        // Default: T2-T6
                        AppliesMonday = true,
                        AppliesTuesday = true,
                        AppliesWednesday = true,
                        AppliesThursday = true,
                        AppliesFriday = true,
                        AppliesSaturday = workOnWeekends ?? false,
                        AppliesSunday = workOnWeekends ?? false,
                        AppliesOnPublicHolidays = workOnHolidays ?? false,
                        AppliesOnWeekends = workOnWeekends ?? false,
                        SkipWhenLocationClosed = true,
                        AutoGenerateEnabled = true,
                        GenerateAdvanceDays = 30,
                        EffectiveFrom = startDate.Value,
                        EffectiveTo = endDate,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = request.CreatedBy
                    };

                    await connection.InsertAsync(schedule, transaction);
                    scheduleIds.Add(schedule.Id);

                    logger.LogInformation("Shift schedule created: {ScheduleId} - {ScheduleName}",
                        schedule.Id, schedule.ScheduleName);
                }

                if (!scheduleIds.Any())
                {
                    warnings.Add("Không tìm thấy thông tin ca làm việc - chưa tạo shift schedules");
                }

                // ================================================================
                // BƯỚC 5: COMMIT TRANSACTION
                // ================================================================
                transaction.Commit();

                // ================================================================
                // BƯỚC 6: GỬI EMAIL THÔNG TIN ĐĂNG NHẬP CHO CUSTOMER
                // ================================================================
                if (userId.HasValue && !string.IsNullOrEmpty(customerEmail) && !string.IsNullOrEmpty(generatedPassword))
                {
                    try
                    {
                        await emailHandler.SendCustomerLoginInfoEmailAsync(
                            customerName,
                            customerEmail,
                            generatedPassword,
                            contractNumber);

                        logger.LogInformation(
                            "Login info email sent successfully to customer: {Email}",
                            customerEmail);
                    }
                    catch (Exception emailEx)
                    {
                        // Log warning nhưng không fail - email không critical
                        logger.LogWarning(emailEx,
                            "Failed to send login info email to {Email}, but import was successful",
                            customerEmail);
                        warnings.Add($"Không thể gửi email thông tin đăng nhập: {emailEx.Message}");
                    }
                }

                // Calculate confidence score
                int score = CalculateConfidenceScore(
                    contractNumber, customerName, startDate, endDate,
                    guardsRequired, shiftSchedules.Count);

                var result = new ImportContractFromDocumentResult
                {
                    Success = true,
                    ContractId = contract.Id,
                    CustomerId = customerId,
                    LocationIds = locationIds,
                    ShiftScheduleIds = scheduleIds,
                    ContractNumber = contractNumber,
                    CustomerName = customerName,
                    LocationsCreated = locationIds.Count,
                    SchedulesCreated = scheduleIds.Count,
                    RawText = rawText,
                    Warnings = warnings,
                    ConfidenceScore = score
                };

                logger.LogInformation(
                    "Contract import completed: {ContractNumber} - {Locations} locations, {Schedules} schedules, User created: {UserCreated}",
                    contractNumber, locationIds.Count, scheduleIds.Count, userId.HasValue);

                return result;
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                logger.LogError(ex, "Error saving contract to database");
                throw;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error importing contract from document");
            return new ImportContractFromDocumentResult
            {
                Success = false,
                ErrorMessage = $"Lỗi import contract: {ex.Message}",
                Warnings = warnings
            };
        }
    }

    // ================================================================
    // TEXT EXTRACTION METHODS
    // ================================================================

    private async Task<string> ExtractTextFromWordAsync(Stream stream)
    {
        var text = new StringBuilder();

        using (var doc = WordprocessingDocument.Open(stream, false))
        {
            var body = doc.MainDocumentPart?.Document.Body;
            if (body == null) return string.Empty;

            foreach (var paragraph in body.Descendants<Paragraph>())
            {
                var paragraphText = paragraph.InnerText;
                if (!string.IsNullOrWhiteSpace(paragraphText))
                {
                    text.AppendLine(paragraphText);
                }
            }

            foreach (var table in body.Descendants<Table>())
            {
                foreach (var row in table.Descendants<TableRow>())
                {
                    var rowText = string.Join(" | ",
                        row.Descendants<TableCell>().Select(c => c.InnerText.Trim()));
                    if (!string.IsNullOrWhiteSpace(rowText))
                    {
                        text.AppendLine(rowText);
                    }
                }
            }
        }

        return await Task.FromResult(text.ToString());
    }

    private async Task<string> ExtractTextFromPdfAsync(Stream stream)
    {
        var text = new StringBuilder();

        try
        {
            using (var reader = new PdfReader(stream))
            {
                // iTextSharp.LGPLv2.Core simple text extraction
                for (int page = 1; page <= reader.NumberOfPages; page++)
                {
                    try
                    {
                        // Get page content bytes
                        var contentBytes = reader.GetPageContent(page);

                        if (contentBytes != null && contentBytes.Length > 0)
                        {
                            // Convert bytes to string - simple extraction
                            var pageContent = Encoding.UTF8.GetString(contentBytes);

                            // Basic text extraction - get text between BT and ET operators
                            var matches = Regex.Matches(pageContent, @"BT\s+(.*?)\s+ET", RegexOptions.Singleline);
                            foreach (Match match in matches)
                            {
                                var textBlock = match.Groups[1].Value;
                                // Extract text from Tj and TJ operators
                                var textMatches = Regex.Matches(textBlock, @"\((.*?)\)\s*Tj|\[(.*?)\]\s*TJ");
                                foreach (Match textMatch in textMatches)
                                {
                                    var extractedText = textMatch.Groups[1].Success
                                        ? textMatch.Groups[1].Value
                                        : textMatch.Groups[2].Value;
                                    if (!string.IsNullOrWhiteSpace(extractedText))
                                    {
                                        text.Append(extractedText + " ");
                                    }
                                }
                            }
                            text.AppendLine();
                        }
                    }
                    catch (Exception pageEx)
                    {
                        logger.LogWarning(pageEx, "Could not extract text from page {Page}", page);
                        // Continue with next page
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error extracting text from PDF");
            throw new InvalidOperationException("Không thể đọc file PDF. Vui lòng kiểm tra file có bị mã hóa hoặc hỏng.", ex);
        }

        return await Task.FromResult(text.ToString());
    }

    // ================================================================
    // PARSING HELPER METHODS
    // ================================================================

    /// <summary>
    /// Phân tích loại hợp đồng và thời hạn từ văn bản
    /// </summary>
    private ContractTypeInfo AnalyzeContractType(string text, DateTime? startDate, DateTime? endDate)
    {
        var info = new ContractTypeInfo();

        // Tính duration từ ngày
        if (startDate.HasValue && endDate.HasValue)
        {
            var totalDays = (endDate.Value - startDate.Value).Days;
            info.DurationMonths = ((endDate.Value.Year - startDate.Value.Year) * 12) + 
                                  endDate.Value.Month - startDate.Value.Month;
            info.TotalDays = totalDays;

            // Phân loại dựa trên số ngày
            if (totalDays <= 1)
            {
                info.ContractType = "one_day";
                info.ServiceScope = "event_based";
                info.AutoGenerateShifts = false;
                info.GenerateShiftsAdvanceDays = 0;
                info.IsRenewable = false;
                info.AutoRenewal = false;
            }
            else if (totalDays <= 7)
            {
                info.ContractType = "weekly";
                info.ServiceScope = "shift_based";
                info.AutoGenerateShifts = true;
                info.GenerateShiftsAdvanceDays = 3;
                info.IsRenewable = false;
                info.AutoRenewal = false;
            }
            else if (totalDays <= 30)
            {
                info.ContractType = "monthly";
                info.ServiceScope = "shift_based";
                info.AutoGenerateShifts = true;
                info.GenerateShiftsAdvanceDays = 7;
                info.IsRenewable = true;
                info.AutoRenewal = false;
            }
            else if (info.DurationMonths <= 6)
            {
                info.ContractType = "short_term";
                info.ServiceScope = "shift_based";
                info.AutoGenerateShifts = true;
                info.GenerateShiftsAdvanceDays = 14;
                info.IsRenewable = true;
                info.AutoRenewal = false;
            }
            else
            {
                info.ContractType = "long_term";
                info.ServiceScope = "shift_based";
                info.AutoGenerateShifts = true;
                info.GenerateShiftsAdvanceDays = 30;
                info.IsRenewable = true;
                info.AutoRenewal = false;
            }
        }
        else
        {
            // Mặc định nếu không có ngày
            info.ContractType = "long_term";
            info.ServiceScope = "shift_based";
            info.DurationMonths = 12;
            info.AutoGenerateShifts = true;
            info.GenerateShiftsAdvanceDays = 30;
            info.IsRenewable = true;
            info.AutoRenewal = false;
        }

        // Override từ keywords trong văn bản
        var lowerText = text.ToLower();
        
        if (Regex.IsMatch(lowerText, @"hợp\s*đồng\s*(dài\s*hạn|lâu\s*dài)", RegexOptions.IgnoreCase))
        {
            info.ContractType = "long_term";
            info.IsRenewable = true;
        }
        else if (Regex.IsMatch(lowerText, @"hợp\s*đồng\s*(ngắn\s*hạn|tạm\s*thời)", RegexOptions.IgnoreCase))
        {
            info.ContractType = "short_term";
            info.IsRenewable = false;
        }
        else if (Regex.IsMatch(lowerText, @"hợp\s*đồng\s*(1\s*ngày|một\s*ngày|sự\s*kiện)", RegexOptions.IgnoreCase))
        {
            info.ContractType = "one_day";
            info.ServiceScope = "event_based";
            info.AutoGenerateShifts = false;
            info.IsRenewable = false;
        }
        else if (Regex.IsMatch(lowerText, @"hợp\s*đồng\s*(tuần|7\s*ngày)", RegexOptions.IgnoreCase))
        {
            info.ContractType = "weekly";
            info.IsRenewable = false;
        }

        // Kiểm tra tự động gia hạn
        if (Regex.IsMatch(lowerText, @"tự\s*động\s*gia\s*hạn", RegexOptions.IgnoreCase))
        {
            info.AutoRenewal = true;
        }

        // Kiểm tra dịch vụ theo sự kiện
        if (Regex.IsMatch(lowerText, @"sự\s*kiện|event|buổi|occasion", RegexOptions.IgnoreCase))
        {
            info.ServiceScope = "event_based";
        }

        return info;
    }

    private string? ExtractContractNumber(string text)
    {
        var patterns = new[]
        {
            @"(?:Số\s*HĐ|Hợp\s*đồng\s*số|Contract\s*No\.?)\s*[:：]\s*([A-Z0-9\-/]+)",
            @"HĐ\s*[-:]?\s*([A-Z0-9\-/]{5,})",
            @"CTR[-\s]?(\d{4})[-\s]?(\d{3})"
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (match.Success) return match.Groups[1].Value.Trim();
        }
        return null;
    }

    private (DateTime? startDate, DateTime? endDate) ExtractDates(string text)
    {
        var pattern = @"(?:Từ\s*ngày|From)\s*(\d{1,2}[\/\-]\d{1,2}[\/\-]\d{4}).*?(?:đến\s*ngày|to)\s*(\d{1,2}[\/\-]\d{1,2}[\/\-]\d{4})";
        var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);

        DateTime? startDate = null, endDate = null;
        if (match.Success)
        {
            if (DateTime.TryParse(match.Groups[1].Value, out var start))
                startDate = start;
            if (DateTime.TryParse(match.Groups[2].Value, out var end))
                endDate = end;
        }
        return (startDate, endDate);
    }

    private string? ExtractCustomerName(string text)
    {
        var patterns = new[]
        {
            @"(?:Bên\s*B|Khách\s*hàng).*?[:：]\s*([^\r\n]+?)(?:\r|\n|Địa\s*chỉ)",
            @"Công\s*ty\s+([^\r\n]{10,80})"
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var name = match.Groups[1].Value.Trim();
                if (name.Length > 5) return name;
            }
        }
        return null;
    }

    private string? ExtractAddress(string text)
    {
        var pattern = @"(?:Địa\s*chỉ|Address).*?[:：]\s*([^\r\n]+)";
        var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    private string? ExtractPhoneNumber(string text)
    {
        // Tìm phone trong phần Bên B
        var benBIndex = text.IndexOf("BÊN B", StringComparison.OrdinalIgnoreCase);
        if (benBIndex == -1)
            benBIndex = text.IndexOf("Bên B", StringComparison.OrdinalIgnoreCase);

        if (benBIndex >= 0)
        {
            // Lấy khoảng 500 ký tự sau "Bên B"
            var textAfterBenB = text.Substring(benBIndex, Math.Min(500, text.Length - benBIndex));

            var pattern = @"(?:Điện\s*thoại|Phone|ĐT).*?[:：]\s*([\d\s\-\(\)\+]{9,20})";
            var match = Regex.Match(textAfterBenB, pattern, RegexOptions.IgnoreCase);

            if (match.Success)
            {
                var phone = Regex.Replace(match.Groups[1].Value, @"[^\d\+]", "");

                // Convert 0 đầu tiên thành +84
                if (phone.StartsWith("0"))
                {
                    phone = "+84" + phone.Substring(1);
                }
                // Nếu đã có +84 thì giữ nguyên
                else if (!phone.StartsWith("+"))
                {
                    // Nếu không có + và không bắt đầu bằng 0, thêm +84
                    phone = "+84" + phone;
                }

                return phone;
            }
        }

        return null;
    }

    private string? ExtractEmail(string text)
    {
        // Tìm phần Bên B trước
        var benBPattern = @"(?:BÊN\s*B|Bên\s*B)[\s\S]*?Email\s*[:：]\s*([a-zA-Z0-9._-]+@[a-zA-Z0-9._-]+\.[a-zA-Z]{2,})";
        var benBMatch = Regex.Match(text, benBPattern, RegexOptions.IgnoreCase);

        if (benBMatch.Success)
        {
            return benBMatch.Groups[1].Value.Trim();
        }

        // Fallback: tìm email đầu tiên sau "Bên B"
        var benBIndex = text.IndexOf("BÊN B", StringComparison.OrdinalIgnoreCase);
        if (benBIndex == -1)
            benBIndex = text.IndexOf("Bên B", StringComparison.OrdinalIgnoreCase);

        if (benBIndex >= 0)
        {
            var textAfterBenB = text.Substring(benBIndex);
            var emailPattern = @"([a-zA-Z0-9._-]+@[a-zA-Z0-9._-]+\.[a-zA-Z]{2,})";
            var emailMatch = Regex.Match(textAfterBenB, emailPattern);
            if (emailMatch.Success)
            {
                return emailMatch.Groups[1].Value;
            }
        }

        return null;
    }

    private string? ExtractTaxCode(string text)
    {
        var pattern = @"(?:Mã\s*số\s*thuế|MST).*?[:：]\s*([+\d]{10,15})";
        var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    /// <summary>
    /// Extract contact person name từ Bên B (sau chữ "Ông" hoặc "Bà")
    /// </summary>
    private string? ExtractContactPersonName(string text)
    {
        // Tìm phần Bên B
        var benBIndex = text.IndexOf("BÊN B", StringComparison.OrdinalIgnoreCase);
        if (benBIndex == -1)
            benBIndex = text.IndexOf("Bên B", StringComparison.OrdinalIgnoreCase);

        if (benBIndex >= 0)
        {
            var textAfterBenB = text.Substring(benBIndex, Math.Min(600, text.Length - benBIndex));

            // Pattern: "Đại diện: Ông/Bà TÊN – Chức vụ"
            var patterns = new[]
            {
                @"(?:Đại\s*diện|Đ/D).*?[:：]\s*(?:Ông|Bà)\s+([A-ZÁÀẢÃẠĂẮẰẲẴẶÂẤẦẨẪẬÉÈẺẼẸÊẾỀỂỄỆÍÌỈĨỊÓÒỎÕỌÔỐỒỔỖỘƠỚỜỞỠỢÚÙỦŨỤƯỨỪỬỮỰÝỲỶỸỴ][a-záàảãạăắằẳẵặâấầẩẫậéèẻẽẹêếềểễệíìỉĩịóòỏõọôốồổỗộơớờởỡợúùủũụưứừửữựýỳỷỹỵ\s]+?)(?:\s*[-–]\s*|\s*\n)",
                @"(?:Ông|Bà)\s+([A-ZÁÀẢÃẠĂẮẰẲẴẶÂẤẦẨẪẬÉÈẺẼẸÊẾỀỂỄỆÍÌỈĨỊÓÒỎÕỌÔỐỒỔỖỘƠỚỜỞỠỢÚÙỦŨỤƯỨỪỬỮỰÝỲỶỸỴ][a-záàảãạăắằẳẵặâấầẩẫậéèẻẽẹêếềểễệíìỉĩịóòỏõọôốồổỗộơớờởỡợúùủũụưứừửữựýỳỷỹỵ\s]+?)(?:\s*[-–]\s*)"
            };

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(textAfterBenB, pattern, RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    return match.Groups[1].Value.Trim();
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Extract contact person title (chức vụ) từ Bên B
    /// </summary>
    private string? ExtractContactPersonTitle(string text)
    {
        // Tìm phần Bên B
        var benBIndex = text.IndexOf("BÊN B", StringComparison.OrdinalIgnoreCase);
        if (benBIndex == -1)
            benBIndex = text.IndexOf("Bên B", StringComparison.OrdinalIgnoreCase);

        if (benBIndex >= 0)
        {
            var textAfterBenB = text.Substring(benBIndex, Math.Min(600, text.Length - benBIndex));

            // Pattern 1: "Ông TÊN – Chức vụ"
            var pattern1 = @"(?:Ông|Bà)\s+[A-ZÁÀẢÃẠĂẮẰẲẴẶÂẤẦẨẪẬÉÈẺẼẸÊẾỀỂỄỆÍÌỈĨỊÓÒỎÕỌÔỐỒỔỖỘƠỚỜỞỠỢÚÙỦŨỤƯỨỪỬỮỰÝỲỶỸỴ][a-záàảãạăắằẳẵặâấầẩẫậéèẻẽẹêếềểễệíìỉĩịóòỏõọôốồổỗộơớờởỡợúùủũụưứừửữựýỳỷỹỵ\s]+?\s*[-–]\s*([A-ZĐa-záàảãạăắằẳẵặâấầẩẫậéèẻẽẹêếềểễệíìỉĩịóòỏõọôốồổỗộơớờởỡợúùủũụưứừửữựýỳỷỹỵ\s]+?)(?:\n|$)";
            var match1 = Regex.Match(textAfterBenB, pattern1, RegexOptions.IgnoreCase);
            if (match1.Success)
            {
                return match1.Groups[1].Value.Trim();
            }

            // Pattern 2: "Chức vụ: XXX"
            var pattern2 = @"Chức\s*vụ\s*[:：]\s*([A-ZĐa-záàảãạăắằẳẵặâấầẩẫậéèẻẽẹêếềểễệíìỉĩịóòỏõọôốồổỗộơớờởỡợúùủũụưứừửữựýỳỷỹỵ\s]+?)(?:\n|$)";
            var match2 = Regex.Match(textAfterBenB, pattern2, RegexOptions.IgnoreCase);
            if (match2.Success)
            {
                return match2.Groups[1].Value.Trim();
            }
        }

        return null;
    }

    private int ExtractGuardsRequired(string text)
    {
        var patterns = new[] {
            @"(\d+)\s*(?:bảo\s*vệ|guards?)",
            @"(?:Số\s*lượng).*?[:：]\s*(\d+)"
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (match.Success && int.TryParse(match.Groups[1].Value, out var count))
                return count;
        }
        return 0;
    }

    private string? ExtractCoverageType(string text)
    {
        if (Regex.IsMatch(text, @"24\s*[\/x]\s*7", RegexOptions.IgnoreCase)) return "24x7";
        if (Regex.IsMatch(text, @"ban\s*ngày", RegexOptions.IgnoreCase)) return "day_only";
        if (Regex.IsMatch(text, @"ban\s*đêm", RegexOptions.IgnoreCase)) return "night_only";
        return null;
    }

    private List<ShiftInfo> ExtractShiftSchedules(string text)
    {
        var shifts = new List<ShiftInfo>();

        // Pattern cải tiến: match cả "Ca sáng", "Ca chiều", "Ca tối", "Ca cuối tuần", "Ca đêm"
        // Tránh match "ca cuối" riêng lẻ
        var patterns = new[]
        {
            // Pattern 1: "Ca XXX: 06:00 – 14:00" hoặc "Ca XXX: 06h00 - 14h00"
            @"Ca\s+(sáng|chiều|tối|đêm|cuối\s+tuần|khuya|trưa)[^\d]*?(\d{1,2})[h:](\d{2})?\s*[-–—]\s*(\d{1,2})[h:](\d{2})?",

            // Pattern 2: "3.1. Ca sáng: 06:00 – 14:00"
            @"\d+\.\d+\.\s*Ca\s+(sáng|chiều|tối|đêm|cuối\s+tuần|khuya|trưa)[^\d]*?(\d{1,2})[h:](\d{2})?\s*[-–—]\s*(\d{1,2})[h:](\d{2})?",

            // Pattern 3: "Ca 1:" hoặc "Ca I:"
            @"Ca\s+([IVX\d]+)[^\d]*?(\d{1,2})[h:](\d{2})?\s*[-–—]\s*(\d{1,2})[h:](\d{2})?"
        };

        foreach (var pattern in patterns)
        {
            var matches = Regex.Matches(text, pattern, RegexOptions.IgnoreCase);

            foreach (Match match in matches)
            {
                var shiftName = match.Groups[1].Value.Trim();
                var startHour = match.Groups[2].Value;
                var startMin = match.Groups[3].Success ? match.Groups[3].Value : "00";
                var endHour = match.Groups[4].Value;
                var endMin = match.Groups[5].Success ? match.Groups[5].Value : "00";

                if (TimeSpan.TryParse($"{startHour}:{startMin}", out var start) &&
                    TimeSpan.TryParse($"{endHour}:{endMin}", out var end))
                {
                    // Chuẩn hóa tên ca
                    var normalizedName = NormalizeShiftName(shiftName);

                    shifts.Add(new ShiftInfo
                    {
                        ShiftName = $"Ca {normalizedName}",
                        StartTime = start,
                        EndTime = end
                    });
                }
            }
        }

        return shifts.Distinct().ToList();
    }

    private string NormalizeShiftName(string shiftName)
    {
        shiftName = shiftName.Trim().ToLower();

        if (shiftName.Contains("cuối") && shiftName.Contains("tuần"))
            return "cuối tuần";
        if (shiftName.Contains("sáng"))
            return "sáng";
        if (shiftName.Contains("chiều"))
            return "chiều";
        if (shiftName.Contains("tối"))
            return "tối";
        if (shiftName.Contains("đêm") || shiftName.Contains("khuya"))
            return "đêm";
        if (shiftName.Contains("trưa"))
            return "trưa";

        // Nếu là số hoặc chữ số La Mã, giữ nguyên
        return shiftName;
    }

    private bool? CheckWorkOnHolidays(string text) =>
        Regex.IsMatch(text, @"làm\s*việc.*?ngày\s*lễ", RegexOptions.IgnoreCase) ? true :
        Regex.IsMatch(text, @"nghỉ.*?ngày\s*lễ", RegexOptions.IgnoreCase) ? false : null;

    private bool? CheckWorkOnWeekends(string text) =>
        Regex.IsMatch(text, @"làm\s*việc.*?cuối\s*tuần", RegexOptions.IgnoreCase) ? true :
        Regex.IsMatch(text, @"nghỉ.*?cuối\s*tuần", RegexOptions.IgnoreCase) ? false : null;

    private decimal CalculateDuration(TimeSpan start, TimeSpan end)
    {
        var duration = end - start;
        if (duration.TotalHours < 0) duration = duration.Add(TimeSpan.FromHours(24));
        return (decimal)duration.TotalHours;
    }

    private async Task<Guid> CreateOrFindCustomerAsync(
        IDbConnection connection, IDbTransaction transaction,
        string name, string? address, string? phone, string? email, string? taxCode,
        string? contactPersonName = null, string? contactPersonTitle = null, Guid? userId = null)
    {
        // Tìm customer theo tên hoặc userId
        Models.Customer? existing = null;

        if (userId.HasValue && userId.Value != Guid.Empty)
        {
            // Ưu tiên tìm theo UserId nếu có
            existing = await connection.QueryFirstOrDefaultAsync<Models.Customer>(
                "SELECT * FROM customers WHERE UserId = @UserId AND IsDeleted = 0 LIMIT 1",
                new { UserId = userId.Value }, transaction);
        }

        if (existing == null)
        {
            // Tìm theo tên nếu không tìm thấy theo UserId
            existing = await connection.QueryFirstOrDefaultAsync<Models.Customer>(
                "SELECT * FROM customers WHERE CompanyName = @Name AND IsDeleted = 0 LIMIT 1",
                new { Name = name }, transaction);
        }

        if (existing != null)
        {
            // Nếu tìm thấy customer nhưng chưa có UserId, update UserId
            if (!existing.UserId.HasValue && userId.HasValue && userId.Value != Guid.Empty)
            {
                existing.UserId = userId;
                existing.UpdatedAt = DateTime.UtcNow;
                await connection.UpdateAsync(existing, transaction);
                logger.LogInformation(
                    "Updated existing customer {CustomerId} with UserId: {UserId}",
                    existing.Id, userId);
            }
            return existing.Id;
        }

        // Tạo mới customer với UserId
        var customer = new Models.Customer
        {
            Id = Guid.NewGuid(),
            UserId = userId.HasValue && userId.Value != Guid.Empty ? userId : null, // Gán UserId từ Users.API
            CustomerCode = $"CUST-{DateTime.Now:yyyyMMdd}-{Guid.NewGuid().ToString().Substring(0, 4).ToUpper()}",
            CompanyName = name,
            Address = address,
            Phone = phone,
            Email = email,
            ContactPersonName = contactPersonName,
            ContactPersonTitle = contactPersonTitle,
            Status = "active",
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow
        };

        await connection.InsertAsync(customer, transaction);
        logger.LogInformation(
            "✓ Created new customer {CustomerCode} with UserId: {UserId}",
            customer.CustomerCode, userId);
        return customer.Id;
    }

    private int CalculateConfidenceScore(
        string? contractNumber, string? customerName,
        DateTime? startDate, DateTime? endDate,
        int guardsRequired, int schedulesCount)
    {
        int score = 0;
        if (!string.IsNullOrEmpty(contractNumber)) score += 15;
        if (!string.IsNullOrEmpty(customerName)) score += 20;
        if (startDate.HasValue) score += 15;
        if (endDate.HasValue) score += 15;
        if (guardsRequired > 0) score += 20;
        if (schedulesCount > 0) score += 15;
        return Math.Min(score, 100);
    }

    private record ContractTypeInfo
    {
        public string ContractType { get; set; } = "long_term";
        public string ServiceScope { get; set; } = "shift_based";
        public int DurationMonths { get; set; }
        public int TotalDays { get; set; }
        public bool AutoGenerateShifts { get; set; } = true;
        public int GenerateShiftsAdvanceDays { get; set; } = 30;
        public bool IsRenewable { get; set; } = true;
        public bool AutoRenewal { get; set; } = false;
    }

    private record ShiftInfo
    {
        public string? ShiftName { get; init; }
        public TimeSpan? StartTime { get; init; }
        public TimeSpan? EndTime { get; init; }
        public int? GuardsPerShift { get; init; }
    }

    /// <summary>
    /// Generate password mạnh, dễ đọc cho customer
    /// Format: Abc12345@ (chữ hoa + chữ thường + số + ký tự đặc biệt)
    /// </summary>
    private string GenerateStrongPassword()
    {
        const string upperChars = "ABCDEFGHJKLMNPQRSTUVWXYZ"; // Bỏ I, O dễ nhầm
        const string lowerChars = "abcdefghijkmnopqrstuvwxyz"; // Bỏ l dễ nhầm
        const string digits = "23456789"; // Bỏ 0, 1 dễ nhầm
        const string special = "@#$%";

        var random = new Random();
        var password = new char[10];

        // Đảm bảo có ít nhất 1 ký tự mỗi loại
        password[0] = upperChars[random.Next(upperChars.Length)];
        password[1] = lowerChars[random.Next(lowerChars.Length)];
        password[2] = lowerChars[random.Next(lowerChars.Length)];
        password[3] = digits[random.Next(digits.Length)];
        password[4] = digits[random.Next(digits.Length)];
        password[5] = digits[random.Next(digits.Length)];
        password[6] = digits[random.Next(digits.Length)];
        password[7] = digits[random.Next(digits.Length)];
        password[8] = special[random.Next(special.Length)];

        // Ký tự cuối random
        var allChars = upperChars + lowerChars + digits;
        password[9] = allChars[random.Next(allChars.Length)];

        return new string(password);
    }
}
