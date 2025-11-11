using BuildingBlocks.Messaging.Events;
using System.Text.Json;

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

internal class ImportContractFromDocumentHandler(
    IDbConnectionFactory connectionFactory,
    ILogger<ImportContractFromDocumentHandler> logger,
    IRequestClient<CreateUserRequest> createUserClient,
    Contracts.API.Extensions.EmailHandler emailHandler,
    IConfiguration configuration)
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
            var customerPhone = ExtractPhoneNumber(rawText); 
            var customerEmail = ExtractEmail(rawText); 
            var taxCode = ExtractTaxCode(rawText);
            var contactPersonName = ExtractContactPersonName(rawText); 
            var contactPersonTitle = ExtractContactPersonTitle(rawText); 
            var guardsRequired = ExtractGuardsRequired(rawText);
            var coverageType = ExtractCoverageType(rawText);
            var shiftSchedules = ExtractShiftSchedules(rawText);
            var workOnHolidays = CheckWorkOnHolidays(rawText);
            var workOnWeekends = CheckWorkOnWeekends(rawText);
            
            var (locationName, locationAddress) = ExtractLocationDetails(rawText);
            
            var (periodStartDate, periodEndDate, periodDuration) = ExtractContractPeriod(rawText);
            
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

                // 4.2: Log customer sync to customer_sync_log
                if (userId.HasValue)
                {
                    var syncLog = new Models.CustomerSyncLog
                    {
                        Id = Guid.NewGuid(),
                        UserId = userId.Value,
                        SyncType = "CREATE",
                        SyncStatus = "SUCCESS",
                        FieldsChanged = System.Text.Json.JsonSerializer.Serialize(new[] { "CompanyName", "Address", "Phone", "Email", "ContactPersonName", "ContactPersonTitle" }),
                        NewValues = System.Text.Json.JsonSerializer.Serialize(new
                        {
                            CompanyName = customerName,
                            Address = customerAddress,
                            Phone = customerPhone,
                            Email = customerEmail,
                            ContactPersonName = contactPersonName,
                            ContactPersonTitle = contactPersonTitle
                        }),
                        SyncInitiatedBy = "CONTRACT_IMPORT",
                        RetryCount = 0,
                        SyncStartedAt = DateTime.UtcNow,
                        SyncCompletedAt = DateTime.UtcNow,
                        SyncDurationMs = 0,
                        CreatedAt = DateTime.UtcNow
                    };

                    await connection.InsertAsync(syncLog, transaction);
                    logger.LogInformation("Customer sync logged for UserId: {UserId}", userId.Value);
                }

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
                    CoverageModel = "fixed_schedule",
                    StartDate = startDate.Value,
                    EndDate = endDate.Value,
                    DurationMonths = contractTypeInfo.DurationMonths,
                    Status = "draft", // Draft để manager review trước khi activate
                    FollowsCustomerCalendar = true,
                    WorkOnPublicHolidays = workOnHolidays ?? false,
                    WorkOnCustomerClosedDays = false,
                    AutoGenerateShifts = contractTypeInfo.AutoGenerateShifts,
                    GenerateShiftsAdvanceDays = contractTypeInfo.GenerateShiftsAdvanceDays,
                    IsRenewable = contractTypeInfo.IsRenewable,
                    AutoRenewal = contractTypeInfo.AutoRenewal,
                    RenewalNoticeDays = 30,
                    RenewalCount = 0,
                    IsDeleted = false,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = request.CreatedBy
                };

                await connection.InsertAsync(contract, transaction);
                logger.LogInformation("Contract created: {ContractId} - {ContractNumber} (Type: {Type}, Duration: {Duration} months)",
                    contract.Id, contract.ContractNumber, contract.ContractType, contract.DurationMonths);

                // 3.2.1: Tạo Contract Period từ ĐIỀU 2
                await CreateOrUpdateContractPeriodAsync(
                    connection,
                    transaction,
                    contract.Id,
                    periodStartDate ?? startDate,
                    periodEndDate ?? endDate,
                    periodDuration,
                    isRenewal: false);

                // 3.3: Tạo Default Location nếu có thông tin guards required
                var locationIds = new List<Guid>();
                if (guardsRequired > 0)
                {
                    // Lấy địa chỉ location từ ĐIỀU 1, fallback về customer address
                    var finalLocationAddress = locationAddress ?? customerAddress ?? "";
                    var finalLocationName = locationName ?? $"Địa điểm mặc định - {customerName}";

                    // Lấy GPS coordinates từ địa chỉ
                    decimal? latitude = null;
                    decimal? longitude = null;

                    if (!string.IsNullOrWhiteSpace(finalLocationAddress))
                    {
                        try
                        {
                            var coordinates = await GetGpsCoordinatesAsync(finalLocationAddress);
                            if (coordinates.HasValue)
                            {
                                latitude = coordinates.Value.Latitude;
                                longitude = coordinates.Value.Longitude;
                                logger.LogInformation(
                                    "GPS coordinates retrieved for location: Lat={Lat}, Lng={Lng}",
                                    latitude, longitude);
                            }
                            else
                            {
                                warnings.Add("Không thể lấy tọa độ GPS từ địa chỉ - location sẽ được tạo không có GPS");
                            }
                        }
                        catch (Exception gpsEx)
                        {
                            logger.LogWarning(gpsEx, "Failed to get GPS coordinates for address: {Address}", finalLocationAddress);
                            warnings.Add($"Lỗi khi lấy tọa độ GPS: {gpsEx.Message}");
                        }
                    }

                    var location = new Models.CustomerLocation
                    {
                        Id = Guid.NewGuid(),
                        CustomerId = customerId,
                        LocationCode = $"LOC-{DateTime.Now:yyyyMMdd}-001",
                        LocationName = finalLocationName,
                        LocationType = "office",
                        Address = finalLocationAddress,
                        Latitude = latitude,
                        Longitude = longitude,
                        GeofenceRadiusMeters = 100, // Default 100 meters
                        OperatingHoursType = "24/7",
                        FollowsStandardWorkweek = true,
                        Requires24x7Coverage = false,
                        AllowsSingleGuard = true,
                        MinimumGuardsRequired = 1,
                        IsActive = true,
                        IsDeleted = false,
                        CreatedAt = DateTime.UtcNow
                    };

                    await connection.InsertAsync(location, transaction);
                    locationIds.Add(location.Id);

                    logger.LogInformation(
                        "Location created: {LocationName} at {Address} (GPS: {HasGps})",
                        location.LocationName, location.Address, latitude.HasValue);

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
                        IsDeleted = false,
                        CreatedAt = DateTime.UtcNow
                    };

                    await connection.InsertAsync(contractLocation, transaction);
                    logger.LogInformation("Location linked to contract: {LocationId}", location.Id);
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
                        AppliesOnCustomerHolidays = true,
                        AppliesOnWeekends = workOnWeekends ?? false,
                        SkipWhenLocationClosed = true,
                        RequiresArmedGuard = false,
                        RequiresSupervisor = false,
                        MinimumExperienceMonths = 0,
                        AutoGenerateEnabled = true,
                        GenerateAdvanceDays = 30,
                        EffectiveFrom = startDate.Value,
                        EffectiveTo = endDate,
                        IsActive = true,
                        IsDeleted = false,
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
                // 3.5: TRÍCH XUẤT VÀ LƯU ĐIỀU KIỆN LÀM VIỆC
                // ================================================================
                var workingConditions = ExtractWorkingConditions(rawText);

                var contractWorkingConditions = new Models.ContractWorkingConditions
                {
                    Id = Guid.NewGuid(),
                    ContractId = contract.Id,

                    // Giờ làm việc chuẩn
                    StandardHoursPerDay = 8m,
                    StandardHoursPerWeek = 40m,
                    StandardHoursPerMonth = 160m,

                    // Giới hạn tăng ca
                    MaxOvertimeHoursPerDay = workingConditions.MaxOvertimeHoursPerDay,
                    MaxOvertimeHoursPerMonth = workingConditions.MaxOvertimeHoursPerMonth,
                    MaxOvertimeHoursPerYear = workingConditions.MaxOvertimeHoursPerMonth.HasValue 
                        ? workingConditions.MaxOvertimeHoursPerMonth.Value * 12m 
                        : null,
                    AllowOvertimeOnWeekends = workingConditions.AllowsOvertime,
                    AllowOvertimeOnHolidays = workingConditions.AllowsOvertime,
                    RequireOvertimeApproval = workingConditions.RequiresOvertimeApproval,

                    // Ca đêm
                    NightShiftStartTime = workingConditions.NightShiftStartTime,
                    NightShiftEndTime = workingConditions.NightShiftEndTime.HasValue 
                        ? TimeSpan.FromHours((double)workingConditions.NightShiftEndTime.Value) 
                        : null,
                    MinimumNightShiftHours = 2m,

                    // Ca trực liên tục
                    AllowContinuous24hShift = workingConditions.ContinuousShift24hRate.HasValue,
                    AllowContinuous48hShift = workingConditions.ContinuousShift48hRate.HasValue,
                    CountSleepTimeInContinuousShift = workingConditions.CountSleepTimeInContinuousShift,
                    SleepTimeCalculationRatio = workingConditions.SleepTimeCalculationRatio,
                    MinimumRestHoursBetweenShifts = workingConditions.MinimumRestHoursBetweenShifts,

                    // Ngày nghỉ & ngày lễ
                    AnnualLeaveDays = workingConditions.PaidLeaveDaysPerYear,
                    TetHolidayDates = workingConditions.TetHolidayDates,
                    LocalHolidaysList = workingConditions.LocalHolidaysList,
                    HolidayWeekendCalculationMethod = workingConditions.HolidayWeekendCalculationMethod,
                    SaturdayAsRegularWorkday = workingConditions.SaturdayAsRegularWorkday,

                    // Chính sách vi phạm
                    OvertimeLimitViolationPolicy = workingConditions.OvertimeLimitViolationPolicy,
                    UnapprovedOvertimePolicy = workingConditions.UnapprovedOvertimePolicy,
                    InsufficientRestPolicy = "compensate",

                    // Ca đặc biệt
                    AllowEventShift = workingConditions.EventShiftRate.HasValue,
                    AllowEmergencyCall = workingConditions.EmergencyCallRate.HasValue,
                    AllowReplacementShift = workingConditions.ReplacementShiftRate.HasValue,
                    MinimumEmergencyNoticeMinutes = 60,

                    // Ghi chú
                    GeneralNotes = workingConditions.SpecialRequirements,
                    SpecialTerms = workingConditions.PenaltyTerms,

                    IsActive = true,
                    EffectiveFrom = contract.StartDate,
                    CreatedBy = request.CreatedBy,
                    UpdatedBy = request.CreatedBy,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await connection.InsertAsync(contractWorkingConditions, transaction);

                logger.LogInformation(
                    "Working conditions saved for contract: {ContractId}",
                    contract.Id);

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
            // Pattern 1: Số thứ tự/năm/HĐDV-BV/HCM/tên đối tác (001/2025/HDDV-BV/HCM/NVHSV)
            @"(?:Số\s*HĐ|Hợp\s*đồng\s*số|Contract\s*No\.?)\s*[:：]?\s*(\d{3,4}/\d{4}/[A-Z\-]+/[A-Z]+/[A-Z]+)",
        
            // Pattern 2: Fallback - match trực tiếp format XXX/YYYY/HDDV-BV/...
            @"(\d{3,4}/\d{4}/HĐDV-BV/[A-Z]+/[A-Z]+)",
        
            // Pattern 3: Format cũ - HĐ số hoặc Contract No
            @"(?:Số\s*HĐ|Hợp\s*đồng\s*số|Contract\s*No\.?)\s*[:：]\s*([A-Z0-9\-/]+)",
        
            // Pattern 4: HĐ với mã
            @"HĐ\s*[-:]?\s*([A-Z0-9\-/]{5,})",
        
            // Pattern 5: CTR format
            @"CTR[-\s]?(\d{4})[-\s]?(\d{3})"
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                // Với pattern 5 (CTR), cần ghép groups
                if (match.Groups.Count > 2 && !string.IsNullOrEmpty(match.Groups[2].Value))
                {
                    return $"{match.Groups[1].Value}-{match.Groups[2].Value}".Trim();
                }
            
                return match.Groups[1].Value.Trim();
            }
        }
    
        return null;
    }


    private (DateTime? startDate, DateTime? endDate) ExtractDates(string text)
    {
        // Mở rộng patterns để cover nhiều trường hợp hơn
        var patterns = new[]
        {
            // Pattern 1: "có hiệu lực từ ngày ... đến hết ngày ..."
            @"(?:có\s+hiệu\s+lực\s+)?từ\s+ngày\s+(\d{1,2}[\/\-]\d{1,2}[\/\-]\d{4})\s+đến\s+(?:hết\s+)?ngày\s+(\d{1,2}[\/\-]\d{1,2}[\/\-]\d{4})",
        
            // Pattern 2: "Từ ngày ... đến ngày ..."
            @"(?:Từ|từ)\s+ngày\s+(\d{1,2}[\/\-]\d{1,2}[\/\-]\d{4})\s+đến\s+ngày\s+(\d{1,2}[\/\-]\d{1,2}[\/\-]\d{4})",
        
            // Pattern 3: English format
            @"(?:From|from)\s+(\d{1,2}[\/\-]\d{1,2}[\/\-]\d{4})\s+(?:to|until)\s+(\d{1,2}[\/\-]\d{1,2}[\/\-]\d{4})",
        
            // Pattern 4: "Bắt đầu từ ... kết thúc ..."
            @"(?:Bắt\s+đầu\s+từ|bắt\s+đầu\s+từ)\s+(?:ngày\s+)?(\d{1,2}[\/\-]\d{1,2}[\/\-]\d{4})\s+(?:kết\s+thúc|đến)\s+(?:ngày\s+)?(\d{1,2}[\/\-]\d{1,2}[\/\-]\d{4})"
        };

        DateTime? startDate = null, endDate = null;

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                if (DateTime.TryParse(match.Groups[1].Value, out var start))
                    startDate = start;
                if (DateTime.TryParse(match.Groups[2].Value, out var end))
                    endDate = end;
            
                if (startDate.HasValue && endDate.HasValue)
                    break; // Tìm thấy thì dừng
            }
        }

        return (startDate, endDate);
    }

    private (DateTime? startDate, DateTime? endDate, string? duration) ExtractContractPeriod(string text)
    {
        // Tìm ĐIỀU 2 về thời hạn hợp đồng
        var dieu2Index = text.IndexOf("ĐIỀU 2", StringComparison.OrdinalIgnoreCase);
        if (dieu2Index == -1)
            dieu2Index = text.IndexOf("Điều 2", StringComparison.OrdinalIgnoreCase);

        string searchText = text;
        if (dieu2Index >= 0)
        {
            // Lấy khoảng 1000 ký tự sau "ĐIỀU 2" để tìm thông tin thời hạn (tăng từ 800)
            searchText = text.Substring(dieu2Index, Math.Min(1000, text.Length - dieu2Index));
            logger.LogInformation("📋 Found ĐIỀU 2 section for contract period extraction");
        }

        DateTime? startDate = null, endDate = null;
        string? duration = null;

        // Pattern 1: "Từ ngày DD/MM/YYYY đến ngày DD/MM/YYYY"
        var datePatterns = new[]
        {
            // Match với "có hiệu lực từ ngày ... đến hết ngày ..."
            @"(?:có\s+hiệu\s+lực\s+)?từ\s+ngày\s+(\d{1,2}[\/\-]\d{1,2}[\/\-]\d{4})\s+đến\s+(?:hết\s+)?ngày\s+(\d{1,2}[\/\-]\d{1,2}[\/\-]\d{4})",
        
            // Match với "Từ ngày ... đến ngày ..."
            @"(?:Từ|từ)\s+ngày\s+(\d{1,2}[\/\-]\d{1,2}[\/\-]\d{4})\s+đến\s+ngày\s+(\d{1,2}[\/\-]\d{1,2}[\/\-]\d{4})",
        
            // Match với "Bắt đầu từ ngày ... đến ngày ..."
            @"(?:Bắt\s+đầu\s+từ|bắt\s+đầu\s+từ)\s+ngày\s+(\d{1,2}[\/\-]\d{1,2}[\/\-]\d{4})\s+đến\s+(?:ngày\s+)?(\d{1,2}[\/\-]\d{1,2}[\/\-]\d{4})",
        
            // Match với "hiệu lực kể từ ... đến ..."
            @"(?:hiệu\s+lực\s+)?kể\s+từ\s+(?:ngày\s+)?(\d{1,2}[\/\-]\d{1,2}[\/\-]\d{4})\s+đến\s+(?:hết\s+)?(?:ngày\s+)?(\d{1,2}[\/\-]\d{1,2}[\/\-]\d{4})"
        };
        foreach (var pattern in datePatterns)
        {
            var dateMatch = Regex.Match(searchText, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        
            if (dateMatch.Success)
            {
                if (DateTime.TryParse(dateMatch.Groups[1].Value, out var start))
                    startDate = start;
                if (DateTime.TryParse(dateMatch.Groups[2].Value, out var end))
                    endDate = end;

                if (startDate.HasValue && endDate.HasValue)
                {
                    logger.LogInformation("✓ Extracted period dates: {Start} to {End}", startDate, endDate);
                    break; // Tìm thấy thì dừng
                }
            }
        }

        // Pattern 2: "Thời hạn X tháng/năm" hoặc "Hợp đồng có hiệu lực X tháng/năm"
        var durationPattern = @"(?:thời\s*hạn|hiệu\s*lực|thời\s*gian)[:\s]*(\d+)\s*(tháng|năm|ngày)";
        var durationMatch = Regex.Match(searchText, durationPattern, RegexOptions.IgnoreCase);

        if (durationMatch.Success)
        {
            duration = $"{durationMatch.Groups[1].Value} {durationMatch.Groups[2].Value}";
            logger.LogInformation("✓ Extracted duration: {Duration}", duration);
        }

        return (startDate, endDate, duration);
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
        // Tìm địa chỉ trong phần Bên B
        var benBIndex = text.IndexOf("BÊN B", StringComparison.OrdinalIgnoreCase);
        if (benBIndex == -1)
            benBIndex = text.IndexOf("Bên B", StringComparison.OrdinalIgnoreCase);

        if (benBIndex >= 0)
        {
            // Lấy khoảng 600 ký tự sau "Bên B"
            var textAfterBenB = text.Substring(benBIndex, Math.Min(600, text.Length - benBIndex));

            var pattern = @"(?:Địa\s*chỉ|Address).*?[:：]\s*([^\r\n]+)";
            var match = Regex.Match(textAfterBenB, pattern, RegexOptions.IgnoreCase);

            if (match.Success)
            {
                return match.Groups[1].Value.Trim();
            }
        }

        // Fallback: tìm địa chỉ đầu tiên trong toàn bộ văn bản (nếu không tìm thấy Bên B)
        var fallbackPattern = @"(?:Địa\s*chỉ|Address).*?[:：]\s*([^\r\n]+)";
        var fallbackMatch = Regex.Match(text, fallbackPattern, RegexOptions.IgnoreCase);
        return fallbackMatch.Success ? fallbackMatch.Groups[1].Value.Trim() : null;
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

    /// <summary>
    /// Extract location details từ ĐIỀU 1: ĐỐI TƯỢNG VÀ PHẠM VI HỢP ĐỒNG
    /// </summary>
    private (string? LocationName, string? LocationAddress) ExtractLocationDetails(string text)
    {
        // Tìm phần ĐIỀU 1
        var dieu1Pattern = @"ĐIỀU\s*1\s*[:：]?\s*(?:ĐỐI\s*TƯỢNG\s*VÀ\s*PHẠM\s*VI\s*HỢP\s*ĐỒNG)?([\s\S]{0,800})(?:ĐIỀU\s*2|$)";
        var dieu1Match = Regex.Match(text, dieu1Pattern, RegexOptions.IgnoreCase);

        if (!dieu1Match.Success)
        {
            return (null, null);
        }

        var dieu1Text = dieu1Match.Groups[1].Value;

        // Extract tên địa điểm: "Tên địa điểm: Siêu thị Mart - Chi nhánh Quận 1"
        string? locationName = null;
        var namePatterns = new[]
        {
            @"Tên\s*địa\s*điểm\s*[:：]\s*([^\r\n]+)",
            @"(?:tại|ở)\s*địa\s*điểm\s*[:：]?\s*([^\r\n]{10,100})"
        };

        foreach (var pattern in namePatterns)
        {
            var match = Regex.Match(dieu1Text, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                locationName = match.Groups[1].Value.Trim();
                // Clean up
                locationName = Regex.Replace(locationName, @"\s*[-–]\s*Địa\s*chỉ.*", "", RegexOptions.IgnoreCase);
                break;
            }
        }

        // Extract địa chỉ: "Địa chỉ: 789 Nguyễn Huệ, Quận 1, TP.HCM"
        string? locationAddress = null;
        var addressPatterns = new[]
        {
            @"Địa\s*chỉ\s*[:：]\s*([^\r\n]+)",
            @"(?:tại|ở)\s*[:：]?\s*(\d+\s+[^,\r\n]+(?:,\s*[^,\r\n]+){1,3})"
        };

        foreach (var pattern in addressPatterns)
        {
            var match = Regex.Match(dieu1Text, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                locationAddress = match.Groups[1].Value.Trim();
                // Clean up: remove "- Số lượng" and after
                locationAddress = Regex.Replace(locationAddress, @"\s*[-–]\s*Số\s*lượng.*", "", RegexOptions.IgnoreCase);
                break;
            }
        }

        logger.LogInformation(
            "Extracted location from ĐIỀU 1 - Name: {Name}, Address: {Address}",
            locationName, locationAddress);

        return (locationName, locationAddress);
    }

    /// <summary>
    /// Lấy GPS coordinates cho địa chỉ Việt Nam - Tối ưu độ chính xác với Nominatim
    /// Strategy: Structured Query → Viewbox → Fallback
    /// </summary>
    private async Task<(decimal? Latitude, decimal? Longitude)?> GetGpsCoordinatesAsync(string? address)
    {
        if (string.IsNullOrWhiteSpace(address)) return null;

        try
        {
            logger.LogInformation("🌍 Getting GPS for: {Address}", address);

            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "BASMS-Contracts-API/1.0");

            var addr = ParseVietnameseAddressComponents(address);

            // TRY 1: Structured query (chính xác cao nhất)
            var result = await QueryNominatim(httpClient, addr, "structured");
            if (result.HasValue) return result;

            // TRY 2: Viewbox query (giới hạn khu vực)
            result = await QueryNominatim(httpClient, addr, "viewbox");
            if (result.HasValue) return result;

            // TRY 3: Simple fallback
            result = await QueryNominatim(httpClient, addr, "simple");
            if (result.HasValue) return result;

            logger.LogWarning("❌ No GPS found for: {Address}", address);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "GPS lookup error: {Address}", address);
            return null;
        }
    }

    /// <summary>
    /// Unified Nominatim query với 3 strategies
    /// </summary>
    private async Task<(decimal? Latitude, decimal? Longitude)?> QueryNominatim(
        HttpClient client, VietnameseAddress addr, string strategy)
    {
        string url;
        var streetFull = string.IsNullOrEmpty(addr.HouseNumber) ? addr.Street : $"{addr.HouseNumber} {addr.Street}";

        switch (strategy)
        {
            case "structured":
                // Structured: street=X&city=Y&state=Z (cao nhất)
                if (string.IsNullOrEmpty(addr.Street)) return null;
                var parts = new List<string>
                {
                    $"street={Uri.EscapeDataString(streetFull)}",
                    $"city={Uri.EscapeDataString(addr.District)}",
                    $"state={Uri.EscapeDataString(addr.City)}",
                    "country=Vietnam",
                    "format=json",
                    "addressdetails=1",
                    "limit=5"
                };
                url = $"https://nominatim.openstreetmap.org/search?{string.Join("&", parts)}";
                break;

            case "viewbox":
                // Viewbox: giới hạn tìm kiếm trong quận
                if (string.IsNullOrEmpty(addr.Street)) return null;
                var viewbox = GetDistrictViewbox(addr.District, addr.City);
                if (viewbox == null) return null;
                var query = $"{streetFull}, {addr.District}, {addr.City}";
                url = $"https://nominatim.openstreetmap.org/search?q={Uri.EscapeDataString(query)}&format=json&addressdetails=1&limit=10&countrycodes=vn&viewbox={viewbox}&bounded=1";
                break;

            case "simple":
                // Simple: street + district + city
                if (string.IsNullOrEmpty(addr.Street)) return null;
                var simpleQuery = $"{addr.Street}, {addr.District}, {addr.City}, Vietnam";
                url = $"https://nominatim.openstreetmap.org/search?q={Uri.EscapeDataString(simpleQuery)}&format=json&addressdetails=1&limit=10&countrycodes=vn";
                break;

            default:
                return null;
        }

        try
        {
            var response = await client.GetStringAsync(url);
            var results = JsonDocument.Parse(response).RootElement;

            if (results.GetArrayLength() > 0)
            {
                var best = SelectBestResult(results, addr);
                if (best.HasValue)
                {
                    var lat = decimal.Parse(best.Value.GetProperty("lat").GetString()!);
                    var lon = decimal.Parse(best.Value.GetProperty("lon").GetString()!);
                    var type = best.Value.TryGetProperty("type", out var t) ? t.GetString() : "";
                    var houseNum = best.Value.TryGetProperty("address", out var a) && a.TryGetProperty("house_number", out var hn)
                        ? hn.GetString() : "N/A";

                    logger.LogInformation("  ✓ [{Strategy}] {Lat}, {Lon} (Type: {Type}, House#: {HouseNum})",
                        strategy.ToUpper(), lat, lon, type, houseNum);

                    await Task.Delay(1100); // Rate limit
                    return (lat, lon);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning("  ✗ {Strategy} failed: {Error}", strategy, ex.Message);
        }

        await Task.Delay(1100);
        return null;
    }

    /// <summary>
    /// Chọn kết quả tốt nhất - ưu tiên house_number
    /// </summary>
    private JsonElement? SelectBestResult(JsonElement results, VietnameseAddress addr)
    {
        JsonElement? best = null;
        double bestScore = 0;

        foreach (var r in results.EnumerateArray())
        {
            double score = r.TryGetProperty("importance", out var imp) ? imp.GetDouble() * 100 : 0;
            var type = r.TryGetProperty("type", out var t) ? t.GetString() : "";
            var osm_type = r.TryGetProperty("osm_type", out var ot) ? ot.GetString() : "";

            // CRITICAL: +300 cho house_number
            if (r.TryGetProperty("address", out var addrObj) && addrObj.TryGetProperty("house_number", out _))
                score += 300;

            // Type bonuses
            if (type == "house" || type == "building") score += 150;
            if (type == "amenity" || type == "office") score += 120;
            if (osm_type == "node") score += 50;

            // Penalty cho road nếu có số nhà
            if (!string.IsNullOrEmpty(addr.HouseNumber) && (type == "road" || type == "highway"))
                score -= 100;

            if (score > bestScore)
            {
                bestScore = score;
                best = r;
            }
        }

        return best;
    }

    // ================================================================
    // CONTRACT PERIOD MANAGEMENT
    // ================================================================

    /// <summary>
    /// Tạo hoặc cập nhật Contract Period
    /// - Lần đầu: tạo period với PeriodNumber = 1
    /// - Gia hạn: tạo record mới với PeriodNumber tăng lên, đánh dấu period cũ là IsCurrentPeriod = false
    /// </summary>
    private async Task CreateOrUpdateContractPeriodAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        Guid contractId,
        DateTime? startDate,
        DateTime? endDate,
        string? duration,
        bool isRenewal = false)
    {
        if (!startDate.HasValue || !endDate.HasValue)
        {
            logger.LogWarning("⚠ Cannot create contract period - missing start or end date");
            return;
        }

        try
        {
            // Kiểm tra xem đã có period nào chưa
            var existingPeriods = await connection.QueryAsync<ContractPeriod>(
                "SELECT * FROM contract_periods WHERE ContractId = @ContractId ORDER BY PeriodNumber DESC",
                new { ContractId = contractId },
                transaction);

            var existingPeriodsList = existingPeriods.ToList();

            if (!existingPeriodsList.Any())
            {
                // Lần đầu - tạo period mới với PeriodNumber = 1
                var newPeriod = new ContractPeriod
                {
                    Id = Guid.NewGuid(),
                    ContractId = contractId,
                    PeriodNumber = 1,
                    PeriodType = "initial",
                    PeriodStartDate = startDate.Value,
                    PeriodEndDate = endDate.Value,
                    IsCurrentPeriod = true,
                    Notes = duration != null ? $"Thời hạn: {duration}" : "Initial contract period",
                    CreatedAt = DateTime.UtcNow
                };

                await connection.InsertAsync(newPeriod, transaction);
                logger.LogInformation("✓ Created initial contract period (Period 1): {Start} to {End}",
                    startDate.Value.ToString("dd/MM/yyyy"),
                    endDate.Value.ToString("dd/MM/yyyy"));
            }
            else
            {
                // Đã có period - xử lý gia hạn hoặc update
                var currentPeriod = existingPeriodsList.First(); // Period mới nhất

                if (isRenewal)
                {
                    // Gia hạn - đánh dấu period cũ là không còn current
                    currentPeriod.IsCurrentPeriod = false;
                    await connection.UpdateAsync(currentPeriod, transaction);

                    // Tạo period mới với PeriodNumber tăng lên
                    var renewalPeriod = new ContractPeriod
                    {
                        Id = Guid.NewGuid(),
                        ContractId = contractId,
                        PeriodNumber = currentPeriod.PeriodNumber + 1,
                        PeriodType = "renewal",
                        PeriodStartDate = startDate.Value,
                        PeriodEndDate = endDate.Value,
                        IsCurrentPeriod = true,
                        Notes = duration != null ? $"Gia hạn lần {currentPeriod.PeriodNumber}. Thời hạn: {duration}" : $"Renewal {currentPeriod.PeriodNumber}",
                        CreatedAt = DateTime.UtcNow
                    };

                    await connection.InsertAsync(renewalPeriod, transaction);
                    logger.LogInformation("✓ Created renewal period (Period {PeriodNumber}): {Start} to {End}",
                        renewalPeriod.PeriodNumber,
                        startDate.Value.ToString("dd/MM/yyyy"),
                        endDate.Value.ToString("dd/MM/yyyy"));

                    // Log lịch sử gia hạn
                    logger.LogInformation("📋 Contract period history: Old period {OldNumber} ({OldEnd}) → New period {NewNumber} ({NewEnd})",
                        currentPeriod.PeriodNumber,
                        currentPeriod.PeriodEndDate.ToString("dd/MM/yyyy"),
                        renewalPeriod.PeriodNumber,
                        renewalPeriod.PeriodEndDate.ToString("dd/MM/yyyy"));
                }
                else
                {
                    // Update thời gian trong period hiện tại (không phải gia hạn)
                    if (currentPeriod.PeriodEndDate != endDate.Value || currentPeriod.PeriodStartDate != startDate.Value)
                    {
                        var oldStartDate = currentPeriod.PeriodStartDate;
                        var oldEndDate = currentPeriod.PeriodEndDate;

                        currentPeriod.PeriodStartDate = startDate.Value;
                        currentPeriod.PeriodEndDate = endDate.Value;
                        if (duration != null)
                        {
                            currentPeriod.Notes = $"Thời hạn: {duration} (Updated)";
                        }

                        await connection.UpdateAsync(currentPeriod, transaction);
                        logger.LogInformation("✓ Updated contract period {PeriodNumber}: {OldStart}-{OldEnd} → {NewStart}-{NewEnd}",
                            currentPeriod.PeriodNumber,
                            oldStartDate.ToString("dd/MM/yyyy"),
                            oldEndDate.ToString("dd/MM/yyyy"),
                            startDate.Value.ToString("dd/MM/yyyy"),
                            endDate.Value.ToString("dd/MM/yyyy"));
                    }
                    else
                    {
                        logger.LogInformation("Contract period unchanged - no update needed");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create/update contract period");
            throw;
        }
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

    /// <summary>
    /// Parse địa chỉ Việt Nam thành các components chi tiết
    /// </summary>
    private VietnameseAddress ParseVietnameseAddressComponents(string address)
    {
        var addr = new VietnameseAddress();

        // Split by comma
        var parts = address.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .ToList();

        if (parts.Count == 0) return addr;

        // Extract số nhà + tên đường từ phần đầu
        var houseMatch = Regex.Match(parts[0], @"^([\d]+[A-Z]?)\s+(.+)");
        if (houseMatch.Success)
        {
            addr.HouseNumber = houseMatch.Groups[1].Value;
            addr.Street = houseMatch.Groups[2].Value.Trim();
        }
        else
        {
            addr.Street = parts[0];
        }

        // Extract phường/ward
        addr.Ward = parts.FirstOrDefault(p => p.Contains("Phường") || p.Contains("Phư") || p.Contains("P."));

        // Extract quận/district
        addr.District = parts.FirstOrDefault(p =>
            p.Contains("Quận") || p.Contains("Huyện") ||
            p.Contains("Thành phố") || p.Contains("Thị xã"));

        // Extract thành phố
        var cityPart = parts.LastOrDefault();
        addr.City = NormalizeCityNameSimple(cityPart);

        return addr;
    }

    /// <summary>
    /// Viewbox cho các quận TP.HCM phổ biến (minlon,minlat,maxlon,maxlat)
    /// </summary>
    private string? GetDistrictViewbox(string? district, string city)
    {
        if (string.IsNullOrEmpty(district)) return null;

        // Chỉ áp dụng cho TP.HCM
        if (!city.Contains("Ho Chi Minh") && !city.Contains("Hồ Chí Minh") && !city.Contains("Sài Gòn"))
            return null;

        var districtNum = district.Replace("Quận ", "").Replace("Q.", "").Trim();

        return districtNum switch
        {
            "1" => "106.690,10.760,106.710,10.785", // Quận 1
            "3" => "106.665,10.765,106.695,10.795", // Quận 3
            "4" => "106.695,10.745,106.720,10.770", // Quận 4
            "5" => "106.655,10.745,106.685,10.770", // Quận 5
            "10" => "106.655,10.765,106.685,10.795", // Quận 10
            "Bình Thạnh" or "Binh Thanh" => "106.690,10.790,106.730,10.830", // Bình Thạnh
            "Phú Nhuận" or "Phu Nhuan" => "106.670,10.790,106.705,10.820", // Phú Nhuận
            "Tân Bình" or "Tan Binh" => "106.620,10.775,106.670,10.825", // Tân Bình
            _ => "106.60,10.70,106.80,10.85" // Bounding box toàn TP.HCM
        };
    }

    /// <summary>
    /// Bỏ dấu tiếng Việt
    /// </summary>
    private string RemoveVietnameseDiacritics(string? text)
    {
        if (string.IsNullOrEmpty(text)) return "";

        var withoutDiacritics = text.Normalize(System.Text.NormalizationForm.FormD);
        var sb = new StringBuilder();

        foreach (var c in withoutDiacritics)
        {
            var category = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
            if (category != System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }

        // Replace đ -> d, Đ -> D
        return sb.ToString()
            .Replace("đ", "d")
            .Replace("Đ", "D")
            .Normalize(System.Text.NormalizationForm.FormC);
    }

    /// <summary>
    /// Chuẩn hóa tên thành phố đơn giản (không thêm ", Vietnam")
    /// </summary>
    private string NormalizeCityNameSimple(string? city)
    {
        if (string.IsNullOrEmpty(city)) return "Ho Chi Minh City";

        var normalized = city.Trim();

        if (normalized.Contains("Hồ Chí Minh") || normalized.Contains("TP.HCM") ||
            normalized.Contains("TPHCM") || normalized.Contains("Sài Gòn") ||
            normalized.Contains("Saigon"))
            return "Ho Chi Minh City";

        if (normalized.Contains("Hà Nội") || normalized.Contains("Hanoi"))
            return "Hanoi";

        if (normalized.Contains("Đà Nẵng") || normalized.Contains("Da Nang"))
            return "Da Nang";

        if (normalized.Contains("Cần Thơ") || normalized.Contains("Can Tho"))
            return "Can Tho";

        if (normalized.Contains("Hải Phòng") || normalized.Contains("Hai Phong"))
            return "Hai Phong";

        return normalized;
    }

     // ================================================================
      // WORKING CONDITIONS EXTRACTION
      // ================================================================

      /// <summary>
      /// Trích xuất điều kiện làm việc từ hợp đồng (ĐIỀU 4, ĐIỀU 5, hoặc các điều khoản khác)
      /// </summary>
      private WorkingConditionsInfo ExtractWorkingConditions(string text)
      {
          var info = new WorkingConditionsInfo();

          // ================================================================
          // LÀM BÙ GIỜ (COMPENSATORY TIME OFF)
          // ================================================================

          // Pattern: "cho phép làm bù" hoặc "được làm bù giờ"
          if (Regex.IsMatch(text, @"(cho\s*phép|được)\s*(làm\s*bù|bù\s*giờ)", RegexOptions.IgnoreCase))
          {
              info.AllowsCompensatoryTimeOff = true;

              // Tỷ lệ: "1:1", "1:1.5", "tỷ lệ 1 ăn 1.5"
              var ratioPattern = @"(?:tỷ\s*lệ|bù)\s*(?:là\s*)?(?:1\s*[:ăn]\s*([\d\.]+)|(\d+\.?\d*)\s*[:ăn]\s*(\d+\.?\d*))";
              var ratioMatch = Regex.Match(text, ratioPattern, RegexOptions.IgnoreCase);

              if (ratioMatch.Success)
              {
                  if (ratioMatch.Groups[1].Success && decimal.TryParse(ratioMatch.Groups[1].Value, out var ratio1))
                  {
                      info.CompensatoryTimeOffRatio = ratio1;
                  }
                  else if (ratioMatch.Groups[2].Success && decimal.TryParse(ratioMatch.Groups[2].Value, out var ratio2) &&
                           ratioMatch.Groups[3].Success && decimal.TryParse(ratioMatch.Groups[3].Value, out var ratio3))
                  {
                      info.CompensatoryTimeOffRatio = ratio3 / ratio2;
                  }
              }
              else
              {
                  info.CompensatoryTimeOffRatio = 1.0m; // Default 1:1
              }

              // Số ngày tối đa: "tối đa 2 ngày/tháng"
              var maxDaysPattern = @"(?:tối\s*đa|không\s*quá)\s*(\d+)\s*ngày.*?tháng";
              var maxDaysMatch = Regex.Match(text, maxDaysPattern, RegexOptions.IgnoreCase);

              if (maxDaysMatch.Success && int.TryParse(maxDaysMatch.Groups[1].Value, out var maxDays))
              {
                  info.MaxCompensatoryDaysPerMonth = maxDays;
              }
          }

          // ================================================================
          // TĂNG CA (OVERTIME)
          // ================================================================

          // Kiểm tra có cho phép tăng ca không
          if (Regex.IsMatch(text, @"tăng\s*ca|làm\s*thêm\s*giờ|over\s*time", RegexOptions.IgnoreCase))
          {
              info.AllowsOvertime = true;

              // Hệ số tăng ca ngày thường: "1.5 lần", "150%", "hệ số 1.5x"
              var weekdayPattern = @"(?:ngày\s*thường|ngày\s*làm\s*việc).*?(?:hệ\s*số|lần|tỷ\s*lệ)\s*(?:là\s*)?(\d+\.?\d*)\s*[x%lần]?";
              var weekdayMatch = Regex.Match(text, weekdayPattern, RegexOptions.IgnoreCase);

              if (weekdayMatch.Success && decimal.TryParse(weekdayMatch.Groups[1].Value, out var weekdayRate))
              {
                  info.OvertimeRateWeekday = weekdayRate;
              }
              else
              {
                  // Default: 1.5x cho ngày thường
                  info.OvertimeRateWeekday = 1.5m;
              }

              // Hệ số cuối tuần
              var weekendPattern = @"(?:cuối\s*tuần|thứ\s*7|chủ\s*nhật).*?(?:hệ\s*số|lần|tỷ\s*lệ)\s*(?:là\s*)?(\d+\.?\d*)\s*[x%lần]?";
              var weekendMatch = Regex.Match(text, weekendPattern, RegexOptions.IgnoreCase);

              if (weekendMatch.Success && decimal.TryParse(weekendMatch.Groups[1].Value, out var weekendRate))
              {
                  info.OvertimeRateWeekend = weekendRate;
              }
              else
              {
                  info.OvertimeRateWeekend = 2.0m; // Default: 2.0x
              }

              // Hệ số ngày lễ
              var holidayPattern = @"(?:ngày\s*lễ|ngày\s*nghỉ).*?(?:hệ\s*số|lần|tỷ\s*lệ)\s*(?:là\s*)?(\d+\.?\d*)\s*[x%lần]?";
              var holidayMatch = Regex.Match(text, holidayPattern, RegexOptions.IgnoreCase);

              if (holidayMatch.Success && decimal.TryParse(holidayMatch.Groups[1].Value, out var holidayRate))
              {
                  info.OvertimeRateHoliday = holidayRate;
              }
              else
              {
                  info.OvertimeRateHoliday = 3.0m; // Default: 3.0x
              }

              // Số giờ tối đa mỗi ngày: "tối đa 4 giờ/ngày"
              var maxHoursPattern = @"(?:tối\s*đa|không\s*quá)\s*(\d+)\s*giờ.*?ngày";
              var maxHoursMatch = Regex.Match(text, maxHoursPattern, RegexOptions.IgnoreCase);

              if (maxHoursMatch.Success && int.TryParse(maxHoursMatch.Groups[1].Value, out var maxHours))
              {
                  info.MaxOvertimeHoursPerDay = maxHours;
              }

              // Số giờ tối đa mỗi tháng: "tối đa 40 giờ/tháng"
              var maxMonthPattern = @"(?:tối\s*đa|không\s*quá)\s*(\d+)\s*giờ.*?tháng";
              var maxMonthMatch = Regex.Match(text, maxMonthPattern, RegexOptions.IgnoreCase);

              if (maxMonthMatch.Success && int.TryParse(maxMonthMatch.Groups[1].Value, out var maxMonth))
              {
                  info.MaxOvertimeHoursPerMonth = maxMonth;
              }

              // Yêu cầu phê duyệt
              info.RequiresOvertimeApproval = Regex.IsMatch(text,
                  @"phải\s*(được\s*)?phê\s*duyệt|cần\s*sự\s*đồng\s*ý",
                  RegexOptions.IgnoreCase);
          }

          // ================================================================
          // NGÀY LỄ (PUBLIC HOLIDAYS)
          // ================================================================

          // Hệ số lương ngày lễ
          var publicHolidayRatePattern = @"ngày\s*lễ.*?(?:hệ\s*số|lương|tỷ\s*lệ)\s*(?:là\s*)?(\d+\.?\d*)\s*[x%lần]?";
          var publicHolidayRateMatch = Regex.Match(text, publicHolidayRatePattern, RegexOptions.IgnoreCase);

          if (publicHolidayRateMatch.Success && decimal.TryParse(publicHolidayRateMatch.Groups[1].Value, out var pubHolidayRate))
          {
              info.PublicHolidayRate = pubHolidayRate;
          }

          // Nghỉ bù nếu làm ngày lễ
          info.AllowsPublicHolidayCompensation = Regex.IsMatch(text,
              @"(nghỉ\s*bù|được\s*nghỉ\s*thay).*?ngày\s*lễ",
              RegexOptions.IgnoreCase);

          // ================================================================
          // NGÀY NGHỈ (LEAVE)
          // ================================================================

          // Ngày nghỉ phép có lương mỗi tháng: "1 ngày phép/tháng"
          var paidLeaveMonthPattern = @"(\d+)\s*ngày.*?(?:phép|nghỉ).*?tháng";
          var paidLeaveMonthMatch = Regex.Match(text, paidLeaveMonthPattern, RegexOptions.IgnoreCase);

          if (paidLeaveMonthMatch.Success && int.TryParse(paidLeaveMonthMatch.Groups[1].Value, out var leaveMonth))
          {
              info.PaidLeaveDaysPerMonth = leaveMonth;
          }

          // Ngày nghỉ phép có lương mỗi năm: "12 ngày phép/năm"
          var paidLeaveYearPattern = @"(\d+)\s*ngày.*?(?:phép|nghỉ).*?năm";
          var paidLeaveYearMatch = Regex.Match(text, paidLeaveYearPattern, RegexOptions.IgnoreCase);

          if (paidLeaveYearMatch.Success && int.TryParse(paidLeaveYearMatch.Groups[1].Value, out var leaveYear))
          {
              info.PaidLeaveDaysPerYear = leaveYear;
          }

          // Ngày nghỉ ốm: "30 ngày nghỉ ốm/năm"
          var sickLeavePattern = @"(\d+)\s*ngày.*?(?:ốm|bệnh).*?năm";
          var sickLeaveMatch = Regex.Match(text, sickLeavePattern, RegexOptions.IgnoreCase);

          if (sickLeaveMatch.Success && int.TryParse(sickLeaveMatch.Groups[1].Value, out var sickDays))
          {
              info.SickLeaveDaysPerYear = sickDays;
          }

          // Theo lịch khách hàng
          info.FollowsCustomerSchedule = Regex.IsMatch(text,
              @"theo\s*lịch.*?khách\s*hàng|nghỉ\s*theo\s*khách",
              RegexOptions.IgnoreCase);

          // Làm khi khách đóng cửa
          info.WorkWhenCustomerClosed = !Regex.IsMatch(text,
              @"không\s*làm\s*việc.*?đóng\s*cửa|nghỉ\s*khi.*?đóng\s*cửa",
              RegexOptions.IgnoreCase);

          // ================================================================
          // CUỐI TUẦN (WEEKENDS)
          // ================================================================

          var weekendRatePattern = @"(?:cuối\s*tuần|saturday|sunday).*?(?:hệ\s*số|lương|tỷ\s*lệ)\s*(?:là\s*)?(\d+\.?\d*)\s*[x%lần]?";
          var weekendRateMatch = Regex.Match(text, weekendRatePattern, RegexOptions.IgnoreCase);

          if (weekendRateMatch.Success && decimal.TryParse(weekendRateMatch.Groups[1].Value, out var wkndRate))
          {
              info.WeekendRate = wkndRate;
          }

          // Thứ 7
          var saturdayPattern = @"(?:thứ\s*7|thứ\s*bảy|saturday).*?(?:hệ\s*số|lương|tỷ\s*lệ)\s*(?:là\s*)?(\d+\.?\d*)\s*[x%lần]?";
          var saturdayMatch = Regex.Match(text, saturdayPattern, RegexOptions.IgnoreCase);

          if (saturdayMatch.Success && decimal.TryParse(saturdayMatch.Groups[1].Value, out var satRate))
          {
              info.SaturdayRate = satRate;
          }

          // Chủ nhật
          var sundayPattern = @"(?:chủ\s*nhật|sunday).*?(?:hệ\s*số|lương|tỷ\s*lệ)\s*(?:là\s*)?(\d+\.?\d*)\s*[x%lần]?";
          var sundayMatch = Regex.Match(text, sundayPattern, RegexOptions.IgnoreCase);

          if (sundayMatch.Success && decimal.TryParse(sundayMatch.Groups[1].Value, out var sunRate))
          {
              info.SundayRate = sunRate;
          }

          // T7 là ngày thường
          info.SaturdayAsRegularWorkday = Regex.IsMatch(text,
              @"thứ\s*7.*?(?:làm\s*việc\s*bình\s*thường|ngày\s*thường)",
              RegexOptions.IgnoreCase);

          // ================================================================
          // CA ĐÊM & TĂNG CA QUA ĐÊM
          // ================================================================

          // Hệ số ca đêm: "ca đêm hệ số 1.3x" hoặc "22h-6h: 1.5x"
          var nightShiftPattern = @"(?:ca\s*đêm|ca\s*khuya|night\s*shift).*?(?:hệ\s*số|lương|tỷ\s*lệ)\s*(?:là\s*)?(\d+\.?\d*)\s*[x%lần]?";
          var nightShiftMatch = Regex.Match(text, nightShiftPattern, RegexOptions.IgnoreCase);

          if (nightShiftMatch.Success && decimal.TryParse(nightShiftMatch.Groups[1].Value, out var nightRate))
          {
              info.NightShiftRate = nightRate;
          }
          else if (Regex.IsMatch(text, @"ca\s*đêm|22[h:]00|night", RegexOptions.IgnoreCase))
          {
              info.NightShiftRate = 1.3m; // Default theo luật lao động VN
          }

          // Khung giờ ca đêm
          var nightTimePattern = @"(?:ca\s*đêm|night).*?(\d{1,2})[h:](\d{2})?\s*[-–]\s*(\d{1,2})[h:](\d{2})?";
          var nightTimeMatch = Regex.Match(text, nightTimePattern, RegexOptions.IgnoreCase);

          if (nightTimeMatch.Success)
          {
              var startHour = nightTimeMatch.Groups[1].Value;
              var startMin = nightTimeMatch.Groups[2].Success ? nightTimeMatch.Groups[2].Value : "00";

              if (TimeSpan.TryParse($"{startHour}:{startMin}", out var nightStart))
              {
                  info.NightShiftStartTime = nightStart;
              }
          }

          // Phụ cấp ca đêm cố định
          var nightAllowancePattern = @"(?:phụ\s*cấp\s*ca\s*đêm|ca\s*đêm\s*phụ\s*cấp).*?([\d,\.]+)\s*(?:đồng|vnđ|vnd)";
          var nightAllowanceMatch = Regex.Match(text, nightAllowancePattern, RegexOptions.IgnoreCase);

          if (nightAllowanceMatch.Success)
          {
              var allowanceStr = nightAllowanceMatch.Groups[1].Value.Replace(",", "").Replace(".", "");
              if (decimal.TryParse(allowanceStr, out var nightAllowance))
              {
                  info.NightShiftAllowance = nightAllowance;
              }
          }

          // Tăng ca đêm = NightRate × OvertimeRate
          if (info.NightShiftRate.HasValue)
          {
              if (info.OvertimeRateWeekday.HasValue)
                  info.OvertimeNightWeekdayRate = info.NightShiftRate.Value * info.OvertimeRateWeekday.Value;

              if (info.OvertimeRateWeekend.HasValue)
                  info.OvertimeNightWeekendRate = info.NightShiftRate.Value * info.OvertimeRateWeekend.Value;

              if (info.OvertimeRateHoliday.HasValue)
                  info.OvertimeNightHolidayRate = info.NightShiftRate.Value * info.OvertimeRateHoliday.Value;
          }

          // ================================================================
          // CA TRỰC LIÊN TỤC
          // ================================================================

          // Ca trực 24h
          var continuous24hPattern = @"(?:ca\s*trực|trực)\s*24\s*(?:giờ|h).*?(?:hệ\s*số|lương|tỷ\s*lệ)\s*(?:là\s*)?(\d+\.?\d*)\s*[x%lần]?";
          var continuous24hMatch = Regex.Match(text, continuous24hPattern, RegexOptions.IgnoreCase);

          if (continuous24hMatch.Success && decimal.TryParse(continuous24hMatch.Groups[1].Value, out var cont24h))
          {
              info.ContinuousShift24hRate = cont24h;
          }

          // Ca trực 48h
          var continuous48hPattern = @"(?:ca\s*trực|trực)\s*48\s*(?:giờ|h).*?(?:hệ\s*số|lương|tỷ\s*lệ)\s*(?:là\s*)?(\d+\.?\d*)\s*[x%lần]?";
          var continuous48hMatch = Regex.Match(text, continuous48hPattern, RegexOptions.IgnoreCase);

          if (continuous48hMatch.Success && decimal.TryParse(continuous48hMatch.Groups[1].Value, out var cont48h))
          {
              info.ContinuousShift48hRate = cont48h;
          }

          // Tính giờ ngủ
          var sleepTimePattern = @"(?:giờ\s*ngủ|thời\s*gian\s*nghỉ).*?(\d+)\s*%";
          var sleepTimeMatch = Regex.Match(text, sleepTimePattern, RegexOptions.IgnoreCase);

          if (sleepTimeMatch.Success && int.TryParse(sleepTimeMatch.Groups[1].Value, out var sleepPercent))
          {
              info.SleepTimeCalculationRatio = sleepPercent / 100m;
          }
          else if (Regex.IsMatch(text, @"không\s*tính.*?giờ\s*ngủ", RegexOptions.IgnoreCase))
          {
              info.CountSleepTimeInContinuousShift = false;
          }

          // Nghỉ giữa ca
          var restBetweenShiftsPattern = @"(?:nghỉ\s*giữa\s*ca|nghỉ\s*ngơi).*?(\d+)\s*giờ";
          var restBetweenShiftsMatch = Regex.Match(text, restBetweenShiftsPattern, RegexOptions.IgnoreCase);

          if (restBetweenShiftsMatch.Success && decimal.TryParse(restBetweenShiftsMatch.Groups[1].Value, out var restHours))
          {
              info.MinimumRestHoursBetweenShifts = restHours;
          }
          else
          {
              info.MinimumRestHoursBetweenShifts = 11m; // Theo luật lao động VN
          }

          // Làm 2 ca liên tiếp
          var consecutivePattern = @"(?:2\s*ca\s*liên\s*tiếp|làm\s*liên\s*tục).*?(?:hệ\s*số|lương|tỷ\s*lệ)\s*(?:là\s*)?(\d+\.?\d*)\s*[x%lần]?";
          var consecutiveMatch = Regex.Match(text, consecutivePattern, RegexOptions.IgnoreCase);

          if (consecutiveMatch.Success && decimal.TryParse(consecutiveMatch.Groups[1].Value, out var consRate))
          {
              info.ConsecutiveShiftRate = consRate;
          }

          // ================================================================
          // TẾT & NGÀY LỄ ĐẶC BIỆT
          // ================================================================

          // Tết Nguyên Đán
          var tetPattern = @"(?:tết|nguyên\s*đán|lunar\s*new\s*year).*?(?:hệ\s*số|lương|tỷ\s*lệ)\s*(?:là\s*)?(\d+\.?\d*)\s*[x%lần]?";
          var tetMatch = Regex.Match(text, tetPattern, RegexOptions.IgnoreCase);

          if (tetMatch.Success && decimal.TryParse(tetMatch.Groups[1].Value, out var tetRate))
          {
              info.TetHolidayRate = tetRate;
          }
          else if (Regex.IsMatch(text, @"tết|nguyên\s*đán", RegexOptions.IgnoreCase))
          {
              info.TetHolidayRate = 4.0m; // Default cao nhất
          }

          // Ca trực xuyên Tết
          var tetContinuousPattern = @"(?:trực.*?tết|tết.*?trực).*?(?:hệ\s*số|lương|tỷ\s*lệ)\s*(?:là\s*)?(\d+\.?\d*)\s*[x%lần]?";
          var tetContinuousMatch = Regex.Match(text, tetContinuousPattern, RegexOptions.IgnoreCase);

          if (tetContinuousMatch.Success && decimal.TryParse(tetContinuousMatch.Groups[1].Value, out var tetContRate))
          {
              info.TetContinuousShiftRate = tetContRate;
          }

          // Phụ cấp Tết
          var tetAllowancePattern = @"(?:thưởng\s*tết|phụ\s*cấp\s*tết).*?([\d,\.]+)\s*(?:đồng|vnđ|triệu)";
          var tetAllowanceMatch = Regex.Match(text, tetAllowancePattern, RegexOptions.IgnoreCase);

          if (tetAllowanceMatch.Success)
          {
              var tetAllowanceStr = tetAllowanceMatch.Groups[1].Value.Replace(",", "").Replace(".", "");
              if (decimal.TryParse(tetAllowanceStr, out var tetAllowance))
              {
                  // Nếu có từ "triệu" thì nhân 1,000,000
                  if (tetAllowanceMatch.Value.Contains("triệu"))
                      tetAllowance *= 1_000_000;
                  else if (tetAllowanceMatch.Value.Contains("k") || tetAllowanceMatch.Value.Contains("K"))
                      tetAllowance *= 1000;

                  info.TetShiftAllowance = tetAllowance;
              }
          }

          // Ngày lễ rơi vào cuối tuần
          if (Regex.IsMatch(text, @"ngày\s*lễ.*?cuối\s*tuần.*?(cộng\s*dồn|tổng\s*cộng)", RegexOptions.IgnoreCase))
          {
              info.HolidayWeekendCalculationMethod = "cumulative";
          }
          else if (Regex.IsMatch(text, @"ngày\s*lễ.*?cuối\s*tuần.*?(cao\s*nhất|lớn\s*hơn)", RegexOptions.IgnoreCase))
          {
              info.HolidayWeekendCalculationMethod = "max";
          }

          // ================================================================
          // CA SỰ KIỆN & KHẨN CẤP
          // ================================================================

          // Ca sự kiện
          var eventPattern = @"(?:ca\s*sự\s*kiện|sự\s*kiện\s*đặc\s*biệt).*?(?:hệ\s*số|lương|tỷ\s*lệ)\s*(?:là\s*)?(\d+\.?\d*)\s*[x%lần]?";
          var eventMatch = Regex.Match(text, eventPattern, RegexOptions.IgnoreCase);

          if (eventMatch.Success && decimal.TryParse(eventMatch.Groups[1].Value, out var eventRate))
          {
              info.EventShiftRate = eventRate;
          }

          // Ca khẩn cấp
          var emergencyPattern = @"(?:ca\s*khẩn\s*cấp|gọi\s*đột\s*xuất|emergency).*?(?:hệ\s*số|lương|tỷ\s*lệ)\s*(?:là\s*)?(\d+\.?\d*)\s*[x%lần]?";
          var emergencyMatch = Regex.Match(text, emergencyPattern, RegexOptions.IgnoreCase);

          if (emergencyMatch.Success && decimal.TryParse(emergencyMatch.Groups[1].Value, out var emergencyRate))
          {
              info.EmergencyCallRate = emergencyRate;
          }

          // Ca thay thế
          var replacementPattern = @"(?:ca\s*thay\s*thế|thay\s*ca).*?(?:hệ\s*số|lương|tỷ\s*lệ)\s*(?:là\s*)?(\d+\.?\d*)\s*[x%lần]?";
          var replacementMatch = Regex.Match(text, replacementPattern, RegexOptions.IgnoreCase);

          if (replacementMatch.Success && decimal.TryParse(replacementMatch.Groups[1].Value, out var replaceRate))
          {
              info.ReplacementShiftRate = replaceRate;
          }

          // ================================================================
          // VI PHẠM GIỚI HẠN & CHÍNH SÁCH
          // ================================================================

          // Vượt giới hạn tăng ca
          if (Regex.IsMatch(text, @"không\s*cho\s*phép.*?vượt.*?tăng\s*ca", RegexOptions.IgnoreCase))
          {
              info.OvertimeLimitViolationPolicy = "not_allowed";
          }
          else if (Regex.IsMatch(text, @"vượt.*?tăng\s*ca.*?(phê\s*duyệt|approval)", RegexOptions.IgnoreCase))
          {
              info.OvertimeLimitViolationPolicy = "requires_approval";
          }
          else if (Regex.IsMatch(text, @"vượt.*?tăng\s*ca.*?(phạt|bồi\s*thường)", RegexOptions.IgnoreCase))
          {
              info.OvertimeLimitViolationPolicy = "penalty";
          }

          // Hệ số bồi thường vượt giới hạn
          var violationRatePattern = @"(?:vượt.*?tăng\s*ca|vượt\s*giờ).*?(?:hệ\s*số|tỷ\s*lệ)\s*(?:là\s*)?(\d+\.?\d*)\s*[x%lần]?";
          var violationRateMatch = Regex.Match(text, violationRatePattern, RegexOptions.IgnoreCase);

          if (violationRateMatch.Success && decimal.TryParse(violationRateMatch.Groups[1].Value, out var violationRate))
          {
              info.OvertimeLimitViolationRate = violationRate;
          }

          // Tăng ca không phê duyệt
          if (Regex.IsMatch(text, @"không\s*phê\s*duyệt.*?(từ\s*chối|không\s*tính)", RegexOptions.IgnoreCase))
          {
              info.UnapprovedOvertimePolicy = "reject";
          }
          else if (Regex.IsMatch(text, @"không\s*phê\s*duyệt.*?phạt", RegexOptions.IgnoreCase))
          {
              info.UnapprovedOvertimePolicy = "accept_with_penalty";
          }

          // ================================================================
          // PHỤ CẤP
          // ================================================================

          // Phụ cấp ăn ca
          var mealAllowancePattern = @"(?:phụ\s*cấp\s*ăn|ăn\s*ca|meal).*?([\d,\.]+)\s*(?:đồng|vnđ|k)";
          var mealAllowanceMatch = Regex.Match(text, mealAllowancePattern, RegexOptions.IgnoreCase);

          if (mealAllowanceMatch.Success)
          {
              var mealStr = mealAllowanceMatch.Groups[1].Value.Replace(",", "").Replace(".", "");
              if (decimal.TryParse(mealStr, out var mealAllowance))
              {
                  if (mealAllowanceMatch.Value.Contains("k") || mealAllowanceMatch.Value.Contains("K"))
                      mealAllowance *= 1000;

                  info.MealAllowancePerShift = mealAllowance;
              }
          }

          // Phụ cấp đi lại
          var transportPattern = @"(?:phụ\s*cấp\s*đi\s*lại|xăng\s*xe|transport).*?([\d,\.]+)\s*(?:đồng|vnđ|k)";
          var transportMatch = Regex.Match(text, transportPattern, RegexOptions.IgnoreCase);

          if (transportMatch.Success)
          {
              var transportStr = transportMatch.Groups[1].Value.Replace(",", "").Replace(".", "");
              if (decimal.TryParse(transportStr, out var transportAllowance))
              {
                  if (transportMatch.Value.Contains("k") || transportMatch.Value.Contains("K"))
                      transportAllowance *= 1000;

                  info.TransportAllowancePerShift = transportAllowance;
              }
          }

          // Phụ cấp điện thoại
          var phonePattern = @"(?:phụ\s*cấp\s*điện\s*thoại|phone).*?([\d,\.]+)\s*(?:đồng|vnđ|k)";
          var phoneMatch = Regex.Match(text, phonePattern, RegexOptions.IgnoreCase);

          if (phoneMatch.Success)
          {
              var phoneStr = phoneMatch.Groups[1].Value.Replace(",", "").Replace(".", "");
              if (decimal.TryParse(phoneStr, out var phoneAllowance))
              {
                  if (phoneMatch.Value.Contains("k") || phoneMatch.Value.Contains("K"))
                      phoneAllowance *= 1000;

                  info.PhoneAllowancePerMonth = phoneAllowance;
              }
          }

          // Phụ cấp trách nhiệm
          var supervisorPattern = @"(?:phụ\s*cấp\s*trách\s*nhiệm|trưởng\s*ca).*?([\d,\.]+)\s*(?:đồng|vnđ|k|triệu)";
          var supervisorMatch = Regex.Match(text, supervisorPattern, RegexOptions.IgnoreCase);

          if (supervisorMatch.Success)
          {
              var supervisorStr = supervisorMatch.Groups[1].Value.Replace(",", "").Replace(".", "");
              if (decimal.TryParse(supervisorStr, out var supervisorAllowance))
              {
                  if (supervisorMatch.Value.Contains("triệu"))
                      supervisorAllowance *= 1_000_000;
                  else if (supervisorMatch.Value.Contains("k") || supervisorMatch.Value.Contains("K"))
                      supervisorAllowance *= 1000;

                  info.SupervisorAllowance = supervisorAllowance;
              }
          }

          // ================================================================
          // ĐIỀU KIỆN ĐẶC BIỆT (SPECIAL CONDITIONS)
          // ================================================================

          // Tìm ĐIỀU 4, ĐIỀU 5 cho các điều kiện đặc biệt
          var dieu4Pattern = @"ĐIỀU\s*[4４]\s*[:：]?([\s\S]{0,1000})(?:ĐIỀU\s*[5５]|$)";
          var dieu4Match = Regex.Match(text, dieu4Pattern, RegexOptions.IgnoreCase);

          if (dieu4Match.Success)
          {
              var dieu4Text = dieu4Match.Groups[1].Value;

              // Yêu cầu đặc biệt
              if (Regex.IsMatch(dieu4Text, @"yêu\s*cầu|điều\s*kiện|quy\s*định", RegexOptions.IgnoreCase))
              {
                  info.SpecialRequirements = dieu4Text.Trim().Substring(0, Math.Min(500, dieu4Text.Length));
              }
          }

          // Tìm phạt/bồi thường
          if (Regex.IsMatch(text, @"(phạt|bồi\s*thường|vi\s*phạm)", RegexOptions.IgnoreCase))
          {
              var penaltyPattern = @"(ĐIỀU.*?(?:phạt|bồi\s*thường|vi\s*phạm)[\s\S]{0,500})";
              var penaltyMatch = Regex.Match(text, penaltyPattern, RegexOptions.IgnoreCase);

              if (penaltyMatch.Success)
              {
                  info.PenaltyTerms = penaltyMatch.Groups[1].Value.Trim();
              }
          }

          // Tìm thưởng
          if (Regex.IsMatch(text, @"(thưởng|khen\s*thưởng|ưu\s*đãi)", RegexOptions.IgnoreCase))
          {
              var bonusPattern = @"(ĐIỀU.*?(?:thưởng|khen\s*thưởng|ưu\s*đãi)[\s\S]{0,500})";
              var bonusMatch = Regex.Match(text, bonusPattern, RegexOptions.IgnoreCase);

              if (bonusMatch.Success)
              {
                  info.BonusTerms = bonusMatch.Groups[1].Value.Trim();
              }
          }

          return info;
      }

      /// <summary>
      /// DTO cho working conditions đã extract
      /// </summary>
      private record WorkingConditionsInfo
      {
          // Làm bù giờ
          public bool AllowsCompensatoryTimeOff { get; set; } = false;
          public decimal? CompensatoryTimeOffRatio { get; set; }
          public int? MaxCompensatoryDaysPerMonth { get; set; }
          public string? CompensatoryTimeOffNotes { get; set; }

          // Tăng ca
          public bool AllowsOvertime { get; set; } = true;
          public decimal? OvertimeRateWeekday { get; set; }
          public decimal? OvertimeRateWeekend { get; set; }
          public decimal? OvertimeRateHoliday { get; set; }
          public int? MaxOvertimeHoursPerDay { get; set; }
          public int? MaxOvertimeHoursPerMonth { get; set; }
          public bool RequiresOvertimeApproval { get; set; } = true;
          public string? OvertimeNotes { get; set; }

          // Ca đêm
          public decimal? NightShiftRate { get; set; }
          public TimeSpan? NightShiftStartTime { get; set; }
          public decimal? NightShiftEndTime { get; set; }
          public decimal? OvertimeNightWeekdayRate { get; set; }
          public decimal? OvertimeNightWeekendRate { get; set; }
          public decimal? OvertimeNightHolidayRate { get; set; }
          public decimal? NightShiftAllowance { get; set; }

          // Ca trực liên tục
          public decimal? ContinuousShift24hRate { get; set; }
          public decimal? ContinuousShift48hRate { get; set; }
          public bool CountSleepTimeInContinuousShift { get; set; } = true;
          public decimal? SleepTimeCalculationRatio { get; set; }
          public decimal? MinimumRestHoursBetweenShifts { get; set; }
          public decimal? InsufficientRestCompensationRate { get; set; }
          public decimal? ConsecutiveShiftRate { get; set; }

          // Tết & ngày lễ đặc biệt
          public decimal? TetHolidayRate { get; set; }
          public string? TetHolidayDates { get; set; }
          public decimal? TetContinuousShiftRate { get; set; }
          public decimal? TetShiftAllowance { get; set; }
          public string? HolidayWeekendCalculationMethod { get; set; }
          public string? LocalHolidaysList { get; set; }
          public decimal? LocalHolidayRate { get; set; }

          // Ngày lễ
          public decimal? PublicHolidayRate { get; set; }
          public bool AllowsPublicHolidayCompensation { get; set; } = false;
          public string? PublicHolidaysList { get; set; }
          public string? PublicHolidayNotes { get; set; }

          // Ngày nghỉ
          public int? PaidLeaveDaysPerMonth { get; set; }
          public int? PaidLeaveDaysPerYear { get; set; }
          public int? SickLeaveDaysPerYear { get; set; }
          public bool FollowsCustomerSchedule { get; set; } = true;
          public bool WorkWhenCustomerClosed { get; set; } = true;
          public string? LeaveNotes { get; set; }

          // Cuối tuần
          public decimal? WeekendRate { get; set; }
          public decimal? SaturdayRate { get; set; }
          public decimal? SundayRate { get; set; }
          public bool SaturdayAsRegularWorkday { get; set; } = false;
          public string? WeekendNotes { get; set; }

          // Ca sự kiện & khẩn cấp
          public decimal? EventShiftRate { get; set; }
          public decimal? EmergencyCallRate { get; set; }
          public decimal? ReplacementShiftRate { get; set; }
          public decimal? EmergencyCallAllowance { get; set; }

          // Vi phạm giới hạn
          public string? OvertimeLimitViolationPolicy { get; set; }
          public decimal? OvertimeLimitViolationRate { get; set; }
          public string? UnapprovedOvertimePolicy { get; set; }
          public decimal? UnapprovedOvertimePenaltyRate { get; set; }

          // Phụ cấp
          public decimal? MealAllowancePerShift { get; set; }
          public decimal? TransportAllowancePerShift { get; set; }
          public decimal? PhoneAllowancePerMonth { get; set; }
          public decimal? UniformAllowance { get; set; }
          public decimal? SupervisorAllowance { get; set; }
          public decimal? HazardAllowance { get; set; }
          public string? AllowanceNotes { get; set; }

          // Điều kiện đặc biệt
          public string? SpecialRequirements { get; set; }
          public string? ScheduleExceptions { get; set; }
          public string? PenaltyTerms { get; set; }
          public string? BonusTerms { get; set; }
      }
      
    /// <summary>
    /// Model cho địa chỉ Việt Nam
    /// </summary>
    private class VietnameseAddress
    {
        public string HouseNumber { get; set; } = "";
        public string Street { get; set; } = "";
        public string? Ward { get; set; }
        public string District { get; set; } = "";
        public string City { get; set; } = "Ho Chi Minh City";
    }
}
