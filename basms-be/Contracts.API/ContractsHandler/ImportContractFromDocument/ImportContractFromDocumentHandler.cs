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

            // Parse ĐIỀU 3 toàn bộ
            var dieu3Info = ParseDieu3(rawText, startDate, endDate);
            
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
                    WorkOnPublicHolidays = dieu3Info.WorkOnPublicHolidays,
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
                var scheduleIds = new List<Guid>();
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

                    // 3.4: Tạo Shift Schedules từ ĐIỀU 3 cho location này
                    foreach (var shiftInfo in dieu3Info.ShiftSchedules)
                    {
                        var schedule = new Models.ContractShiftSchedule
                        {
                            Id = Guid.NewGuid(),
                            ContractId = contract.Id,
                            LocationId = location.Id, // Link tới location
                            ScheduleName = shiftInfo.ShiftName,
                            ScheduleType = "regular",
                            ShiftStartTime = shiftInfo.StartTime,
                            ShiftEndTime = shiftInfo.EndTime,
                            CrossesMidnight = shiftInfo.CrossesMidnight,
                            DurationHours = CalculateDuration(shiftInfo.StartTime, shiftInfo.EndTime),
                            BreakMinutes = 60, // Default
                            GuardsPerShift = guardsRequired,
                            RecurrenceType = "weekly",

                            // Áp dụng T2-T6 (weekdays)
                            AppliesMonday = true,
                            AppliesTuesday = true,
                            AppliesWednesday = true,
                            AppliesThursday = true,
                            AppliesFriday = true,

                            // Cuối tuần từ ĐIỀU 3.3
                            AppliesSaturday = dieu3Info.AppliesSaturday,
                            AppliesSunday = dieu3Info.AppliesSunday,
                            AppliesOnWeekends = dieu3Info.AppliesOnWeekends,

                            // Ngày lễ từ ĐIỀU 3.4
                            AppliesOnPublicHolidays = dieu3Info.WorkOnPublicHolidays,
                            AppliesOnCustomerHolidays = true,

                            SkipWhenLocationClosed = false, // Vẫn canh khi đóng cửa
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

                        logger.LogInformation(
                            "Shift schedule created: {ScheduleName} ({Start}-{End}) for Location {LocationId} - Sat={Sat}, Sun={Sun}, Weekend={Weekend}, Holiday={Holiday}",
                            schedule.ScheduleName, schedule.ShiftStartTime, schedule.ShiftEndTime, location.Id,
                            schedule.AppliesSaturday, schedule.AppliesSunday, schedule.AppliesOnWeekends, schedule.AppliesOnPublicHolidays);
                    }

                    if (!scheduleIds.Any())
                    {
                        warnings.Add("Không tìm thấy thông tin ca làm việc trong ĐIỀU 3 - chưa tạo shift schedules");
                    }
                }
                else
                {
                    warnings.Add("Không tìm thấy số lượng bảo vệ - chưa tạo location");
                }

                // ================================================================
                // 3.5: LƯU PUBLIC HOLIDAYS TỪ ĐIỀU 3.4
                // ================================================================
                foreach (var holidayInfo in dieu3Info.PublicHolidays)
                {
                    // Kiểm tra xem ngày lễ này đã tồn tại chưa
                    var existingHoliday = await connection.QueryFirstOrDefaultAsync<Models.PublicHoliday>(
                        "SELECT * FROM public_holidays WHERE HolidayDate = @Date AND Year = @Year LIMIT 1",
                        new { Date = holidayInfo.HolidayDate, Year = holidayInfo.Year },
                        transaction);

                    if (existingHoliday == null)
                    {
                        var holiday = new Models.PublicHoliday
                        {
                            Id = Guid.NewGuid(),
                            HolidayDate = holidayInfo.HolidayDate,
                            HolidayName = holidayInfo.HolidayName,
                            HolidayNameEn = holidayInfo.HolidayNameEn,
                            HolidayCategory = holidayInfo.HolidayCategory,
                            IsTetPeriod = holidayInfo.IsTetPeriod,
                            IsTetHoliday = holidayInfo.IsTetHoliday,
                            TetDayNumber = holidayInfo.TetDayNumber,
                            HolidayStartDate = holidayInfo.HolidayStartDate,
                            HolidayEndDate = holidayInfo.HolidayEndDate,
                            TotalHolidayDays = holidayInfo.TotalHolidayDays,
                            IsOfficialHoliday = true,
                            IsObserved = true,
                            AppliesNationwide = true,
                            StandardWorkplacesClosed = true,
                            EssentialServicesOperating = true, // Bảo vệ vẫn làm
                            Year = holidayInfo.Year,
                            CreatedAt = DateTime.UtcNow
                        };

                        await connection.InsertAsync(holiday, transaction);
                        logger.LogInformation("✓ Public holiday created: {Name} on {Date}",
                            holiday.HolidayName, holiday.HolidayDate.ToShortDateString());
                    }
                    else
                    {
                        logger.LogInformation("  Public holiday already exists: {Name} on {Date}",
                            existingHoliday.HolidayName, existingHoliday.HolidayDate.ToShortDateString());
                    }
                }

                // ================================================================
                // 3.6: LƯU SUBSTITUTE WORK DAYS TỪ ĐIỀU 3.4
                // ================================================================
                foreach (var subInfo in dieu3Info.SubstituteWorkDays)
                {
                    // Tìm holiday tương ứng
                    var relatedHoliday = await connection.QueryFirstOrDefaultAsync<Models.PublicHoliday>(
                        "SELECT * FROM public_holidays WHERE HolidayDate >= @SubDate - INTERVAL 7 DAY AND HolidayDate <= @SubDate + INTERVAL 7 DAY AND Year = @Year LIMIT 1",
                        new { SubDate = subInfo.SubstituteDate, Year = subInfo.Year },
                        transaction);

                    if (relatedHoliday != null)
                    {
                        var substituteDay = new Models.HolidaySubstituteWorkDay
                        {
                            Id = Guid.NewGuid(),
                            HolidayId = relatedHoliday.Id,
                            SubstituteDate = subInfo.SubstituteDate,
                            Reason = subInfo.Reason ?? $"Work on {subInfo.SubstituteDate.ToShortDateString()} to substitute for {relatedHoliday.HolidayName}",
                            Year = subInfo.Year,
                            CreatedAt = DateTime.UtcNow
                        };

                        await connection.InsertAsync(substituteDay, transaction);
                        logger.LogInformation("✓ Substitute work day created: {Date} for {Holiday}",
                            substituteDay.SubstituteDate.ToShortDateString(), relatedHoliday.HolidayName);
                    }
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
                    guardsRequired, scheduleIds.Count);

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
        // Tìm ĐIỀU 2 section
        var dieu2Match = Regex.Match(text, @"ĐIỀU\s*2[:\.\s]+(.*?)(?=ĐIỀU\s*3|$)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        string searchText = dieu2Match.Success ? dieu2Match.Value : text;

        if (dieu2Match.Success)
        {
            logger.LogInformation("📋 Found ĐIỀU 2 section ({Length} chars)", searchText.Length);
        }
        else
        {
            logger.LogWarning("⚠ ĐIỀU 2 not found, searching entire document");
        }

        // Tìm TẤT CẢ các dates trong section
        var allDates = new List<DateTime>();
        var datePattern = @"\b(\d{1,2})[\/\-](\d{1,2})[\/\-](\d{4})\b";
        var dateMatches = Regex.Matches(searchText, datePattern);

        foreach (Match match in dateMatches)
        {
            var dateStr = match.Value;
            if (DateTime.TryParseExact(dateStr, new[] { "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "d-M-yyyy" },
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var date))
            {
                allDates.Add(date);
                logger.LogInformation("  Found date: {Date}", date.ToString("dd/MM/yyyy"));
            }
        }

        // Logic: date đầu tiên là start, date cuối cùng là end
        DateTime? startDate = null, endDate = null;

        if (allDates.Count >= 2)
        {
            // Sắp xếp dates
            allDates.Sort();
            startDate = allDates.First();
            endDate = allDates.Last();

            logger.LogInformation("✓ Contract period: {Start} to {End} ({Days} days)",
                startDate.Value.ToString("dd/MM/yyyy"),
                endDate.Value.ToString("dd/MM/yyyy"),
                (endDate.Value - startDate.Value).Days);
        }
        else if (allDates.Count == 1)
        {
            logger.LogWarning("⚠ Only found 1 date: {Date}", allDates[0].ToString("dd/MM/yyyy"));
            startDate = allDates[0];
        }
        else
        {
            logger.LogWarning("⚠ No dates found in ĐIỀU 2");
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

            logger.LogWarning("No GPS found for: {Address}", address);
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

    /// <summary>
    /// Parse toàn bộ ĐIỀU 3: LỊCH LÀM VIỆC, CA TRỰC, NGÀY NGHỈ VÀ TĂNG CA
    /// </summary>
    private Dieu3ParsedInfo ParseDieu3(string fullText, DateTime? contractStartDate, DateTime? contractEndDate)
    {
        var info = new Dieu3ParsedInfo();

        // Tìm ĐIỀU 3
        var dieu3Section = ExtractSection(fullText, "ĐIỀU 3", "ĐIỀU 4");
        if (string.IsNullOrEmpty(dieu3Section))
        {
            logger.LogWarning("❌ Không tìm thấy ĐIỀU 3 trong hợp đồng");
            return info;
        }

        logger.LogInformation("✓ Found ĐIỀU 3 section ({Length} chars)", dieu3Section.Length);

        // 3.1 - Ca làm việc
        info.ShiftSchedules = ParseDieu3_1_ShiftSchedules(dieu3Section);
        logger.LogInformation("✓ Parsed {Count} shift schedules from section 3.1", info.ShiftSchedules.Count);

        // 3.3 - Ca trực cuối tuần
        ParseDieu3_3_WeekendWork(dieu3Section, info);
        logger.LogInformation("✓ Weekend: Sat={Sat}, Sun={Sun}, AppliesWeekend={Weekend}",
            info.AppliesSaturday, info.AppliesSunday, info.AppliesOnWeekends);

        // 3.4 - Ngày lễ Tết
        var startYear = contractStartDate?.Year ?? DateTime.Now.Year;
        var endYear = contractEndDate?.Year ?? startYear + 1;

        info.PublicHolidays = ParseDieu3_4_PublicHolidays(dieu3Section, startYear, endYear);
        info.SubstituteWorkDays = ParseDieu3_4_SubstituteWorkDays(dieu3Section, startYear, endYear);
        info.WorkOnPublicHolidays = CheckDieu3_4_WorkOnHolidays(dieu3Section);

        logger.LogInformation("✓ Holidays: {Count} public holidays, {SubCount} substitute days, WorkOnHolidays={Work}",
            info.PublicHolidays.Count, info.SubstituteWorkDays.Count, info.WorkOnPublicHolidays);

        return info;
    }

    /// <summary>
    /// Extract một section từ văn bản
    /// </summary>
    private string ExtractSection(string text, string startMarker, string? endMarker = null)
    {
        var startIndex = text.IndexOf(startMarker, StringComparison.OrdinalIgnoreCase);
        if (startIndex == -1)
            return string.Empty;

        if (!string.IsNullOrEmpty(endMarker))
        {
            var endIndex = text.IndexOf(endMarker, startIndex + startMarker.Length, StringComparison.OrdinalIgnoreCase);
            if (endIndex > startIndex)
            {
                return text.Substring(startIndex, endIndex - startIndex);
            }
        }

        // Nếu không có end marker, lấy 5000 ký tự
        return text.Substring(startIndex, Math.Min(5000, text.Length - startIndex));
    }

    /// <summary>
    /// Parse 3.1 - Ca làm việc
    /// </summary>
    private List<Dieu3ShiftSchedule> ParseDieu3_1_ShiftSchedules(string dieu3Text)
    {
        var shifts = new List<Dieu3ShiftSchedule>();

        // Pattern cho các ca: "• Ca sáng: 06h00 – 14h00"
        var patterns = new[]
        {
            // Pattern 1: "• Ca XXX: 06h00 – 14h00"
            @"[•\-]\s*Ca\s+(sáng|chiều|tối|đêm|khuya)\s*[:：]\s*(\d{1,2})h(\d{2})?\s*[–\-—]\s*(\d{1,2})h(\d{2})?(?:\s+ngày\s+hôm\s+sau)?",

            // Pattern 2: Không có bullet
            @"Ca\s+(sáng|chiều|tối|đêm|khuya)\s*[:：]\s*(\d{1,2})h(\d{2})?\s*[–\-—]\s*(\d{1,2})h(\d{2})?(?:\s+ngày\s+hôm\s+sau)?"
        };

        foreach (var pattern in patterns)
        {
            var matches = Regex.Matches(dieu3Text, pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);

            foreach (Match match in matches)
            {
                var shiftName = match.Groups[1].Value.Trim();
                var startHour = match.Groups[2].Value;
                var startMin = match.Groups[3].Success && !string.IsNullOrEmpty(match.Groups[3].Value) ? match.Groups[3].Value : "00";
                var endHour = match.Groups[4].Value;
                var endMin = match.Groups[5].Success && !string.IsNullOrEmpty(match.Groups[5].Value) ? match.Groups[5].Value : "00";

                if (TimeSpan.TryParse($"{startHour}:{startMin}", out var start) &&
                    TimeSpan.TryParse($"{endHour}:{endMin}", out var end))
                {
                    var normalizedName = NormalizeShiftName(shiftName);
                    var crossesMidnight = end < start;

                    shifts.Add(new Dieu3ShiftSchedule
                    {
                        ShiftName = $"Ca {normalizedName}",
                        StartTime = start,
                        EndTime = end,
                        CrossesMidnight = crossesMidnight
                    });

                    logger.LogInformation("  ✓ Shift: {Name} ({Start} - {End}) CrossMidnight={Cross}",
                        $"Ca {normalizedName}", start, end, crossesMidnight);
                }
            }
        }

        return shifts.DistinctBy(s => new { s.StartTime, s.EndTime }).ToList();
    }

    /// <summary>
    /// Parse 3.3 - Ca trực cuối tuần (Thứ Bảy và Chủ Nhật)
    /// Logic:
    /// - Nếu không có section 3.3 → KHÔNG làm cuối tuần (0, 0, 0)
    /// - Nếu có "duy trì/bố trí như ngày làm việc bình thường" → LÀM cuối tuần (1, 1, 1)
    /// - Nếu có "nghỉ riêng cho cuối tuần" → KHÔNG làm cuối tuần (0, 0, 0)
    /// - Nếu mention "Thứ 7" và/hoặc "Chủ nhật" → Tùy theo ngày (1/0, 0/1, 1)
    /// - Default nếu có section 3.3 → LÀM cuối tuần (1, 1, 1)
    /// </summary>
    private void ParseDieu3_3_WeekendWork(string dieu3Text, Dieu3ParsedInfo info)
    {
        // Tìm subsection 3.3
        var section33Match = Regex.Match(dieu3Text,
            @"3\.3\.?\s+[^\r\n]*(?:cuối\s*tuần|Thứ\s*Bảy|Chủ\s*Nhật)[^\r\n]*",
            RegexOptions.IgnoreCase);

        if (!section33Match.Success)
        {
            // Không có section 3.3 → mặc định KHÔNG làm cuối tuần
            info.AppliesSaturday = false;
            info.AppliesSunday = false;
            info.AppliesOnWeekends = false;
            logger.LogInformation("  ❌ No section 3.3 found → weekends OFF (0, 0, 0)");
            return;
        }

        // Lấy nội dung section 3.3 (từ 3.3 đến 3.4 hoặc 1200 ký tự)
        var section33Start = section33Match.Index;
        var section34Match = Regex.Match(dieu3Text, @"3\.4\.?", RegexOptions.IgnoreCase);
        var section33Length = section34Match.Success && section34Match.Index > section33Start
            ? section34Match.Index - section33Start
            : Math.Min(1200, dieu3Text.Length - section33Start);

        var section33 = dieu3Text.Substring(section33Start, section33Length);

        logger.LogInformation("  📄 Section 3.3 content ({Length} chars): {Preview}",
            section33.Length, section33.Length > 250 ? section33.Substring(0, 250) + "..." : section33);

        // ================================================================
        // KIỂM TRA CÁC PATTERN THEO THỨ TỰ ƯU TIÊN
        // ================================================================

        // 1. Kiểm tra "duy trì/bố trí như ngày làm việc bình thường" (LÀM VIỆC cuối tuần)
        var workNormalPatterns = new[]
        {
            @"(?:duy\s*trì|bố\s*trí).*?(?:lực\s*lượng|bảo\s*vệ).*?(?:như\s*ngày\s*làm\s*việc\s*bình\s*thường|bình\s*thường)",
            @"(?:duy\s*trì|bố\s*trí).*?(?:như\s*ngày\s*làm\s*việc\s*bình\s*thường|bình\s*thường)",
            @"làm\s*việc.*?(?:như\s*)?bình\s*thường.*?cuối\s*tuần"
        };

        foreach (var pattern in workNormalPatterns)
        {
            if (Regex.IsMatch(section33, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline))
            {
                info.AppliesSaturday = true;
                info.AppliesSunday = true;
                info.AppliesOnWeekends = true;
                logger.LogInformation("  ✅ Section 3.3: 'DUY TRÌ NHƯ BÌNH THƯỜNG' → weekends ON (1, 1, 1)");
                return;
            }
        }

        // 2. Kiểm tra "KHÔNG áp dụng chế độ nghỉ riêng cho cuối tuần" (LÀM VIỆC cuối tuần)
        if (Regex.IsMatch(section33,
            @"(?:Không|không)\s+áp\s+dụng\s+(?:chế\s+độ\s+)?nghỉ\s+riêng.*?cuối\s*tuần",
            RegexOptions.IgnoreCase | RegexOptions.Singleline))
        {
            info.AppliesSaturday = true;
            info.AppliesSunday = true;
            info.AppliesOnWeekends = true;
            logger.LogInformation("  ✅ Section 3.3: 'KHÔNG ÁP DỤNG NGHỈ RIÊNG' → weekends ON (1, 1, 1)");
            return;
        }

        // 3. Kiểm tra "nghỉ riêng cho cuối tuần" hoặc "không làm việc cuối tuần" (NGHỈ cuối tuần)
        var offPatterns = new[]
        {
            @"áp\s*dụng\s+(?:chế\s+độ\s+)?nghỉ\s+riêng.*?cuối\s*tuần",
            @"nghỉ.*?(?:vào|trong).*?cuối\s*tuần",
            @"không\s+làm\s+việc.*?cuối\s*tuần",
            @"cuối\s*tuần.*?được\s+nghỉ"
        };

        foreach (var pattern in offPatterns)
        {
            if (Regex.IsMatch(section33, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline))
            {
                info.AppliesSaturday = false;
                info.AppliesSunday = false;
                info.AppliesOnWeekends = false;
                logger.LogInformation("  ❌ Section 3.3: 'NGHỈ RIÊNG/KHÔNG LÀM VIỆC' → weekends OFF (0, 0, 0)");
                return;
            }
        }

        // 4. Kiểm tra mention cụ thể "Thứ 7" hoặc "Chủ nhật"
        var hasSaturday = Regex.IsMatch(section33, @"(?:thứ\s*(?:7|bảy)|saturday)(?!\s*và\s*chủ\s*nhật.*?nghỉ)", RegexOptions.IgnoreCase);
        var hasSunday = Regex.IsMatch(section33, @"(?:chủ\s*nhật|sunday)(?!\s*nghỉ)", RegexOptions.IgnoreCase);

        if (hasSaturday && hasSunday)
        {
            info.AppliesSaturday = true;
            info.AppliesSunday = true;
            info.AppliesOnWeekends = true;
            logger.LogInformation("  ✅ Section 3.3: Mentions 'THỨ 7 VÀ CHỦ NHẬT' → weekends ON (1, 1, 1)");
            return;
        }

        if (hasSaturday && !hasSunday)
        {
            info.AppliesSaturday = true;
            info.AppliesSunday = false;
            info.AppliesOnWeekends = true; // Vẫn = 1 vì có ít nhất 1 ngày cuối tuần
            logger.LogInformation("  ⚠ Section 3.3: Mentions 'THỨ 7' only → Saturday ON (1, 0, 1)");
            return;
        }

        if (!hasSaturday && hasSunday)
        {
            info.AppliesSaturday = false;
            info.AppliesSunday = true;
            info.AppliesOnWeekends = true; // Vẫn = 1 vì có ít nhất 1 ngày cuối tuần
            logger.LogInformation("  ⚠ Section 3.3: Mentions 'CHỦ NHẬT' only → Sunday ON (0, 1, 1)");
            return;
        }

        // 5. Default: Nếu có section 3.3 nhưng không match pattern nào → LÀM cuối tuần
        info.AppliesSaturday = true;
        info.AppliesSunday = true;
        info.AppliesOnWeekends = true;
        logger.LogInformation("  ⚠ Section 3.3: Ambiguous/default → weekends ON (1, 1, 1)");
    }

    /// <summary>
    /// Parse 3.4 - Ngày lễ, Tết
    /// </summary>
    private List<Dieu3PublicHoliday> ParseDieu3_4_PublicHolidays(string dieu3Text, int startYear, int endYear)
    {
        var holidays = new List<Dieu3PublicHoliday>();

        // Tìm section 3.4
        var section34Match = Regex.Match(dieu3Text, @"3\.4\.?\s+[^\r\n]*(?:Ngày\s*lễ|Tết)", RegexOptions.IgnoreCase);
        if (!section34Match.Success)
        {
            logger.LogWarning("  No section 3.4 found");
            return holidays;
        }

        var section34Start = section34Match.Index;
        var section34 = dieu3Text.Substring(section34Start, Math.Min(3000, dieu3Text.Length - section34Start));

        logger.LogInformation("📋 Section 3.4 content preview (first 500 chars):\n{Preview}",
            section34.Length > 500 ? section34.Substring(0, 500) : section34);

        // 1. Tết Nguyên Đán - Multiple patterns to handle various formats
        // Pattern 1: "Tết Nguyên đán 2025: Từ Thứ Tư, 29/01/2025 đến hết Thứ Ba, 04/02/2025"
        // Pattern 2: "Tết Nguyên Đán: 29/01/2025 - 04/02/2025"
        // Pattern 3: Without year number but has date range
        var tetPatterns = new[]
        {
            @"Tết\s+Nguy[eê]n\s+[ĐđDd][áaA]n\s+(\d{4})[:\s,]*.*?(\d{1,2}/\d{1,2}/\d{4}).*?(?:đến|[-–])\s*(?:hết\s+)?.*?(\d{1,2}/\d{1,2}/\d{4})",
            @"Tết\s+Nguy[eê]n\s+[ĐđDd][áaA]n[:\s,]*.*?(\d{1,2}/01/\d{4}|(\d{1,2}/02/\d{4})).*?(?:đến|[-–])\s*(?:hết\s+)?.*?(\d{1,2}/01/\d{4}|(\d{1,2}/02/\d{4}))",
            @"Tết\s+(?:âm\s+lịch|Nguy[eê]n\s+[ĐđDd][áaA]n)[:\s,]*.*?(\d{1,2}/\d{1,2}/\d{4}).*?(?:đến|[-–]).*?(\d{1,2}/\d{1,2}/\d{4})"
        };

        bool tetFound = false;
        foreach (var tetPattern in tetPatterns)
        {
            var tetMatch = Regex.Match(section34, tetPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (tetMatch.Success)
            {
                logger.LogInformation("  🎉 Tết pattern matched! Groups: {Count}", tetMatch.Groups.Count);
                for (int i = 0; i < tetMatch.Groups.Count; i++)
                {
                    logger.LogInformation("    Group[{Index}]: {Value}", i, tetMatch.Groups[i].Value);
                }

                // Extract dates from matched groups
                List<DateTime> tetDates = new List<DateTime>();
                for (int i = 1; i < tetMatch.Groups.Count; i++)
                {
                    if (!string.IsNullOrEmpty(tetMatch.Groups[i].Value) &&
                        DateTime.TryParse(tetMatch.Groups[i].Value, out var date))
                    {
                        tetDates.Add(date);
                    }
                }

                if (tetDates.Count >= 2)
                {
                    tetDates.Sort();
                    var tetStart = tetDates.First();
                    var tetEnd = tetDates.Last();
                    var year = tetStart.Year;

                    int totalDays = (tetEnd - tetStart).Days + 1;
                    int dayNumber = 1;
                    for (var date = tetStart.Date; date <= tetEnd.Date; date = date.AddDays(1))
                    {
                        holidays.Add(new Dieu3PublicHoliday
                        {
                            HolidayDate = date,
                            HolidayName = dayNumber == 1 ? "Tết Nguyên Đán" : $"Tết Nguyên Đán (Ngày {dayNumber})",
                            HolidayNameEn = $"Lunar New Year (Day {dayNumber})",
                            HolidayCategory = "tet",
                            IsTetPeriod = true,
                            IsTetHoliday = true,
                            TetDayNumber = dayNumber,
                            HolidayStartDate = tetStart,
                            HolidayEndDate = tetEnd,
                            TotalHolidayDays = totalDays,
                            Year = year
                        });
                        dayNumber++;
                    }
                    logger.LogInformation("  ✓ Tết {Year}: {Days} days ({Start} - {End})",
                        year, dayNumber - 1, tetStart.ToString("dd/MM/yyyy"), tetEnd.ToString("dd/MM/yyyy"));
                    tetFound = true;
                    break;
                }
            }
        }

        if (!tetFound)
        {
            logger.LogWarning("  ⚠ Tết Nguyên Đán not found in section 3.4");
        }

        // 2. Giỗ Tổ Hùng Vương
        var hungVuongPattern = @"Giỗ\s+Tổ\s+Hùng\s+Vương.*?(\d{1,2}/\d{1,2}/\d{4})";
        var hungVuongMatch = Regex.Match(section34, hungVuongPattern, RegexOptions.IgnoreCase);
        if (hungVuongMatch.Success && DateTime.TryParse(hungVuongMatch.Groups[1].Value, out var hungVuongDate))
        {
            holidays.Add(new Dieu3PublicHoliday
            {
                HolidayDate = hungVuongDate,
                HolidayName = "Giỗ Tổ Hùng Vương",
                HolidayNameEn = "Hung Kings' Festival",
                HolidayCategory = "national",
                Year = hungVuongDate.Year
            });
        }

        // 3. Ngày 30/4 và 1/5
        var liberationPattern = @"(?:30/4|Giải\s*phóng).*?(\d{1,2}/04/\d{4}).*?(?:01/5|1/5|Lao\s*động).*?(\d{1,2}/05/\d{4})";
        var liberationMatch = Regex.Match(section34, liberationPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (liberationMatch.Success)
        {
            if (DateTime.TryParse(liberationMatch.Groups[1].Value, out var day304))
            {
                holidays.Add(new Dieu3PublicHoliday
                {
                    HolidayDate = day304,
                    HolidayName = "Ngày Giải phóng miền Nam",
                    HolidayNameEn = "Reunification Day",
                    HolidayCategory = "national",
                    Year = day304.Year
                });
            }

            if (DateTime.TryParse(liberationMatch.Groups[2].Value, out var day015))
            {
                holidays.Add(new Dieu3PublicHoliday
                {
                    HolidayDate = day015,
                    HolidayName = "Ngày Quốc tế Lao động",
                    HolidayNameEn = "International Labor Day",
                    HolidayCategory = "national",
                    Year = day015.Year
                });
            }
        }

        // 4. Quốc khánh 2/9
        var nationalDayPattern = @"Quốc\s*khánh.*?(\d{1,2}/09/\d{4})";
        var nationalDayMatch = Regex.Match(section34, nationalDayPattern, RegexOptions.IgnoreCase);
        if (nationalDayMatch.Success && DateTime.TryParse(nationalDayMatch.Groups[1].Value, out var nationalDay))
        {
            holidays.Add(new Dieu3PublicHoliday
            {
                HolidayDate = nationalDay,
                HolidayName = "Ngày Quốc khánh",
                HolidayNameEn = "National Day",
                HolidayCategory = "national",
                Year = nationalDay.Year
            });
        }

        // 5. Tết Dương lịch
        var newYearPattern = @"Tết\s+Dương\s+lịch.*?(\d{1,2}/01/\d{4})";
        var newYearMatch = Regex.Match(section34, newYearPattern, RegexOptions.IgnoreCase);
        if (newYearMatch.Success && DateTime.TryParse(newYearMatch.Groups[1].Value, out var newYearDay))
        {
            holidays.Add(new Dieu3PublicHoliday
            {
                HolidayDate = newYearDay,
                HolidayName = "Tết Dương lịch",
                HolidayNameEn = "New Year's Day",
                HolidayCategory = "national",
                Year = newYearDay.Year
            });
        }

        return holidays;
    }

    /// <summary>
    /// Parse ngày làm bù từ 3.4
    /// </summary>
    private List<Dieu3SubstituteWorkDay> ParseDieu3_4_SubstituteWorkDays(string dieu3Text, int startYear, int endYear)
    {
        var substitutes = new List<Dieu3SubstituteWorkDay>();

        var section34Match = Regex.Match(dieu3Text, @"3\.4\.?\s+[^\r\n]*(?:Ngày\s*lễ|Tết)", RegexOptions.IgnoreCase);
        if (!section34Match.Success)
            return substitutes;

        var section34 = dieu3Text.Substring(section34Match.Index, Math.Min(3000, dieu3Text.Length - section34Match.Index));

        // Pattern: "nghỉ bù ngày 01/09/2025"
        var substitutePattern = @"nghỉ\s*bù\s*(?:ngày\s*)?(\d{1,2}/\d{1,2}/\d{4})";
        var matches = Regex.Matches(section34, substitutePattern, RegexOptions.IgnoreCase);

        foreach (Match match in matches)
        {
            if (DateTime.TryParse(match.Groups[1].Value, out var subDate))
            {
                substitutes.Add(new Dieu3SubstituteWorkDay
                {
                    SubstituteDate = subDate,
                    Reason = "Nghỉ bù theo quy định Nhà nước",
                    Year = subDate.Year
                });
            }
        }

        return substitutes;
    }

    /// <summary>
    /// Kiểm tra có làm việc vào ngày lễ không từ 3.4
    /// </summary>
    private bool CheckDieu3_4_WorkOnHolidays(string dieu3Text)
    {
        var section34Match = Regex.Match(dieu3Text, @"3\.4\.?\s+[^\r\n]*(?:Ngày\s*lễ|Tết)", RegexOptions.IgnoreCase);
        if (!section34Match.Success)
            return false;

        var section34 = dieu3Text.Substring(section34Match.Index, Math.Min(2000, dieu3Text.Length - section34Match.Index));

        // Kiểm tra "vẫn phải bố trí đủ nhân viên trực 24/24"
        var workPatterns = new[]
        {
            @"vẫn\s+phải\s+bố\s+trí.*?trực\s+24/24",
            @"Bên\s+A\s+vẫn\s+phải\s+bố\s+trí.*?nhân\s+viên",
            @"nhân\s+viên.*?vẫn\s+làm\s+việc\s+bình\s+thường"
        };

        foreach (var pattern in workPatterns)
        {
            if (Regex.IsMatch(section34, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline))
            {
                logger.LogInformation("  ✓ Work on public holidays: TRUE");
                return true;
            }
        }

        return false;
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

    // ============================================================================
    // HELPER CLASSES CHO ĐIỀU 3
    // ============================================================================

    /// <summary>
    /// Thông tin đã parse từ ĐIỀU 3
    /// </summary>
    private class Dieu3ParsedInfo
    {
        public List<Dieu3ShiftSchedule> ShiftSchedules { get; set; } = new();
        public bool AppliesSaturday { get; set; } = false;
        public bool AppliesSunday { get; set; } = false;
        public bool AppliesOnWeekends { get; set; } = false;
        public List<Dieu3PublicHoliday> PublicHolidays { get; set; } = new();
        public List<Dieu3SubstituteWorkDay> SubstituteWorkDays { get; set; } = new();
        public bool WorkOnPublicHolidays { get; set; } = false;
    }

    private class Dieu3ShiftSchedule
    {
        public string ShiftName { get; set; } = string.Empty;
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public bool CrossesMidnight { get; set; }
    }

    private class Dieu3PublicHoliday
    {
        public DateTime HolidayDate { get; set; }
        public string HolidayName { get; set; } = string.Empty;
        public string? HolidayNameEn { get; set; }
        public string HolidayCategory { get; set; } = "national";
        public bool IsTetPeriod { get; set; } = false;
        public bool IsTetHoliday { get; set; } = false;
        public int? TetDayNumber { get; set; }
        public DateTime? HolidayStartDate { get; set; }
        public DateTime? HolidayEndDate { get; set; }
        public int? TotalHolidayDays { get; set; }
        public int Year { get; set; }
    }

    private class Dieu3SubstituteWorkDay
    {
        public DateTime SubstituteDate { get; set; }
        public string? Reason { get; set; }
        public int Year { get; set; }
    }
}
