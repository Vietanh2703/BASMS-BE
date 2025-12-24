namespace Contracts.API.ContractsHandler.ImportContractFromDocument;

public record ImportContractFromDocumentCommand(
    Guid DocumentId
) : ICommand<ImportContractFromDocumentResult>;

public record ImportContractFromDocumentResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public Guid? ContractId { get; init; }
    public Guid? CustomerId { get; init; }
    public List<Guid> LocationIds { get; init; } = new();
    public List<Guid> ShiftScheduleIds { get; init; } = new();
    public string? ContractNumber { get; init; }
    public string? CustomerName { get; init; }
    public int LocationsCreated { get; init; }
    public int SchedulesCreated { get; init; }
    public string RawText { get; init; } = string.Empty;
    public List<string> Warnings { get; init; } = new();
    public int ConfidenceScore { get; init; }
}

internal class ImportContractFromDocumentHandler(
    IDbConnectionFactory connectionFactory,
    IS3Service s3Service,
    ILogger<ImportContractFromDocumentHandler> logger,
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
            logger.LogInformation("Importing contract from DocumentId: {DocumentId}", request.DocumentId);
            using var connection = await connectionFactory.CreateConnectionAsync();

            var document = await connection.QueryFirstOrDefaultAsync<ContractDocument>(
                "SELECT * FROM contract_documents WHERE Id = @Id AND IsDeleted = 0",
                new { Id = request.DocumentId });

            if (document == null)
                return new ImportContractFromDocumentResult
                {
                    Success = false,
                    ErrorMessage = $"Document with ID {request.DocumentId} not found"
                };

            logger.LogInformation("Found document: {DocumentName} at {FileUrl}", document.DocumentName,
                document.FileUrl);


            var (downloadSuccess, fileStream, downloadError) = await s3Service.DownloadFileAsync(
                document.FileUrl,
                cancellationToken);

            if (!downloadSuccess || fileStream == null)
                return new ImportContractFromDocumentResult
                {
                    Success = false,
                    ErrorMessage = downloadError ?? "Failed to download file from S3"
                };

            string rawText;
            var fileExtension = Path.GetExtension(document.DocumentName).ToLower();

            if (fileExtension == ".docx")
            {
                rawText = await ExtractTextFromWordAsync(fileStream);
            }
            else if (fileExtension == ".pdf")
            {
                rawText = await ExtractTextFromPdfAsync(fileStream);
            }
            else
            {
                fileStream.Dispose();
                return new ImportContractFromDocumentResult
                {
                    Success = false,
                    ErrorMessage = $"File type không được hỗ trợ: {fileExtension}. Chỉ hỗ trợ .docx và .pdf"
                };
            }

            fileStream.Dispose();

            if (string.IsNullOrWhiteSpace(rawText))
                return new ImportContractFromDocumentResult
                {
                    Success = false,
                    ErrorMessage = "Không thể đọc text từ file. File có thể bị lỗi hoặc rỗng."
                };

            logger.LogInformation("Extracted {Length} characters from document", rawText.Length);
            
            var contractNumber = ExtractContractNumber(rawText);
            var (startDate, endDate) = ExtractDates(rawText);
            var customerName = ExtractCustomerName(rawText);
            var customerAddress = ExtractAddress(rawText);
            var customerPhone = ExtractPhoneNumber(rawText);
            var customerEmail = ExtractEmail(rawText);
            var (contactPersonName, contactPersonTitle) = ExtractContactPersonInfo(rawText);
            var identityNumber = ExtractIdentityNumber(rawText);
            var guardsRequired = ExtractGuardsRequired(rawText);
            var coverageType = ExtractCoverageType(rawText);
            var dieu3Info = ParseDieu3(rawText, startDate, endDate);
            var (locationName, locationAddress) = ExtractLocationDetails(rawText);
            var (periodStartDate, periodEndDate, periodDuration) = ExtractContractPeriod(rawText);
            var contractTypeInfo = AnalyzeContractType(rawText, startDate, endDate);
            
            logger.LogInformation(
                "Parsed: Contract={Contract}, Customer={Customer}, Email={Email}, Phone={Phone}, Contact={Contact}, Title={Title}, CCCD={CCCD}, Type={Type}, Duration={Duration}",
                contractNumber, customerName, customerEmail, customerPhone, contactPersonName, contactPersonTitle,
                identityNumber, contractTypeInfo.ContractType, contractTypeInfo.DurationMonths);
            
            logger.LogInformation(
                "Searching for customer with extracted info - Email: {Email}, IdentityNumber: {IdentityNumber}, Phone: {Phone}",
                customerEmail, identityNumber, customerPhone);

            var customer = await FindCustomerAsync(connection, customerEmail, identityNumber, customerPhone);

            if (customer == null)
            {
                logger.LogError(
                    "CUSTOMER NOT FOUND! Cannot import contract. Extracted info - Email: {Email}, CCCD: {CCCD}, Phone: {Phone}",
                    customerEmail ?? "N/A", identityNumber ?? "N/A", customerPhone ?? "N/A");

                return new ImportContractFromDocumentResult
                {
                    Success = false,
                    ErrorMessage = $"Không tìm thấy khách hàng trong hệ thống với thông tin được trích xuất từ hợp đồng:\n" +
                                   $"- Email: {customerEmail ?? "Không tìm thấy"}\n" +
                                   $"- CCCD: {identityNumber ?? "Không tìm thấy"}\n" +
                                   $"- Số điện thoại: {customerPhone ?? "Không tìm thấy"}\n\n" +
                                   $"Vui lòng tạo khách hàng trước hoặc kiểm tra lại thông tin trong hợp đồng.",
                    RawText = rawText,
                    Warnings = warnings
                };
            }

            logger.LogInformation("Found existing customer: {CustomerId} - {CompanyName}", customer.Id, customer.CompanyName ?? customer.ContactPersonName);
            var customerId = customer.Id;

            // Validation
            if (string.IsNullOrEmpty(contractNumber))
            {
                warnings.Add("Không tìm thấy số hợp đồng - sẽ tự động generate");
                contractNumber = $"CTR-{DateTime.Now:yyyyMMdd}-{Guid.NewGuid().ToString().Substring(0, 4).ToUpper()}";
            }

            if (string.IsNullOrEmpty(customerName))
                return new ImportContractFromDocumentResult
                {
                    Success = false,
                    ErrorMessage = "Không tìm thấy tên khách hàng trong file. Vui lòng kiểm tra lại.",
                    RawText = rawText,
                    Warnings = warnings
                };

            if (!startDate.HasValue || !endDate.HasValue)
            {
                warnings.Add("Không tìm thấy ngày bắt đầu/kết thúc - sử dụng giá trị mặc định");
                startDate ??= DateTime.Now.Date;
                endDate ??= startDate.Value.AddMonths(12);
            }

            if (string.IsNullOrEmpty(contactPersonName))
                warnings.Add("Không tìm thấy tên người đại diện - sẽ sử dụng giá trị mặc định");

            if (string.IsNullOrEmpty(contactPersonTitle))
                warnings.Add("Không tìm thấy chức vụ người đại diện - sẽ sử dụng giá trị mặc định");
            
            using var transaction = connection.BeginTransaction();

            try
            {
                var updated = false;
                
                if (string.IsNullOrEmpty(customer.Address) && !string.IsNullOrEmpty(customerAddress))
                {
                    customer.Address = customerAddress;
                    updated = true;
                }

                if (string.IsNullOrEmpty(customer.Phone) && !string.IsNullOrEmpty(customerPhone))
                {
                    customer.Phone = customerPhone;
                    updated = true;
                }

                if (string.IsNullOrEmpty(customer.ContactPersonName) && !string.IsNullOrEmpty(contactPersonName))
                {
                    customer.ContactPersonName = contactPersonName;
                    updated = true;
                }

                if (string.IsNullOrEmpty(customer.ContactPersonTitle) && !string.IsNullOrEmpty(contactPersonTitle))
                {
                    customer.ContactPersonTitle = contactPersonTitle;
                    updated = true;
                }

                if (updated)
                {
                    customer.UpdatedAt = DateTime.UtcNow;
                    await connection.UpdateAsync(customer, transaction);
                    logger.LogInformation("Updated customer information for CustomerId: {CustomerId}", customerId);
                }

                logger.LogInformation(
                    "Using existing customer: {CustomerId} - {CompanyName} with contact: {ContactName} - {ContactTitle}",
                    customerId, customer.CompanyName, customer.ContactPersonName, customer.ContactPersonTitle);
                
                var durationMonths = (endDate.Value.Year - startDate.Value.Year) * 12 +
                    endDate.Value.Month - startDate.Value.Month;

                var contract = new Contract
                {
                    Id = Guid.NewGuid(),
                    DocumentId = request.DocumentId, 
                    ContractNumber = contractNumber,
                    ContractTitle = $"Hợp đồng bảo vệ - {customerName}",
                    CustomerId = customerId,
                    ContractType = contractTypeInfo.ContractType,
                    ServiceScope = contractTypeInfo.ServiceScope,
                    CoverageModel = "fixed_schedule",
                    StartDate = startDate.Value,
                    EndDate = endDate.Value,
                    DurationMonths = contractTypeInfo.DurationMonths,
                    Status = "draft", 
                    ContractFileUrl = document.FileUrl, 
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
                    CreatedBy = document.UploadedBy 
                };

                await connection.InsertAsync(contract, transaction);
                logger.LogInformation(
                    "Contract created: {ContractId} - {ContractNumber} (Type: {Type}, Duration: {Duration} months)",
                    contract.Id, contract.ContractNumber, contract.ContractType, contract.DurationMonths);


                await CreateOrUpdateContractPeriodAsync(
                    connection,
                    transaction,
                    contract.Id,
                    periodStartDate ?? startDate,
                    periodEndDate ?? endDate,
                    periodDuration);


                var locationIds = new List<Guid>();
                var scheduleIds = new List<Guid>();
                if (guardsRequired > 0)
                {
                    var finalLocationAddress = locationAddress ?? customerAddress ?? "";
                    var finalLocationName = locationName ?? $"Địa điểm mặc định - {customerName}";
                    decimal? latitude = null;
                    decimal? longitude = null;

                    if (!string.IsNullOrWhiteSpace(finalLocationAddress))
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
                            logger.LogWarning(gpsEx, "Failed to get GPS coordinates for address: {Address}",
                                finalLocationAddress);
                            warnings.Add($"Lỗi khi lấy tọa độ GPS: {gpsEx.Message}");
                        }

                    var location = new CustomerLocation
                    {
                        Id = Guid.NewGuid(),
                        CustomerId = customerId,
                        LocationCode = $"LOC-{DateTime.Now:yyyyMMdd}-001",
                        LocationName = finalLocationName,
                        LocationType = "office",
                        Address = finalLocationAddress,
                        Latitude = latitude,
                        Longitude = longitude,
                        GeofenceRadiusMeters = 100, 
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
                    
                    var contractLocation = new ContractLocation
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
                    
                    foreach (var shiftInfo in dieu3Info.ShiftSchedules)
                    {
                        var schedule = new ContractShiftSchedule
                        {
                            Id = Guid.NewGuid(),
                            ContractId = contract.Id,
                            LocationId = location.Id, 
                            ScheduleName = shiftInfo.ShiftName,
                            ScheduleType = "regular",
                            ShiftStartTime = shiftInfo.StartTime,
                            ShiftEndTime = shiftInfo.EndTime,
                            CrossesMidnight = shiftInfo.CrossesMidnight,
                            DurationHours = CalculateDuration(shiftInfo.StartTime, shiftInfo.EndTime),
                            BreakMinutes = 60, 
                            GuardsPerShift = guardsRequired,
                            RecurrenceType = "weekly",
                            AppliesMonday = true,
                            AppliesTuesday = true,
                            AppliesWednesday = true,
                            AppliesThursday = true,
                            AppliesFriday = true,
                            AppliesSaturday = dieu3Info.AppliesSaturday,
                            AppliesSunday = dieu3Info.AppliesSunday,
                            AppliesOnWeekends = dieu3Info.AppliesOnWeekends,
                            AppliesOnPublicHolidays = dieu3Info.WorkOnPublicHolidays,
                            AppliesOnCustomerHolidays = true,
                            SkipWhenLocationClosed = false,
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
                            CreatedBy = document.UploadedBy
                        };

                        await connection.InsertAsync(schedule, transaction);
                        scheduleIds.Add(schedule.Id);

                        logger.LogInformation(
                            "Shift schedule created: {ScheduleName} ({Start}-{End}) for Location {LocationId} - Sat={Sat}, Sun={Sun}, Weekend={Weekend}, Holiday={Holiday}",
                            schedule.ScheduleName, schedule.ShiftStartTime, schedule.ShiftEndTime, location.Id,
                            schedule.AppliesSaturday, schedule.AppliesSunday, schedule.AppliesOnWeekends,
                            schedule.AppliesOnPublicHolidays);
                    }

                    if (!scheduleIds.Any())
                        warnings.Add("Không tìm thấy thông tin ca làm việc trong ĐIỀU 3 - chưa tạo shift schedules");
                }
                else
                {
                    warnings.Add("Không tìm thấy số lượng bảo vệ - chưa tạo location");
                }
                
                foreach (var holidayInfo in dieu3Info.PublicHolidays)
                {
                    var existingHoliday = await connection.QueryFirstOrDefaultAsync<PublicHoliday>(
                        "SELECT * FROM public_holidays WHERE HolidayDate = @Date AND Year = @Year LIMIT 1",
                        new { Date = holidayInfo.HolidayDate, holidayInfo.Year },
                        transaction);

                    if (existingHoliday == null)
                    {
                        var holiday = new PublicHoliday
                        {
                            Id = Guid.NewGuid(),
                            ContractId = contract.Id,
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
                            EssentialServicesOperating = true,
                            Year = holidayInfo.Year,
                            CreatedAt = DateTime.UtcNow
                        };

                        await connection.InsertAsync(holiday, transaction);
                        logger.LogInformation("Public holiday created: {Name} on {Date} for Contract {ContractId}",
                            holiday.HolidayName, holiday.HolidayDate.ToShortDateString(), contract.Id);
                    }
                    else
                    {
                        logger.LogInformation("Public holiday already exists: {Name} on {Date}",
                            existingHoliday.HolidayName, existingHoliday.HolidayDate.ToShortDateString());
                    }
                }
                
                foreach (var subInfo in dieu3Info.SubstituteWorkDays)
                {
                    var relatedHoliday = await connection.QueryFirstOrDefaultAsync<PublicHoliday>(
                        "SELECT * FROM public_holidays WHERE HolidayDate >= @SubDate - INTERVAL 7 DAY AND HolidayDate <= @SubDate + INTERVAL 7 DAY AND Year = @Year LIMIT 1",
                        new { SubDate = subInfo.SubstituteDate, subInfo.Year },
                        transaction);

                    if (relatedHoliday != null)
                    {
                        var substituteDay = new HolidaySubstituteWorkDay
                        {
                            Id = Guid.NewGuid(),
                            HolidayId = relatedHoliday.Id,
                            SubstituteDate = subInfo.SubstituteDate,
                            Reason = subInfo.Reason ??
                                     $"Work on {subInfo.SubstituteDate.ToShortDateString()} to substitute for {relatedHoliday.HolidayName}",
                            Year = subInfo.Year,
                            CreatedAt = DateTime.UtcNow
                        };

                        await connection.InsertAsync(substituteDay, transaction);
                        logger.LogInformation("Substitute work day created: {Date} for {Holiday}",
                            substituteDay.SubstituteDate.ToShortDateString(), relatedHoliday.HolidayName);
                    }
                }
                
                transaction.Commit();

                logger.LogInformation("Transaction committed successfully");
                var score = CalculateConfidenceScore(
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
                    "Contract import completed: {ContractNumber} - {Locations} locations, {Schedules} schedules for Customer: {CustomerId}",
                    contractNumber, locationIds.Count, scheduleIds.Count, customerId);

                try
                {
                    var tempDocuments = await connection.QueryAsync<ContractDocument>(
                        @"SELECT * FROM contract_documents
                          WHERE DocumentType IN ('filled_contract', 'signed_contract')
                          AND IsDeleted = 0
                          AND CreatedAt >= DATE_SUB(NOW(), INTERVAL 1 DAY)
                          ORDER BY CreatedAt DESC",
                        transaction: transaction);

                    var docsToDelete = tempDocuments.ToList();

                    logger.LogInformation("Found {Count} temporary documents to cleanup", docsToDelete.Count);

                    foreach (var tempDoc in docsToDelete)
                    {
                        logger.LogInformation("Deleting temporary file from S3: {FileUrl}", tempDoc.FileUrl);
                        var deleteSuccess = await s3Service.DeleteFileAsync(tempDoc.FileUrl, cancellationToken);

                        if (deleteSuccess)
                        {
                            await connection.ExecuteAsync(
                                "DELETE FROM contract_documents WHERE Id = @Id",
                                new { tempDoc.Id },
                                transaction);

                            logger.LogInformation(
                                "Deleted temporary {Type} document: {Id} - {Name}",
                                tempDoc.DocumentType, tempDoc.Id, tempDoc.DocumentName);
                        }
                        else
                        {
                            logger.LogWarning(
                                "Failed to delete temporary file from S3, keeping database record: {FileUrl}",
                                tempDoc.FileUrl);
                        }
                    }
                }
                catch (Exception cleanupEx)
                {
                    logger.LogWarning(cleanupEx, "Failed to cleanup temporary files, but import was successful");
                    warnings.Add("Không thể xóa file tạm thời (filled/signed), cần xóa thủ công");
                }

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
                if (!string.IsNullOrWhiteSpace(paragraphText)) text.AppendLine(paragraphText);
            }

            foreach (var table in body.Descendants<Table>())
            foreach (var row in table.Descendants<TableRow>())
            {
                var rowText = string.Join(" | ",
                    row.Descendants<TableCell>().Select(c => c.InnerText.Trim()));
                if (!string.IsNullOrWhiteSpace(rowText)) text.AppendLine(rowText);
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
                for (var page = 1; page <= reader.NumberOfPages; page++)
                    try
                    {
                        var contentBytes = reader.GetPageContent(page);
                        if (contentBytes != null && contentBytes.Length > 0)
                        {
                            var pageContent = Encoding.UTF8.GetString(contentBytes);
                            var matches = Regex.Matches(pageContent, @"BT\s+(.*?)\s+ET", RegexOptions.Singleline);
                            foreach (Match match in matches)
                            {
                                var textBlock = match.Groups[1].Value;
                                var textMatches = Regex.Matches(textBlock, @"\((.*?)\)\s*Tj|\[(.*?)\]\s*TJ");
                                foreach (Match textMatch in textMatches)
                                {
                                    var extractedText = textMatch.Groups[1].Success
                                        ? textMatch.Groups[1].Value
                                        : textMatch.Groups[2].Value;
                                    if (!string.IsNullOrWhiteSpace(extractedText)) text.Append(extractedText + " ");
                                }
                            }

                            text.AppendLine();
                        }
                    }
                    catch (Exception pageEx)
                    {
                        logger.LogWarning(pageEx, "Could not extract text from page {Page}", page);
                    }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error extracting text from PDF");
            throw new InvalidOperationException(
                "Không thể đọc file PDF. Vui lòng kiểm tra file có bị mã hóa hoặc hỏng.", ex);
        }

        return await Task.FromResult(text.ToString());
    }
    
    private ContractTypeInfo AnalyzeContractType(string text, DateTime? startDate, DateTime? endDate)
    {
        var info = new ContractTypeInfo();
        
        if (startDate.HasValue && endDate.HasValue)
        {
            var totalDays = (endDate.Value - startDate.Value).Days;
            info.DurationMonths = (endDate.Value.Year - startDate.Value.Year) * 12 +
                endDate.Value.Month - startDate.Value.Month;
            info.TotalDays = totalDays;
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
            info.ContractType = "long_term";
            info.ServiceScope = "shift_based";
            info.DurationMonths = 12;
            info.AutoGenerateShifts = true;
            info.GenerateShiftsAdvanceDays = 30;
            info.IsRenewable = true;
            info.AutoRenewal = false;
        }
        
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
        
        if (Regex.IsMatch(lowerText, @"tự\s*động\s*gia\s*hạn", RegexOptions.IgnoreCase)) info.AutoRenewal = true;
        
        if (Regex.IsMatch(lowerText, @"sự\s*kiện|event|buổi|occasion", RegexOptions.IgnoreCase))
            info.ServiceScope = "event_based";

        return info;
    }

    private string? ExtractContractNumber(string text)
    {
        var patterns = new[]
        {
            @"(\d{8}/CTANCH(?:/HDDVBV)?)",
            
            @"(?:Số\s*HĐ|Hợp\s*đồng\s*số|Contract\s*No\.?)\s*[:：]?\s*(\d{3,4}/\d{4}/[A-Z\-]+/[A-Z]+/[A-Z]+)",
            
            @"(\d{3,4}/\d{4}/HĐDV-BV/[A-Z]+/[A-Z]+)",
            
            @"(?:Số\s*HĐ|Hợp\s*đồng\s*số|Contract\s*No\.?)\s*[:：]\s*([A-Z0-9\-/]+)"
        };


        foreach (var pattern in patterns)
        {
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (!match.Success)
                continue;
            
            if (match.Groups.Count > 2 && !string.IsNullOrEmpty(match.Groups[2].Value))
                return $"{match.Groups[1].Value}-{match.Groups[2].Value}".Trim();

            var value = match.Groups[1].Value.Trim();

            if (Regex.IsMatch(value, @"^\d{8}/CTANCH$", RegexOptions.IgnoreCase)) value += "/HDDVBV";

            return value;
        }

        return string.Empty;
    }


    private (DateTime? startDate, DateTime? endDate) ExtractDates(string text)
    {
        var dieu2Match = Regex.Match(text, @"ĐIỀU\s*2[:\.\s]+(.*?)(?=ĐIỀU\s*3|$)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        var searchText = dieu2Match.Success ? dieu2Match.Value : text;
        if (dieu2Match.Success)
            logger.LogInformation("Found ĐIỀU 2 section ({Length} chars)", searchText.Length);
        else
            logger.LogWarning("ĐIỀU 2 not found, searching entire document");
        
        var allDates = new List<DateTime>();
        var datePattern = @"\b(\d{1,2})[\/\-](\d{1,2})[\/\-](\d{4})\b";
        var dateMatches = Regex.Matches(searchText, datePattern);

        foreach (Match match in dateMatches)
        {
            var dateStr = match.Value;
            if (DateTime.TryParseExact(dateStr, new[] { "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "d-M-yyyy" },
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var date))
            {
                allDates.Add(date);
                logger.LogInformation("  Found date: {Date}", date.ToString("dd/MM/yyyy"));
            }
        }
        
        DateTime? startDate = null, endDate = null;

        if (allDates.Count >= 2)
        {
            allDates.Sort();
            startDate = allDates.First();
            endDate = allDates.Last();

            logger.LogInformation("Contract period: {Start} to {End} ({Days} days)",
                startDate.Value.ToString("dd/MM/yyyy"),
                endDate.Value.ToString("dd/MM/yyyy"),
                (endDate.Value - startDate.Value).Days);
        }
        else if (allDates.Count == 1)
        {
            logger.LogWarning("Only found 1 date: {Date}", allDates[0].ToString("dd/MM/yyyy"));
            startDate = allDates[0];
        }
        else
        {
            logger.LogWarning("No dates found in ĐIỀU 2");
        }

        return (startDate, endDate);
    }

    private (DateTime? startDate, DateTime? endDate, string? duration) ExtractContractPeriod(string text)
    {
        var dieu2Index = text.IndexOf("ĐIỀU 2", StringComparison.OrdinalIgnoreCase);
        if (dieu2Index == -1)
            dieu2Index = text.IndexOf("Điều 2", StringComparison.OrdinalIgnoreCase);

        var searchText = text;
        if (dieu2Index >= 0)
        {
            searchText = text.Substring(dieu2Index, Math.Min(1000, text.Length - dieu2Index));
            logger.LogInformation("📋 Found ĐIỀU 2 section for contract period extraction");
        }

        DateTime? startDate = null, endDate = null;
        string? duration = null;
        var datePatterns = new[]
        {
            @"(?:có\s+hiệu\s+lực\s+)?từ\s+ngày\s+(\d{1,2}[\/\-]\d{1,2}[\/\-]\d{4})\s+đến\s+(?:hết\s+)?ngày\s+(\d{1,2}[\/\-]\d{1,2}[\/\-]\d{4})",
            
            @"(?:Từ|từ)\s+ngày\s+(\d{1,2}[\/\-]\d{1,2}[\/\-]\d{4})\s+đến\s+ngày\s+(\d{1,2}[\/\-]\d{1,2}[\/\-]\d{4})",
            
            @"(?:Bắt\s+đầu\s+từ|bắt\s+đầu\s+từ)\s+ngày\s+(\d{1,2}[\/\-]\d{1,2}[\/\-]\d{4})\s+đến\s+(?:ngày\s+)?(\d{1,2}[\/\-]\d{1,2}[\/\-]\d{4})",
            
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
                    logger.LogInformation("Extracted period dates: {Start} to {End}", startDate, endDate);
                    break; 
                }
            }
        }
        
        var durationPattern = @"(?:thời\s*hạn|hiệu\s*lực|thời\s*gian)[:\s]*(\d+)\s*(tháng|năm|ngày)";
        var durationMatch = Regex.Match(searchText, durationPattern, RegexOptions.IgnoreCase);

        if (durationMatch.Success)
        {
            duration = $"{durationMatch.Groups[1].Value} {durationMatch.Groups[2].Value}";
            logger.LogInformation("Extracted duration: {Duration}", duration);
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
        var benBIndex = text.IndexOf("BÊN B", StringComparison.OrdinalIgnoreCase);
        if (benBIndex == -1)
            benBIndex = text.IndexOf("Bên B", StringComparison.OrdinalIgnoreCase);

        if (benBIndex >= 0)
        {
            var textAfterBenB = text.Substring(benBIndex, Math.Min(600, text.Length - benBIndex));

            var pattern = @"(?:Địa\s*chỉ|Address).*?[:：]\s*([^\r\n]+)";
            var match = Regex.Match(textAfterBenB, pattern, RegexOptions.IgnoreCase);

            if (match.Success) return match.Groups[1].Value.Trim();
        }
        
        var fallbackPattern = @"(?:Địa\s*chỉ|Address).*?[:：]\s*([^\r\n]+)";
        var fallbackMatch = Regex.Match(text, fallbackPattern, RegexOptions.IgnoreCase);
        return fallbackMatch.Success ? fallbackMatch.Groups[1].Value.Trim() : null;
    }

    private string? ExtractPhoneNumber(string text)
    {
        var benBIndex = text.IndexOf("BÊN B", StringComparison.OrdinalIgnoreCase);
        if (benBIndex == -1)
            benBIndex = text.IndexOf("Bên B", StringComparison.OrdinalIgnoreCase);

        if (benBIndex >= 0)
        {
            var textAfterBenB = text.Substring(benBIndex, Math.Min(500, text.Length - benBIndex));

            var pattern = @"(?:Điện\s*thoại|Phone|ĐT).*?[:：]\s*([\d\s\-\(\)\+]{9,20})";
            var match = Regex.Match(textAfterBenB, pattern, RegexOptions.IgnoreCase);

            if (match.Success)
            {
                var phone = Regex.Replace(match.Groups[1].Value, @"[^\d\+]", "");
                if (phone.StartsWith("0"))
                    phone = "+84" + phone.Substring(1);
                else if (!phone.StartsWith("+"))
                    phone = "+84" + phone;

                return phone;
            }
        }

        return null;
    }

    private string? ExtractEmail(string text)
    {
        var benBPattern = @"(?:BÊN\s*B|Bên\s*B)[\s\S]*?Email\s*[:：]\s*([a-zA-Z0-9._-]+@[a-zA-Z0-9._-]+\.[a-zA-Z]{2,})";
        var benBMatch = Regex.Match(text, benBPattern, RegexOptions.IgnoreCase);

        if (benBMatch.Success) return benBMatch.Groups[1].Value.Trim();
        var benBIndex = text.IndexOf("BÊN B", StringComparison.OrdinalIgnoreCase);
        if (benBIndex == -1)
            benBIndex = text.IndexOf("Bên B", StringComparison.OrdinalIgnoreCase);
        if (benBIndex >= 0)
        {
            var textAfterBenB = text.Substring(benBIndex);
            var emailPattern = @"([a-zA-Z0-9._-]+@[a-zA-Z0-9._-]+\.[a-zA-Z]{2,})";
            var emailMatch = Regex.Match(textAfterBenB, emailPattern);
            if (emailMatch.Success) return emailMatch.Groups[1].Value;
        }

        return null;
    }

    private (string? name, string? title) ExtractContactPersonInfo(string text)
    {
        var benBIndex = text.IndexOf("BÊN B", StringComparison.OrdinalIgnoreCase);
        if (benBIndex == -1)
            benBIndex = text.IndexOf("Bên B", StringComparison.OrdinalIgnoreCase);

        if (benBIndex < 0)
        {
            logger.LogWarning("Could not find 'Bên B' section in document");
            return (null, null);
        }

        var textAfterBenB = text.Substring(benBIndex, Math.Min(1000, text.Length - benBIndex));
        var pattern1 = @"(?:Đại\s*diện|Đ/D|Người\s*đại\s*diện)\s*[:：]?\s*" +
                       @"(?<gender>Ông|Bà)\s+" +
                       @"(?<name>[A-ZÀÁẢÃẠĂẮẰẲẴẶÂẤẦẨẪẬÉÈẺẼẸÊẾỀỂỄỆÍÌỈĨỊÓÒỎÕỌÔỐỒỔỖỘƠỚỜỞỠỢÚÙỦŨỤƯỨỪỬỮỰÝỲỶỸỴĐ]" +
                       @"[a-zàáảãạăắằẳẵặâấầẩẫậéèẻẽẹêếềểễệíìỉĩịóòỏõọôốồổỗộơớờởỡợúùủũụưứừửữựýỳỷỹỵđ]+" +
                       @"(?:\s+[A-ZÀÁẢÃẠĂẮẰẲẴẶÂẤẦẨẪẬÉÈẺẼẸÊẾỀỂỄỆÍÌỈĨỊÓÒỎÕỌÔỐỒỔỖỘƠỚỜỞỠỢÚÙỦŨỤƯỨỪỬỮỰÝỲỶỸỴĐ]" +
                       @"[a-zàáảãạăắằẳẵặâấầẩẫậéèẻẽẹêếềểễệíìỉĩịóòỏõọôốồổỗộơớờởỡợúùủũụưứừửữựýỳỷỹỵđ]+)*)" +
                       @"\s*[-–—]\s*" +
                       @"(?<title>[A-ZÀÁẢÃẠĂẮẰẲẴẶÂẤẦẨẪẬÉÈẺẼẸÊẾỀỂỄỆÍÌỈĨỊÓÒỎÕỌÔỐỒỔỖỘƠỚỜỞỠỢÚÙỦŨỤƯỨỪỬỮỰÝỲỶỸỴĐ]" +
                       @"[a-zàáảãạăắằẳẵặâấầẩẫậéèẻẽẹêếềểễệíìỉĩịóòỏõọôốồổỗộơớờởỡợúùủũụưứừửữựýỳỷỹỵđ]+" +
                       @"(?:\s+(?!Số\s|CCCD|CMND|Điện\s*thoại|Email|Địa\s*chỉ|Căn\s*cước)[a-zàáảãạăắằẳẵặâấầẩẫậéèẻẽẹêếềểễệíìỉĩịóòỏõọôốồổỗộơớờởỡợúùủũụưứừửữựýỳỷỹỵđ]+)*)";

        var match1 = Regex.Match(textAfterBenB, pattern1, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (match1.Success)
        {
            var name = match1.Groups["name"].Value.Trim();
            var title = match1.Groups["title"].Value.Trim();

            logger.LogInformation("✓ Extracted from Pattern 1: Name='{Name}', Title='{Title}'", name, title);
            return (name, title);
        }
        
        var pattern2 = @"(?<gender>Ông|Bà)\s+" +
                       @"(?<name>[A-ZÀÁẢÃẠĂẮẰẲẴẶÂẤẦẨẪẬÉÈẺẼẸÊẾỀỂỄỆÍÌỈĨỊÓÒỎÕỌÔỐỒỔỖỘƠỚỜỞỠỢÚÙỦŨỤƯỨỪỬỮỰÝỲỶỸỴĐ]" +
                       @"[a-zàáảãạăắằẳẵặâấầẩẫậéèẻẽẹêếềểễệíìỉĩịóòỏõọôốồổỗộơớờởỡợúùủũụưứừửữựýỳỷỹỵđ]+" +
                       @"(?:\s+[A-ZÀÁẢÃẠĂẮẰẲẴẶÂẤẦẨẪẬÉÈẺẼẸÊẾỀỂỄỆÍÌỈĨỊÓÒỎÕỌÔỐỒỔỖỘƠỚỜỞỠỢÚÙỦŨỤƯỨỪỬỮỰÝỲỶỸỴĐ]" +
                       @"[a-zàáảãạăắằẳẵặâấầẩẫậéèẻẽẹêếềểễệíìỉĩịóòỏõọôốồổỗộơớờởỡợúùủũụưứừửữựýỳỷỹỵđ]+)*)" +
                       @"\s*[-–—]\s*" +
                       @"(?<title>[A-ZÀÁẢÃẠĂẮẰẲẴẶÂẤẦẨẪẬÉÈẺẼẸÊẾỀỂỄỆÍÌỈĨỊÓÒỎÕỌÔỐỒỔỖỘƠỚỜỞỠỢÚÙỦŨỤƯỨỪỬỮỰÝỲỶỸỴĐ]" +
                       @"[a-zàáảãạăắằẳẵặâấầẩẫậéèẻẽẹêếềểễệíìỉĩịóòỏõọôốồổỗộơớờởỡợúùủũụưứừửữựýỳỷỹỵđ]+" +
                       @"(?:\s+(?!Số\s|CCCD|CMND|Điện\s*thoại|Email|Địa\s*chỉ|Căn\s*cước)[a-zàáảãạăắằẳẵặâấầẩẫậéèẻẽẹêếềểễệíìỉĩịóòỏõọôốồổỗộơớờởỡợúùủũụưứừửữựýỳỷỹỵđ]+)*)";

        var match2 = Regex.Match(textAfterBenB, pattern2, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (match2.Success)
        {
            var name = match2.Groups["name"].Value.Trim();
            var title = match2.Groups["title"].Value.Trim();

            logger.LogInformation("Extracted from Pattern 2: Name='{Name}', Title='{Title}'", name, title);
            return (name, title);
        }
        
        var pattern3 = @"(?:Đại\s*diện|Đ/D|Người\s*đại\s*diện)\s*[:：]?\s*" +
                       @"(?<gender>Ông|Bà)\s+" +
                       @"(?<name>[A-ZÀÁẢÃẠĂẮẰẲẴẶÂẤẦẨẪẬÉÈẺẼẸÊẾỀỂỄỆÍÌỈĨỊÓÒỎÕỌÔỐỒỔỖỘƠỚỜỞỠỢÚÙỦŨỤƯỨỪỬỮỰÝỲỶỸỴĐ]" +
                       @"[a-zàáảãạăắằẳẵặâấầẩẫậéèẻẽẹêếềểễệíìỉĩịóòỏõọôốồổỗộơớờởỡợúùủũụưứừửữựýỳỷỹỵđ]+" +
                       @"(?:\s+[A-ZÀÁẢÃẠĂẮẰẲẴẶÂẤẦẨẪẬÉÈẺẼẸÊẾỀỂỄỆÍÌỈĨỊÓÒỎÕỌÔỐỒỔỖỘƠỚỜỞỠỢÚÙỦŨỤƯỨỪỬỮỰÝỲỶỸỴĐ]" +
                       @"[a-zàáảãạăắằẳẵặâấầẩẫậéèẻẽẹêếềểễệíìỉĩịóòỏõọôốồổỗộơớờởỡợúùủũụưứừửữựýỳỷỹỵđ]+)*)";

        var match3 = Regex.Match(textAfterBenB, pattern3, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (match3.Success)
        {
            var name = match3.Groups["name"].Value.Trim();

            logger.LogInformation("Extracted from Pattern 3 (name only): Name='{Name}'", name);
            return (name, null);
        }
        
        logger.LogWarning("Could not extract contact person info from Bên B. Preview: {Text}",
            textAfterBenB.Substring(0, Math.Min(300, textAfterBenB.Length)));

        return (null, null);
    }
    
    private string? ExtractContactPersonName(string text)
    {
        var (name, _) = ExtractContactPersonInfo(text);
        return name;
    }

    
    private string? ExtractIdentityNumber(string text)
    {
        var benBIndex = text.IndexOf("BÊN B", StringComparison.OrdinalIgnoreCase);
        if (benBIndex == -1)
            benBIndex = text.IndexOf("Bên B", StringComparison.OrdinalIgnoreCase);

        if (benBIndex >= 0)
        {
            var textAfterBenB = text.Substring(benBIndex, Math.Min(800, text.Length - benBIndex));
            
            var patterns = new[]
            {
                @"Số\s*CCCD\s*[:：]\s*(\d{12})",
                @"CCCD\s*[:：]\s*(\d{12})",
                @"Số\s*CMND\s*[:：]\s*(\d{9,12})",
                @"CMND\s*[:：]\s*(\d{9,12})",
                @"Số\s*giấy\s*tờ\s*tùy\s*thân\s*[:：]\s*(\d{9,12})"
            };

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(textAfterBenB, pattern, RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    var idNumber = match.Groups[1].Value.Trim();


                    if (idNumber.Length == 12 || idNumber.Length == 9)
                    {
                        logger.LogInformation("Extracted Identity Number: {IdNumber} ({Length} digits)",
                            idNumber, idNumber.Length);
                        return idNumber;
                    }
                }
            }
        }

        logger.LogWarning("Identity Number not found in Bên B");
        return null;
    }

    
    private string? ExtractContactPersonTitle(string text)
    {
        var benBIndex = text.IndexOf("BÊN B", StringComparison.OrdinalIgnoreCase);
        if (benBIndex == -1)
            benBIndex = text.IndexOf("Bên B", StringComparison.OrdinalIgnoreCase);

        if (benBIndex >= 0)
        {
            var textAfterBenB = text.Substring(benBIndex, Math.Min(600, text.Length - benBIndex));
            var pattern1 =
                @"(?:Ông|Bà)\s+[A-ZÁÀẢÃẠĂẮẰẲẴẶÂẤẦẨẪẬÉÈẺẼẸÊẾỀỂỄỆÍÌỈĨỊÓÒỎÕỌÔỐỒỔỖỘƠỚỜỞỠỢÚÙỦŨỤƯỨỪỬỮỰÝỲỶỸỴ][a-záàảãạăắằẳẵặâấầẩẫậéèẻẽẹêếềểễệíìỉĩịóòỏõọôốồổỗộơớờởỡợúùủũụưứừửữựýỳỷỹỵ\s]+?\s*[-–]\s*([A-ZĐa-záàảãạăắằẳẵặâấầẩẫậéèẻẽẹêếềểễệíìỉĩịóòỏõọôốồổỗộơớờởỡợúùủũụưứừửữựýỳỷỹỵ\s]+?)(?:\n|$)";
            var match1 = Regex.Match(textAfterBenB, pattern1, RegexOptions.IgnoreCase);
            if (match1.Success) return match1.Groups[1].Value.Trim();
            
            var pattern2 =
                @"Chức\s*vụ\s*[:：]\s*([A-ZĐa-záàảãạăắằẳẵặâấầẩẫậéèẻẽẹêếềểễệíìỉĩịóòỏõọôốồổỗộơớờởỡợúùủũụưứừửữựýỳỷỹỵ\s]+?)(?:\n|$)";
            var match2 = Regex.Match(textAfterBenB, pattern2, RegexOptions.IgnoreCase);
            if (match2.Success) return match2.Groups[1].Value.Trim();
        }

        return null;
    }
    
    private (string? LocationName, string? LocationAddress) ExtractLocationDetails(string text)
    {

        var dieu1Pattern =
            @"ĐIỀU\s*1\s*[:：]?\s*(?:ĐỐI\s*TƯỢNG\s*VÀ\s*PHẠM\s*VI\s*HỢP\s*ĐỒNG)?([\s\S]{0,800})(?:ĐIỀU\s*2|$)";
        var dieu1Match = Regex.Match(text, dieu1Pattern, RegexOptions.IgnoreCase);

        if (!dieu1Match.Success) return (null, null);

        var dieu1Text = dieu1Match.Groups[1].Value;
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
                locationName = Regex.Replace(locationName, @"\s*[-–]\s*Địa\s*chỉ.*", "", RegexOptions.IgnoreCase);
                break;
            }
        }
        
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
                locationAddress =
                    Regex.Replace(locationAddress, @"\s*[-–]\s*Số\s*lượng.*", "", RegexOptions.IgnoreCase);
                break;
            }
        }

        logger.LogInformation(
            "Extracted location from ĐIỀU 1 - Name: {Name}, Address: {Address}",
            locationName, locationAddress);

        return (locationName, locationAddress);
    }

    private async Task<(decimal? Latitude, decimal? Longitude)?> GetGpsCoordinatesAsync(string? address)
    {
        if (string.IsNullOrWhiteSpace(address)) return null;

        try
        {
            logger.LogInformation("Getting GPS for: {Address}", address);

            var hereApiKey = configuration["HereApiSettings:ApiKey"];
            var hereEndpoint = configuration["HereApiSettings:GeocodingEndpoint"] ??
                               "https://geocode.search.hereapi.com/v1/geocode";

            if (string.IsNullOrWhiteSpace(hereApiKey))
            {
                logger.LogWarning("HERE API key not configured");
                return null;
            }

            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "BASMS-Contracts-API/1.0");
            
            var encodedAddress = Uri.EscapeDataString(address);
            var url = $"{hereEndpoint}?q={encodedAddress}&apiKey={hereApiKey}";

            logger.LogInformation("Querying HERE API...");

            var response = await httpClient.GetStringAsync(url);
            var json = JsonDocument.Parse(response);
            if (!json.RootElement.TryGetProperty("items", out var items) ||
                items.GetArrayLength() == 0)
            {
                logger.LogWarning("No results found for address: {Address}", address);
                return null;
            }
            
            var firstResult = items[0];

            if (!firstResult.TryGetProperty("position", out var position))
            {
                logger.LogWarning("No position in result");
                return null;
            }
            
            if (!position.TryGetProperty("lat", out var latProp) ||
                !position.TryGetProperty("lng", out var lngProp))
            {
                logger.LogWarning("Missing lat/lng in position");
                return null;
            }

            var lat = latProp.GetDecimal();
            var lng = lngProp.GetDecimal();
            var formattedAddress = firstResult.TryGetProperty("address", out var addrProp) &&
                                   addrProp.TryGetProperty("label", out var labelProp)
                ? labelProp.GetString()
                : "N/A";

            var resultType = firstResult.TryGetProperty("resultType", out var typeProp)
                ? typeProp.GetString()
                : "N/A";

            logger.LogInformation(" {Lat}, {Lng}", lat, lng);
            logger.LogInformation("Formatted: {FormattedAddress}", formattedAddress);
            logger.LogInformation("ResultType: {ResultType}", resultType);

            return (lat, lng);
        }
        catch (HttpRequestException httpEx)
        {
            logger.LogError(httpEx, "HTTP error when calling HERE API for address: {Address}", address);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "GPS lookup error: {Address}", address);
            return null;
        }
    }

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
            var existingPeriods = await connection.QueryAsync<ContractPeriod>(
                "SELECT * FROM contract_periods WHERE ContractId = @ContractId ORDER BY PeriodNumber DESC",
                new { ContractId = contractId },
                transaction);

            var existingPeriodsList = existingPeriods.ToList();

            if (!existingPeriodsList.Any())
            {
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
                logger.LogInformation("Created initial contract period (Period 1): {Start} to {End}",
                    startDate.Value.ToString("dd/MM/yyyy"),
                    endDate.Value.ToString("dd/MM/yyyy"));
            }
            else
            {
                var currentPeriod = existingPeriodsList.First(); 

                if (isRenewal)
                {
                    currentPeriod.IsCurrentPeriod = false;
                    await connection.UpdateAsync(currentPeriod, transaction);
                    var renewalPeriod = new ContractPeriod
                    {
                        Id = Guid.NewGuid(),
                        ContractId = contractId,
                        PeriodNumber = currentPeriod.PeriodNumber + 1,
                        PeriodType = "renewal",
                        PeriodStartDate = startDate.Value,
                        PeriodEndDate = endDate.Value,
                        IsCurrentPeriod = true,
                        Notes = duration != null
                            ? $"Gia hạn lần {currentPeriod.PeriodNumber}. Thời hạn: {duration}"
                            : $"Renewal {currentPeriod.PeriodNumber}",
                        CreatedAt = DateTime.UtcNow
                    };

                    await connection.InsertAsync(renewalPeriod, transaction);
                    logger.LogInformation("Created renewal period (Period {PeriodNumber}): {Start} to {End}",
                        renewalPeriod.PeriodNumber,
                        startDate.Value.ToString("dd/MM/yyyy"),
                        endDate.Value.ToString("dd/MM/yyyy"));
                    
                    logger.LogInformation(
                        "Contract period history: Old period {OldNumber} ({OldEnd}) → New period {NewNumber} ({NewEnd})",
                        currentPeriod.PeriodNumber,
                        currentPeriod.PeriodEndDate.ToString("dd/MM/yyyy"),
                        renewalPeriod.PeriodNumber,
                        renewalPeriod.PeriodEndDate.ToString("dd/MM/yyyy"));
                }
                else
                {
                    if (currentPeriod.PeriodEndDate != endDate.Value ||
                        currentPeriod.PeriodStartDate != startDate.Value)
                    {
                        var oldStartDate = currentPeriod.PeriodStartDate;
                        var oldEndDate = currentPeriod.PeriodEndDate;
                        currentPeriod.PeriodStartDate = startDate.Value;
                        currentPeriod.PeriodEndDate = endDate.Value;
                        if (duration != null) currentPeriod.Notes = $"Thời hạn: {duration} (Updated)";

                        await connection.UpdateAsync(currentPeriod, transaction);
                        logger.LogInformation(
                            "Updated contract period {PeriodNumber}: {OldStart}-{OldEnd} → {NewStart}-{NewEnd}",
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
        var patterns = new[]
        {
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
        
        var patterns = new[]
        {
            @"Ca\s+(sáng|chiều|tối|đêm|cuối\s+tuần|khuya|trưa)[^\d]*?(\d{1,2})[h:](\d{2})?\s*[-–—]\s*(\d{1,2})[h:](\d{2})?",
            
            @"\d+\.\d+\.\s*Ca\s+(sáng|chiều|tối|đêm|cuối\s+tuần|khuya|trưa)[^\d]*?(\d{1,2})[h:](\d{2})?\s*[-–—]\s*(\d{1,2})[h:](\d{2})?",
            
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
    
    private Dieu3ParsedInfo ParseDieu3(string fullText, DateTime? contractStartDate, DateTime? contractEndDate)
    {
        var info = new Dieu3ParsedInfo();
        
        var dieu3Section = ExtractSection(fullText, "ĐIỀU 3", "ĐIỀU 4");
        if (string.IsNullOrEmpty(dieu3Section))
        {
            logger.LogWarning("Không tìm thấy ĐIỀU 3 trong hợp đồng");
            return info;
        }

        logger.LogInformation("Found ĐIỀU 3 section ({Length} chars)", dieu3Section.Length);
        
        info.ShiftSchedules = ParseDieu3_1_ShiftSchedules(dieu3Section);
        logger.LogInformation("Parsed {Count} shift schedules from section 3.1", info.ShiftSchedules.Count);
        
        ParseDieu3_3_WeekendWork(dieu3Section, info);
        logger.LogInformation("Weekend: Sat={Sat}, Sun={Sun}, AppliesWeekend={Weekend}",
            info.AppliesSaturday, info.AppliesSunday, info.AppliesOnWeekends);
        
        var startYear = contractStartDate?.Year ?? DateTime.Now.Year;
        var endYear = contractEndDate?.Year ?? startYear + 1;

        info.PublicHolidays = ParseDieu3_4_PublicHolidays(dieu3Section, startYear, endYear);
        info.SubstituteWorkDays = ParseDieu3_4_SubstituteWorkDays(dieu3Section, startYear, endYear);
        info.WorkOnPublicHolidays = CheckDieu3_4_WorkOnHolidays(dieu3Section);

        logger.LogInformation("Holidays: {Count} public holidays, {SubCount} substitute days, WorkOnHolidays={Work}",
            info.PublicHolidays.Count, info.SubstituteWorkDays.Count, info.WorkOnPublicHolidays);

        return info;
    }
    
    private string ExtractSection(string text, string startMarker, string? endMarker = null)
    {
        var startIndex = text.IndexOf(startMarker, StringComparison.OrdinalIgnoreCase);
        if (startIndex == -1)
            return string.Empty;

        if (!string.IsNullOrEmpty(endMarker))
        {
            var endIndex = text.IndexOf(endMarker, startIndex + startMarker.Length, StringComparison.OrdinalIgnoreCase);
            if (endIndex > startIndex) return text.Substring(startIndex, endIndex - startIndex);
        }
        return text.Substring(startIndex, Math.Min(5000, text.Length - startIndex));
    }


    private List<Dieu3ShiftSchedule> ParseDieu3_1_ShiftSchedules(string dieu3Text)
    {
        var shifts = new List<Dieu3ShiftSchedule>();
        
        var patterns = new[]
        {
            @"[•\-]\s*Ca\s+(sáng|chiều|tối|đêm|khuya)\s*[:：]\s*(\d{1,2})h(\d{2})?\s*[–\-—]\s*(\d{1,2})h(\d{2})?(?:\s+ngày\s+hôm\s+sau)?",
            @"Ca\s+(sáng|chiều|tối|đêm|khuya)\s*[:：]\s*(\d{1,2})h(\d{2})?\s*[–\-—]\s*(\d{1,2})h(\d{2})?(?:\s+ngày\s+hôm\s+sau)?"
        };

        foreach (var pattern in patterns)
        {
            var matches = Regex.Matches(dieu3Text, pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);

            foreach (Match match in matches)
            {
                var shiftName = match.Groups[1].Value.Trim();
                var startHour = match.Groups[2].Value;
                var startMin = match.Groups[3].Success && !string.IsNullOrEmpty(match.Groups[3].Value)
                    ? match.Groups[3].Value
                    : "00";
                var endHour = match.Groups[4].Value;
                var endMin = match.Groups[5].Success && !string.IsNullOrEmpty(match.Groups[5].Value)
                    ? match.Groups[5].Value
                    : "00";

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

                    logger.LogInformation("Shift: {Name} ({Start} - {End}) CrossMidnight={Cross}",
                        $"Ca {normalizedName}", start, end, crossesMidnight);
                }
            }
        }

        return shifts.DistinctBy(s => new { s.StartTime, s.EndTime }).ToList();
    }

    private void ParseDieu3_3_WeekendWork(string dieu3Text, Dieu3ParsedInfo info)
    {
        var section33Match = Regex.Match(dieu3Text,
            @"3\.3\.?\s+[^\r\n]*(?:cuối\s*tuần|Thứ\s*Bảy|Chủ\s*Nhật)[^\r\n]*",
            RegexOptions.IgnoreCase);

        if (!section33Match.Success)
        {
            info.AppliesSaturday = false;
            info.AppliesSunday = false;
            info.AppliesOnWeekends = false;
            logger.LogInformation("No section 3.3 found → weekends OFF (0, 0, 0)");
            return;
        }
        
        var section33Start = section33Match.Index;
        var section34Match = Regex.Match(dieu3Text, @"3\.4\.?", RegexOptions.IgnoreCase);
        var section33Length = section34Match.Success && section34Match.Index > section33Start
            ? section34Match.Index - section33Start
            : Math.Min(1200, dieu3Text.Length - section33Start);

        var section33 = dieu3Text.Substring(section33Start, section33Length);

        logger.LogInformation("Section 3.3 content ({Length} chars): {Preview}",
            section33.Length, section33.Length > 250 ? section33.Substring(0, 250) + "..." : section33);
        
        var workNormalPatterns = new[]
        {
            @"(?:duy\s*trì|bố\s*trí).*?(?:lực\s*lượng|bảo\s*vệ).*?(?:như\s*ngày\s*làm\s*việc\s*bình\s*thường|bình\s*thường)",
            @"(?:duy\s*trì|bố\s*trí).*?(?:như\s*ngày\s*làm\s*việc\s*bình\s*thường|bình\s*thường)",
            @"làm\s*việc.*?(?:như\s*)?bình\s*thường.*?cuối\s*tuần"
        };

        foreach (var pattern in workNormalPatterns)
            if (Regex.IsMatch(section33, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline))
            {
                info.AppliesSaturday = true;
                info.AppliesSunday = true;
                info.AppliesOnWeekends = true;
                logger.LogInformation("Section 3.3: 'DUY TRÌ NHƯ BÌNH THƯỜNG' → weekends ON (1, 1, 1)");
                return;
            }
        
        if (Regex.IsMatch(section33,
                @"(?:Không|không)\s+áp\s+dụng\s+(?:chế\s+độ\s+)?nghỉ\s+riêng.*?cuối\s*tuần",
                RegexOptions.IgnoreCase | RegexOptions.Singleline))
        {
            info.AppliesSaturday = true;
            info.AppliesSunday = true;
            info.AppliesOnWeekends = true;
            logger.LogInformation("Section 3.3: 'KHÔNG ÁP DỤNG NGHỈ RIÊNG' → weekends ON (1, 1, 1)");
            return;
        }
        
        var offPatterns = new[]
        {
            @"áp\s*dụng\s+(?:chế\s+độ\s+)?nghỉ\s+riêng.*?cuối\s*tuần",
            @"nghỉ.*?(?:vào|trong).*?cuối\s*tuần",
            @"không\s+làm\s+việc.*?cuối\s*tuần",
            @"cuối\s*tuần.*?được\s+nghỉ"
        };

        foreach (var pattern in offPatterns)
            if (Regex.IsMatch(section33, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline))
            {
                info.AppliesSaturday = false;
                info.AppliesSunday = false;
                info.AppliesOnWeekends = false;
                logger.LogInformation("Section 3.3: 'NGHỈ RIÊNG/KHÔNG LÀM VIỆC' → weekends OFF (0, 0, 0)");
                return;
            }
        
        var hasSaturday = Regex.IsMatch(section33, @"(?:thứ\s*(?:7|bảy)|saturday)(?!\s*và\s*chủ\s*nhật.*?nghỉ)",
            RegexOptions.IgnoreCase);
        var hasSunday = Regex.IsMatch(section33, @"(?:chủ\s*nhật|sunday)(?!\s*nghỉ)", RegexOptions.IgnoreCase);

        if (hasSaturday && hasSunday)
        {
            info.AppliesSaturday = true;
            info.AppliesSunday = true;
            info.AppliesOnWeekends = true;
            logger.LogInformation("Section 3.3: Mentions 'THỨ 7 VÀ CHỦ NHẬT' → weekends ON (1, 1, 1)");
            return;
        }

        if (hasSaturday && !hasSunday)
        {
            info.AppliesSaturday = true;
            info.AppliesSunday = false;
            info.AppliesOnWeekends = true;
            logger.LogInformation("Section 3.3: Mentions 'THỨ 7' only → Saturday ON (1, 0, 1)");
            return;
        }

        if (!hasSaturday && hasSunday)
        {
            info.AppliesSaturday = false;
            info.AppliesSunday = true;
            info.AppliesOnWeekends = true;
            logger.LogInformation("Section 3.3: Mentions 'CHỦ NHẬT' only → Sunday ON (0, 1, 1)");
            return;
        }
        
        info.AppliesSaturday = true;
        info.AppliesSunday = true;
        info.AppliesOnWeekends = true;
        logger.LogInformation("  ⚠ Section 3.3: Ambiguous/default → weekends ON (1, 1, 1)");
    }
    
    private List<Dieu3PublicHoliday> ParseDieu3_4_PublicHolidays(string dieu3Text, int startYear, int endYear)
    {
        var holidays = new List<Dieu3PublicHoliday>();
        var section34Match = Regex.Match(dieu3Text, @"3\.4\.?\s+[^\r\n]*(?:Ngày\s*lễ|Tết)", RegexOptions.IgnoreCase);
        if (!section34Match.Success)
        {
            logger.LogWarning("  No section 3.4 found");
            return holidays;
        }

        var section34Start = section34Match.Index;
        var section34 = dieu3Text.Substring(section34Start, Math.Min(3000, dieu3Text.Length - section34Start));

        logger.LogInformation("Section 3.4 content preview (first 500 chars):\n{Preview}",
            section34.Length > 500 ? section34.Substring(0, 500) : section34);


        var tetPatterns = new[]
        {
            @"Tết\s+Nguy[eê]n\s+[ĐđDd][áaA]n\s+(\d{4})[:\s,]+(?:Từ\s+)?[^0-9]*?(\d{1,2}/\d{1,2}/\d{4})\s+đến\s+(?:hết\s+)?[^0-9]*?(\d{1,2}/\d{1,2}/\d{4})",

            @"Tết\s+Nguy[eê]n\s+[ĐđDd][áaA]n[:\s,]+[^0-9]*?(\d{1,2}/(?:01|02)/\d{4})\s*[-–]\s*(\d{1,2}/(?:01|02)/\d{4})",
            
            @"Tết\s+âm\s+lịch[:\s,]+[^0-9]*?(\d{1,2}/(?:01|02)/\d{4})\s+đến\s+(?:hết\s+)?[^0-9]*?(\d{1,2}/(?:01|02)/\d{4})"
        };

        var tetFound = false;
        foreach (var tetPattern in tetPatterns)
        {
            if (tetFound) break; 

            var tetMatch = Regex.Match(section34, tetPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (tetMatch.Success)
            {
                logger.LogInformation("Tết pattern matched! Groups: {Count}", tetMatch.Groups.Count);

                var tetDates = new List<DateTime>();

                for (var i = 1; i < tetMatch.Groups.Count; i++)
                {
                    var groupValue = tetMatch.Groups[i].Value.Trim();

                    if (string.IsNullOrEmpty(groupValue))
                        continue;

                    if (groupValue.Length == 4 && !groupValue.Contains("/"))
                    {
                        logger.LogInformation("Skipped Group[{Index}]: {Value} (year-only)", i, groupValue);
                        continue;
                    }

                    if (DateTime.TryParseExact(groupValue,
                            new[] { "d/M/yyyy", "dd/MM/yyyy", "d/MM/yyyy", "dd/M/yyyy" },
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.None,
                            out var date))
                    {
                        tetDates.Add(date);
                        logger.LogInformation("Parsed date from Group[{Index}]: {Date:dd/MM/yyyy}", i, date);
                    }
                }

                if (tetDates.Count == 2)
                {
                    tetDates.Sort();
                    var tetStart = tetDates[0];
                    var tetEnd = tetDates[1];

                    var totalDays = (tetEnd - tetStart).Days + 1;

                    if (totalDays < 3 || totalDays > 10)
                    {
                        logger.LogWarning("Invalid Tết duration: {Days} days (expected 3-10)", totalDays);
                        continue;
                    }

                    if (tetStart.Month > 2)
                    {
                        logger.LogWarning("Invalid Tết month: {Month} (expected Jan/Feb)", tetStart.Month);
                        continue;
                    }

                    if (tetStart.Month == 1 && tetStart.Day == 1)
                    {
                        logger.LogWarning("Rejected: This is Tết Dương Lịch (01/01), not Tết Nguyên Đán");
                        continue;
                    }
                    
                    holidays.Add(new Dieu3PublicHoliday
                    {
                        HolidayDate = tetStart,
                        HolidayName = "Tết Nguyên Đán",
                        HolidayNameEn = "Lunar New Year",
                        HolidayCategory = "tet",
                        IsTetPeriod = true,
                        IsTetHoliday = true,
                        TetDayNumber = totalDays, 
                        HolidayStartDate = tetStart,
                        HolidayEndDate = tetEnd,
                        TotalHolidayDays = totalDays,
                        Year = tetStart.Year
                    });

                    logger.LogInformation(
                        "  ✓ Tết Nguyên Đán {Year}: {Days} days ({Start:dd/MM/yyyy} - {End:dd/MM/yyyy})",
                        tetStart.Year, totalDays, tetStart, tetEnd);

                    tetFound = true;
                    break;
                }

                logger.LogWarning("Found {Count} dates, expected exactly 2 for Tết period", tetDates.Count);
            }
        }

        if (!tetFound) logger.LogWarning("Tết Nguyên Đán not found in section 3.4");
        
        
        var hungVuongPattern = @"Giỗ\s+Tổ\s+Hùng\s+Vương.*?(\d{1,2}/\d{1,2}/\d{4})";
        var hungVuongMatch = Regex.Match(section34, hungVuongPattern, RegexOptions.IgnoreCase);
        if (hungVuongMatch.Success && DateTime.TryParse(hungVuongMatch.Groups[1].Value, out var hungVuongDate))
            holidays.Add(new Dieu3PublicHoliday
            {
                HolidayDate = hungVuongDate,
                HolidayName = "Giỗ Tổ Hùng Vương",
                HolidayNameEn = "Hung Kings' Festival",
                HolidayCategory = "national",
                Year = hungVuongDate.Year,
                HolidayStartDate = hungVuongDate,
                HolidayEndDate = hungVuongDate,
                TotalHolidayDays = (hungVuongDate - hungVuongDate).Days + 1
            });


        
        var day304Pattern = @"(?:30/4|Giải\s*phóng\s*miền\s*Nam).*?(\d{1,2}/04/\d{4})";
        var day304Match = Regex.Match(section34, day304Pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (day304Match.Success && DateTime.TryParseExact(day304Match.Groups[1].Value,
                new[] { "d/M/yyyy", "dd/MM/yyyy", "d/MM/yyyy", "dd/M/yyyy" },
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var day304))
        {
            holidays.Add(new Dieu3PublicHoliday
            {
                HolidayDate = day304,
                HolidayName = "Ngày Giải phóng miền Nam",
                HolidayNameEn = "Reunification Day",
                HolidayCategory = "national",
                Year = day304.Year,
                HolidayStartDate = day304,
                HolidayEndDate = day304,
                TotalHolidayDays = (day304 - day304).Days + 1
            });
            logger.LogInformation("Found 30/4: {Date:dd/MM/yyyy}", day304);
        }


        var day015Pattern = @"(?:01/5|1/5|Quốc\s*tế\s*Lao\s*động|Lao\s*động).*?(\d{1,2}/05/\d{4})";
        var day015Match = Regex.Match(section34, day015Pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (day015Match.Success && DateTime.TryParseExact(day015Match.Groups[1].Value,
                new[] { "d/M/yyyy", "dd/MM/yyyy", "d/MM/yyyy", "dd/M/yyyy" },
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var day015))
        {
            holidays.Add(new Dieu3PublicHoliday
            {
                HolidayDate = day015,
                HolidayName = "Ngày Quốc tế Lao động",
                HolidayNameEn = "International Labor Day",
                HolidayCategory = "national",
                Year = day015.Year,
                HolidayStartDate = day015,
                HolidayEndDate = day015,
                TotalHolidayDays = (day015 - day015).Days + 1
            });
            logger.LogInformation("Found 1/5: {Date:dd/MM/yyyy}", day015);
        }
        
        var nationalDayPattern = @"Quốc\s*khánh.*?(\d{1,2}/09/\d{4})";
        var nationalDayMatch = Regex.Match(section34, nationalDayPattern, RegexOptions.IgnoreCase);
        if (nationalDayMatch.Success && DateTime.TryParse(nationalDayMatch.Groups[1].Value, out var nationalDay))
            holidays.Add(new Dieu3PublicHoliday
            {
                HolidayDate = nationalDay,
                HolidayName = "Ngày Quốc khánh",
                HolidayNameEn = "National Day",
                HolidayCategory = "national",
                Year = nationalDay.Year,
                HolidayStartDate = nationalDay,
                HolidayEndDate = nationalDay,
                TotalHolidayDays = (nationalDay - nationalDay).Days + 1
            });
        
        var newYearPattern = @"Tết\s+Dương\s+lịch.*?(\d{1,2}/01/\d{4})";
        var newYearMatch = Regex.Match(section34, newYearPattern, RegexOptions.IgnoreCase);
        if (newYearMatch.Success && DateTime.TryParse(newYearMatch.Groups[1].Value, out var newYearDay))
            holidays.Add(new Dieu3PublicHoliday
            {
                HolidayDate = newYearDay,
                HolidayName = "Tết Dương lịch",
                HolidayNameEn = "New Year's Day",
                HolidayCategory = "national",
                Year = newYearDay.Year,
                HolidayStartDate = newYearDay,
                HolidayEndDate = newYearDay,
                TotalHolidayDays = (newYearDay - newYearDay).Days + 1
            });

        return holidays;
    }
    
    private List<Dieu3SubstituteWorkDay> ParseDieu3_4_SubstituteWorkDays(string dieu3Text, int startYear, int endYear)
    {
        var substitutes = new List<Dieu3SubstituteWorkDay>();

        var section34Match = Regex.Match(dieu3Text, @"3\.4\.?\s+[^\r\n]*(?:Ngày\s*lễ|Tết)", RegexOptions.IgnoreCase);
        if (!section34Match.Success)
            return substitutes;

        var section34 =
            dieu3Text.Substring(section34Match.Index, Math.Min(3000, dieu3Text.Length - section34Match.Index));
        
        var substitutePattern = @"nghỉ\s*bù\s*(?:ngày\s*)?(\d{1,2}/\d{1,2}/\d{4})";
        var matches = Regex.Matches(section34, substitutePattern, RegexOptions.IgnoreCase);

        foreach (Match match in matches)
            if (DateTime.TryParse(match.Groups[1].Value, out var subDate))
                substitutes.Add(new Dieu3SubstituteWorkDay
                {
                    SubstituteDate = subDate,
                    Reason = "Nghỉ bù theo quy định Nhà nước",
                    Year = subDate.Year
                });

        return substitutes;
    }


    private bool CheckDieu3_4_WorkOnHolidays(string dieu3Text)
    {
        var section34Match = Regex.Match(dieu3Text, @"3\.4\.?\s+[^\r\n]*(?:Ngày\s*lễ|Tết)", RegexOptions.IgnoreCase);
        if (!section34Match.Success)
            return false;

        var section34 =
            dieu3Text.Substring(section34Match.Index, Math.Min(2000, dieu3Text.Length - section34Match.Index));
        
        var workPatterns = new[]
        {
            @"vẫn\s+phải\s+bố\s+trí.*?trực\s+24/24",
            @"Bên\s+A\s+vẫn\s+phải\s+bố\s+trí.*?nhân\s+viên",
            @"nhân\s+viên.*?vẫn\s+làm\s+việc\s+bình\s+thường"
        };

        foreach (var pattern in workPatterns)
            if (Regex.IsMatch(section34, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline))
            {
                logger.LogInformation("  ✓ Work on public holidays: TRUE");
                return true;
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
        return shiftName;
    }

    private bool? CheckWorkOnHolidays(string text)
    {
        return Regex.IsMatch(text, @"làm\s*việc.*?ngày\s*lễ", RegexOptions.IgnoreCase) ? true :
            Regex.IsMatch(text, @"nghỉ.*?ngày\s*lễ", RegexOptions.IgnoreCase) ? false : null;
    }

    private bool? CheckWorkOnWeekends(string text)
    {
        return Regex.IsMatch(text, @"làm\s*việc.*?cuối\s*tuần", RegexOptions.IgnoreCase) ? true :
            Regex.IsMatch(text, @"nghỉ.*?cuối\s*tuần", RegexOptions.IgnoreCase) ? false : null;
    }

    private decimal CalculateDuration(TimeSpan start, TimeSpan end)
    {
        var duration = end - start;
        if (duration.TotalHours < 0) duration = duration.Add(TimeSpan.FromHours(24));
        return (decimal)duration.TotalHours;
    }
    
    private async Task<Customer?> FindCustomerAsync(
        IDbConnection connection,
        string? email,
        string? identityNumber,
        string? phoneNumber)
    {
        Customer? existing = null;
        
        if (!string.IsNullOrWhiteSpace(email))
        {
            existing = await connection.QueryFirstOrDefaultAsync<Customer>(
                "SELECT * FROM customers WHERE Email = @Email AND IsDeleted = 0 LIMIT 1",
                new { Email = email });

            if (existing != null)
            {
                logger.LogInformation("Found customer by Email: {CustomerId} - {CompanyName}", existing.Id, existing.CompanyName ?? existing.ContactPersonName);
                return existing;
            }
        }
        
        if (!string.IsNullOrWhiteSpace(identityNumber))
        {
            existing = await connection.QueryFirstOrDefaultAsync<Customer>(
                "SELECT * FROM customers WHERE IdentityNumber = @IdentityNumber AND IsDeleted = 0 LIMIT 1",
                new { IdentityNumber = identityNumber });

            if (existing != null)
            {
                logger.LogInformation("Found customer by IdentityNumber (CCCD): {CustomerId} - {CompanyName}", existing.Id, existing.CompanyName ?? existing.ContactPersonName);
                return existing;
            }
        }
        
        if (!string.IsNullOrWhiteSpace(phoneNumber))
        {
            existing = await connection.QueryFirstOrDefaultAsync<Customer>(
                "SELECT * FROM customers WHERE Phone = @Phone AND IsDeleted = 0 LIMIT 1",
                new { Phone = phoneNumber });

            if (existing != null)
            {
                logger.LogInformation("Found customer by PhoneNumber: {CustomerId} - {CompanyName}", existing.Id, existing.CompanyName ?? existing.ContactPersonName);
                return existing;
            }
        }

        logger.LogWarning("Customer not found with Email: {Email}, IdentityNumber: {IdentityNumber}, PhoneNumber: {PhoneNumber}",
            email ?? "N/A", identityNumber ?? "N/A", phoneNumber ?? "N/A");

        return null;
    }

    private int CalculateConfidenceScore(
        string? contractNumber, string? customerName,
        DateTime? startDate, DateTime? endDate,
        int guardsRequired, int schedulesCount)
    {
        var score = 0;
        if (!string.IsNullOrEmpty(contractNumber)) score += 15;
        if (!string.IsNullOrEmpty(customerName)) score += 20;
        if (startDate.HasValue) score += 15;
        if (endDate.HasValue) score += 15;
        if (guardsRequired > 0) score += 20;
        if (schedulesCount > 0) score += 15;
        return Math.Min(score, 100);
    }
    
    private VietnameseAddress ParseVietnameseAddressComponents(string address)
    {
        var addr = new VietnameseAddress();
        
        var parts = address.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .ToList();

        if (parts.Count == 0) return addr;
        
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
        
        addr.Ward = parts.FirstOrDefault(p => p.Contains("Phường") || p.Contains("Phư") || p.Contains("P."));
        
        addr.District = parts.FirstOrDefault(p =>
            p.Contains("Quận") || p.Contains("Huyện") ||
            p.Contains("Thành phố") || p.Contains("Thị xã"));
        
        var cityPart = parts.LastOrDefault();
        addr.City = NormalizeCityNameSimple(cityPart);

        return addr;
    }

    
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
    
    
    private async Task<Customer?> FindCustomerByUserIdAsync(
        IDbConnection connection,
        Guid userId,
        IDbTransaction? transaction = null)
    {
        if (userId == Guid.Empty)
            return null;

        return await connection.QueryFirstOrDefaultAsync<Customer>(
            "SELECT * FROM customers WHERE UserId = @UserId AND IsDeleted = 0 LIMIT 1",
            new { UserId = userId },
            transaction);
    }
    
    private async Task<Customer?> FindCustomerByEmailAsync(
        IDbConnection connection,
        string? email,
        IDbTransaction? transaction = null)
    {
        if (string.IsNullOrEmpty(email))
            return null;

        return await connection.QueryFirstOrDefaultAsync<Customer>(
            "SELECT * FROM customers WHERE Email = @Email AND IsDeleted = 0 LIMIT 1",
            new { Email = email },
            transaction);
    }

    private async Task<Customer?> FindCustomerByCompanyNameAsync(
        IDbConnection connection,
        string? companyName,
        IDbTransaction? transaction = null)
    {
        if (string.IsNullOrEmpty(companyName))
            return null;

        return await connection.QueryFirstOrDefaultAsync<Customer>(
            "SELECT * FROM customers WHERE CompanyName = @Name AND IsDeleted = 0 LIMIT 1",
            new { Name = companyName },
            transaction);
    }


    private string? ExtractBenBSection(string text, int maxLength = 1000)
    {
        var benBIndex = text.IndexOf("BÊN B", StringComparison.OrdinalIgnoreCase);
        if (benBIndex == -1)
            benBIndex = text.IndexOf("Bên B", StringComparison.OrdinalIgnoreCase);

        if (benBIndex < 0)
            return null;

        var length = Math.Min(maxLength, text.Length - benBIndex);
        return text.Substring(benBIndex, length);
    }
    
    private async Task<bool> MergeCustomerInfoAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        Customer customer,
        string? contactPersonName,
        string? contactPersonTitle,
        string? identityNumber,
        string? gender,
        string? address)
    {
        var updated = false;

        if (string.IsNullOrEmpty(customer.ContactPersonName) && !string.IsNullOrEmpty(contactPersonName))
        {
            customer.ContactPersonName = contactPersonName;
            updated = true;
        }

        if (string.IsNullOrEmpty(customer.ContactPersonTitle) && !string.IsNullOrEmpty(contactPersonTitle))
        {
            customer.ContactPersonTitle = contactPersonTitle;
            updated = true;
        }

        if (string.IsNullOrEmpty(customer.IdentityNumber) && !string.IsNullOrEmpty(identityNumber))
        {
            customer.IdentityNumber = identityNumber;
            updated = true;
        }

        if (string.IsNullOrEmpty(customer.Gender) && !string.IsNullOrEmpty(gender))
        {
            customer.Gender = gender;
            updated = true;
        }

        if (string.IsNullOrEmpty(customer.Address) && !string.IsNullOrEmpty(address))
        {
            customer.Address = address;
            updated = true;
        }

        if (updated)
        {
            customer.UpdatedAt = DateTime.UtcNow;
            await connection.UpdateAsync(customer, transaction);
            logger.LogInformation(
                "Updated customer {CustomerId} with additional info from contract document",
                customer.Id);
        }

        return updated;
    }
    
    private bool TryParseVietnameseDate(string? dateString, out DateTime result)
    {
        result = DateTime.MinValue;

        if (string.IsNullOrWhiteSpace(dateString))
            return false;

        var formats = new[]
        {
            "d/M/yyyy", "dd/MM/yyyy", "d/MM/yyyy", "dd/M/yyyy",
            "dd-MM-yyyy", "d-M-yyyy", "yyyy-MM-dd"
        };

        return DateTime.TryParseExact(
            dateString.Trim(),
            formats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out result);
    }


    private Dieu3PublicHoliday CreatePublicHoliday(
        DateTime date,
        string name,
        string nameEn,
        string category = "national",
        DateTime? startDate = null,
        DateTime? endDate = null,
        int? totalDays = null,
        bool isTet = false,
        int? tetDayNumber = null)
    {
        return new Dieu3PublicHoliday
        {
            HolidayDate = date,
            HolidayName = name,
            HolidayNameEn = nameEn,
            HolidayCategory = category,
            Year = date.Year,
            HolidayStartDate = startDate ?? date,
            HolidayEndDate = endDate ?? date,
            TotalHolidayDays = totalDays ?? 1,
            IsTetPeriod = isTet,
            IsTetHoliday = isTet,
            TetDayNumber = tetDayNumber
        };
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
        public bool AutoRenewal { get; set; }
    }

    private record ShiftInfo
    {
        public string? ShiftName { get; init; }
        public TimeSpan? StartTime { get; init; }
        public TimeSpan? EndTime { get; init; }
        public int? GuardsPerShift { get; init; }
    }

    
    private class VietnameseAddress
    {
        public string HouseNumber { get; set; } = "";
        public string Street { get; set; } = "";
        public string? Ward { get; set; }
        public string District { get; set; } = "";
        public string City { get; set; } = "Ho Chi Minh City";
    }


    private class Dieu3ParsedInfo
    {
        public List<Dieu3ShiftSchedule> ShiftSchedules { get; set; } = new();
        public bool AppliesSaturday { get; set; }
        public bool AppliesSunday { get; set; }
        public bool AppliesOnWeekends { get; set; }
        public List<Dieu3PublicHoliday> PublicHolidays { get; set; } = new();
        public List<Dieu3SubstituteWorkDay> SubstituteWorkDays { get; set; } = new();
        public bool WorkOnPublicHolidays { get; set; }
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
        public bool IsTetPeriod { get; set; }
        public bool IsTetHoliday { get; set; }
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