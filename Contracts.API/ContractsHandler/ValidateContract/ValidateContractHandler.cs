namespace Contracts.API.ContractsHandler.ValidateContract;

// ================================================================
// QUERY & RESULT
// ================================================================

/// <summary>
/// Query để validate contract với document
/// </summary>
public record ValidateContractQuery(
    Guid ContractId,
    Stream? DocumentStream,
    string? FileName
) : IQuery<ValidateContractResult>;

/// <summary>
/// Kết quả validation với tỷ lệ khớp % và danh sách differences
/// </summary>
public record ValidateContractResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public ValidationSummary? Summary { get; init; }
}

public record ValidationSummary
{
    public decimal MatchPercentage { get; init; }
    public int TotalFieldsChecked { get; init; }
    public int MatchedFields { get; init; }
    public int MismatchedFields { get; init; }
    public int MissingInDocument { get; init; }
    public int ExtraInDocument { get; init; }

    // Chi tiết từng phần
    public SectionComparison ContractInfo { get; init; } = new();
    public SectionComparison Locations { get; init; } = new();
    public SectionComparison ShiftSchedules { get; init; } = new();
    public SectionComparison PublicHolidays { get; init; } = new();
    public SectionComparison WorkingConditions { get; init; } = new();

    // Danh sách tất cả differences
    public List<ValidationDifference> Differences { get; init; } = new();
}

public record SectionComparison
{
    public string SectionName { get; init; } = string.Empty;
    public decimal MatchPercentage { get; init; }
    public int TotalFields { get; init; }
    public int MatchedFields { get; init; }
    public List<FieldComparison> Fields { get; init; } = new();
}

public record FieldComparison
{
    public string FieldName { get; init; } = string.Empty;
    public string? DatabaseValue { get; init; }
    public string? DocumentValue { get; init; }
    public bool IsMatch { get; init; }
    public string? Difference { get; init; }
}

public record ValidationDifference
{
    public string Category { get; init; } = string.Empty;
    public string Field { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty; // mismatch, missing, extra
    public string? DatabaseValue { get; init; }
    public string? DocumentValue { get; init; }
    public string Description { get; init; } = string.Empty;
    public string Severity { get; init; } = "medium"; // low, medium, high, critical
}

internal class ValidateContractHandler(
    IDbConnectionFactory connectionFactory,
    ILogger<ValidateContractHandler> logger)
    : IQueryHandler<ValidateContractQuery, ValidateContractResult>
{
    public async Task<ValidateContractResult> Handle(
        ValidateContractQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Validating contract {ContractId} against document: {FileName}",
                request.ContractId, request.FileName ?? "N/A");

            // Validate input
            if (request.DocumentStream == null || string.IsNullOrEmpty(request.FileName))
            {
                return new ValidateContractResult
                {
                    Success = false,
                    ErrorMessage = "Document file is required for validation"
                };
            }

            using var connection = await connectionFactory.CreateConnectionAsync();

            // ================================================================
            // 1. LẤY DỮ LIỆU TỪ DATABASE
            // ================================================================

            var contract = await connection.QueryFirstOrDefaultAsync<Models.Contract>(
                "SELECT * FROM contracts WHERE Id = @Id AND IsDeleted = 0",
                new { Id = request.ContractId });

            if (contract == null)
            {
                return new ValidateContractResult
                {
                    Success = false,
                    ErrorMessage = $"Contract with ID {request.ContractId} not found"
                };
            }

            Models.Customer? customer = null;
            if (contract.CustomerId.HasValue)
            {
                customer = await connection.QueryFirstOrDefaultAsync<Models.Customer>(
                    "SELECT * FROM customers WHERE Id = @CustomerId",
                    new { CustomerId = contract.CustomerId.Value });
            }

            var contractLocations = (await connection.QueryAsync<dynamic>(
                @"SELECT cl.*, loc.LocationName, loc.Address, loc.City, loc.District
                  FROM contract_locations cl
                  INNER JOIN customer_locations loc ON cl.LocationId = loc.Id
                  WHERE cl.ContractId = @ContractId AND cl.IsDeleted = 0",
                new { ContractId = contract.Id })).ToList();

            var shiftSchedules = (await connection.QueryAsync<Models.ContractShiftSchedule>(
                "SELECT * FROM contract_shift_schedules WHERE ContractId = @ContractId AND IsDeleted = 0",
                new { ContractId = contract.Id })).ToList();

            // Lấy public holidays trong khoảng thời gian hợp đồng
            var publicHolidays = (await connection.QueryAsync<Models.PublicHoliday>(
                @"SELECT * FROM public_holidays
                  WHERE HolidayDate >= @StartDate AND HolidayDate <= @EndDate
                  ORDER BY HolidayDate",
                new { StartDate = contract.StartDate, EndDate = contract.EndDate })).ToList();

            // ================================================================
            // 2. TRÍCH XUẤT TEXT TỪ DOCUMENT
            // ================================================================

            string documentText;
            try
            {
                documentText = ExtractTextFromDocument(request.DocumentStream, request.FileName);
            }
            catch (Exception ex)
            {
                return new ValidateContractResult
                {
                    Success = false,
                    ErrorMessage = $"Failed to extract text from document: {ex.Message}"
                };
            }

            if (string.IsNullOrWhiteSpace(documentText))
            {
                return new ValidateContractResult
                {
                    Success = false,
                    ErrorMessage = "Document appears to be empty or unreadable"
                };
            }

            // ================================================================
            // 3. TRÍCH XUẤT THÔNG TIN TỪ TEXT (như ImportContractFromDocument)
            // ================================================================

            var extracted = ExtractContractInformation(documentText);

            // ================================================================
            // 4. SO SÁNH VÀ TÍNH TOÁN KẾT QUẢ
            // ================================================================

            var differences = new List<ValidationDifference>();
            int totalFields = 0;
            int matchedFields = 0;

            // So sánh từng phần
            var contractInfoComparison = CompareContractInfo(contract, customer, extracted, differences, ref totalFields, ref matchedFields);
            var locationsComparison = CompareLocations(contractLocations, extracted, differences, ref totalFields, ref matchedFields);
            var shiftsComparison = CompareShiftSchedules(shiftSchedules, extracted, differences, ref totalFields, ref matchedFields);
            var holidaysComparison = ComparePublicHolidays(publicHolidays, extracted, differences, ref totalFields, ref matchedFields);

            // Tính % khớp tổng thể
            var matchPercentage = totalFields > 0 ? Math.Round((decimal)matchedFields / totalFields * 100, 2) : 0;

            var summary = new ValidationSummary
            {
                MatchPercentage = matchPercentage,
                TotalFieldsChecked = totalFields,
                MatchedFields = matchedFields,
                MismatchedFields = differences.Count(d => d.Type == "mismatch"),
                MissingInDocument = differences.Count(d => d.Type == "missing"),
                ExtraInDocument = differences.Count(d => d.Type == "extra"),
                ContractInfo = contractInfoComparison,
                Locations = locationsComparison,
                ShiftSchedules = shiftsComparison,
                PublicHolidays = holidaysComparison,
                Differences = differences.OrderByDescending(d => d.Severity)
                                       .ThenBy(d => d.Category)
                                       .ThenBy(d => d.Field)
                                       .ToList()
            };

            logger.LogInformation(
                "Contract validation completed: {MatchPercentage}% match, {TotalDifferences} differences found",
                matchPercentage, differences.Count);

            return new ValidateContractResult
            {
                Success = true,
                Summary = summary
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error validating contract {ContractId}", request.ContractId);
            return new ValidateContractResult
            {
                Success = false,
                ErrorMessage = $"Validation error: {ex.Message}"
            };
        }
    }

    // ================================================================
    // TEXT EXTRACTION METHODS
    // ================================================================

    private string ExtractTextFromDocument(Stream stream, string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        return extension switch
        {
            ".docx" or ".doc" => ExtractFromDocx(stream),
            _ => throw new NotSupportedException($"File type {extension} is not supported. Only PDF and DOCX are allowed.")
        };
    }

    

    private string ExtractFromDocx(Stream stream)
    {
        var sb = new StringBuilder();
        using var doc = WordprocessingDocument.Open(stream, false);

        var body = doc.MainDocumentPart?.Document.Body;
        if (body != null)
        {
            foreach (var paragraph in body.Elements<Paragraph>())
            {
                sb.AppendLine(paragraph.InnerText);
            }
        }

        return sb.ToString();
    }

    // ================================================================
    // INFORMATION EXTRACTION METHODS (Regex-based)
    // ================================================================

    private ExtractedInfo ExtractContractInformation(string text)
    {
        var extracted = new ExtractedInfo();

        // ================================================================
        // SỬ DỤNG ĐÚNG LOGIC TỪ ImportContractFromDocumentHandler
        // ================================================================

        // 1. Contract Number - Sử dụng logic từ ExtractContractNumber
        extracted.ContractNumber = ExtractContractNumber(text);
        logger.LogInformation("📋 Extracted Contract Number: {Number}", extracted.ContractNumber ?? "N/A");

        // 2. Start Date & End Date - Sử dụng logic MỚI từ ExtractDates
        var (startDate, endDate) = ExtractDates(text);
        extracted.StartDate = startDate;
        extracted.EndDate = endDate;
        logger.LogInformation("📅 Extracted Dates: {Start} to {End}",
            startDate?.ToString("dd/MM/yyyy") ?? "N/A",
            endDate?.ToString("dd/MM/yyyy") ?? "N/A");

        // 3. Customer Name - Sử dụng logic từ ExtractCustomerName
        extracted.CustomerName = ExtractCustomerName(text);
        logger.LogInformation("🏢 Extracted Customer Name: {Name}", extracted.CustomerName ?? "N/A");

        // 4. Extract Locations từ ĐIỀU 1
        extracted.Locations = ExtractLocationsFromDieu1(text);
        logger.LogInformation("📍 Extracted {Count} locations", extracted.Locations.Count);

        // 5. Extract Shifts từ ĐIỀU 3
        extracted.Shifts = ExtractShiftsFromDieu3(text);
        logger.LogInformation("⏰ Extracted {Count} shifts", extracted.Shifts.Count);

        // 6. Extract Public Holidays từ ĐIỀU 3.4
        extracted.Holidays = ExtractPublicHolidaysFromDieu3(text, startDate, endDate);
        logger.LogInformation("🎉 Extracted {Count} holidays", extracted.Holidays.Count);

        return extracted;
    }

    // ================================================================
    // LOGIC ĐÚNG TỪ ImportContractFromDocumentHandler
    // ================================================================

    private string? ExtractContractNumber(string text)
    {
        // Pattern 1: "Số: 001/2025/HĐDV-BV/HCM/NVHSV" - Hỗ trợ tiếng Việt và Unicode
        // Capture tất cả ký tự không phải whitespace sau "Số:"
        var pattern1 = @"Số[\s:：]+([^\s\r\n]+(?:\s*/\s*[^\s\r\n]+)*)";
        var match1 = Regex.Match(text, pattern1, RegexOptions.IgnoreCase);
        if (match1.Success && match1.Groups[1].Value.Contains('/'))
        {
            var contractNum = match1.Groups[1].Value.Trim();
            // Clean up: remove trailing punctuation nếu có
            contractNum = Regex.Replace(contractNum, @"[,.\s]+$", "");
            return contractNum;
        }

        // Pattern 2: "Hợp đồng số: ..." với support Unicode
        var pattern2 = @"Hợp\s*đồng\s*(?:số|number)[\s:：]+([^\s\r\n]+)";
        var match2 = Regex.Match(text, pattern2, RegexOptions.IgnoreCase);
        if (match2.Success)
        {
            var contractNum = match2.Groups[1].Value.Trim();
            contractNum = Regex.Replace(contractNum, @"[,.\s]+$", "");
            return contractNum;
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
            }
        }

        DateTime? startDate = null, endDate = null;

        if (allDates.Count >= 2)
        {
            allDates.Sort();
            startDate = allDates.First();
            endDate = allDates.Last();
        }
        else if (allDates.Count == 1)
        {
            startDate = allDates[0];
        }

        return (startDate, endDate);
    }

    private string? ExtractCustomerName(string text)
    {
        // Tìm trong ĐIỀU 1 section - Bên A (Customer)
        var dieu1Match = Regex.Match(text, @"ĐIỀU\s*1[:\.\s]+(.*?)(?=ĐIỀU\s*2|$)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        string searchText = dieu1Match.Success ? dieu1Match.Value : text;

        // Pattern: Tìm sau "Bên A" hoặc "BÊN A" với tên viết hoa
        var patterns = new[]
        {
            @"Bên\s+A[\s:：\-–]+([A-ZÀÁẠẢÃÂẦẤẬẨẪĂẰẮẶẲẴÈÉẸẺẼÊỀẾỆỂỄÌÍỊỈĨÒÓỌỎÕÔỒỐỘỔỖƠỜỚỢỞỠÙÚỤỦŨƯỪỨỰỬỮỲÝỴỶỸĐ\s\.]+?)(?=\n|Địa\s*chỉ|Mã\s*số|Đại\s*diện|Điện\s*thoại|$)",
            @"BÊN\s+A[\s:：\-–]+([A-ZÀÁẠẢÃÂẦẤẬẨẪĂẰẮẶẲẴÈÉẸẺẼÊỀẾỆỂỄÌÍỊỈĨÒÓỌỎÕÔỒỐỘỔỖƠỜỚỢỞỠÙÚỤỦŨƯỪỨỰỬỮỲÝỴỶỸĐ\s\.]+?)(?=\n|Địa\s*chỉ|Mã\s*số|Đại\s*diện|Điện\s*thoại|$)"
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(searchText, pattern, RegexOptions.Multiline);
            if (match.Success)
            {
                var name = match.Groups[1].Value.Trim();
                // Clean up: remove ":" và extra spaces
                name = Regex.Replace(name, @"\s{2,}", " ");
                name = name.TrimEnd(':').Trim();
                return name;
            }
        }

        return null;
    }

    private List<LocationInfo> ExtractLocationsFromDieu1(string text)
    {
        var locations = new List<LocationInfo>();

        // Tìm ĐIỀU 1 section
        var dieu1Match = Regex.Match(text, @"ĐIỀU\s*1[:\.\s]+(.*?)(?=ĐIỀU\s*2|$)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        if (!dieu1Match.Success)
        {
            logger.LogWarning("⚠ ĐIỀU 1 not found for location extraction");
            return locations;
        }

        var dieu1Text = dieu1Match.Value;
        logger.LogInformation("📋 ĐIỀU 1 section length: {Length} chars", dieu1Text.Length);

        // Tìm địa điểm và số lượng bảo vệ RIÊNG BIỆT (vì có thể không cùng 1 dòng)

        // 1. Tìm địa điểm
        var locationPattern = @"Địa\s*điểm[:\s]+([^\n]+?)(?=\n|$)";
        var locationMatch = Regex.Match(dieu1Text, locationPattern, RegexOptions.IgnoreCase);

        string locationName = "";
        if (locationMatch.Success)
        {
            locationName = locationMatch.Groups[1].Value.Trim();
            // Clean up: remove trailing dashes, commas
            locationName = Regex.Replace(locationName, @"[\-,\s]+$", "").Trim();
            logger.LogInformation("  ✓ Found location: {Location}", locationName);
        }

        // 2. Tìm số lượng bảo vệ (có thể ở dòng khác)
        var guardsPatterns = new[]
        {
            // "Số lượng: 05 (năm) nhân viên bảo vệ" - THÊM PATTERN MỚI
            @"Số\s*lượng\s*[:\s]*(\d+)\s*\([^\)]+\)\s*(?:nhân\s*viên\s*)?\s*bảo\s*vệ",
    
            // "Số lượng bảo vệ: 5"
            @"Số\s*lượng\s*(?:nhân\s*viên\s*)?\s*bảo\s*vệ\s*[:\s]*(\d+)",
    
            // "Số lượng: 5 bảo vệ" (không có từ trong ngoặc)
            @"Số\s*lượng\s*[:\s]*(\d+)\s*(?:nhân\s*viên\s*)?\s*bảo\s*vệ",
    
            // "5 bảo vệ"
            @"(\d+)\s*(?:nhân\s*viên\s*)?\s*bảo\s*vệ",
    
            // "bảo vệ: 5"
            @"bảo\s*vệ\s*[:\s]*(\d+)",
    
            // "Guards: 5" or "Guards Required: 5"
            @"(?:Guards|Guard)\s*(?:Required)?\s*[:\s]*(\d+)",
    
            // "5 guards" or "5 guard"
            @"(\d+)\s*(?:guards?|Guards?)"
        };

        int guardsCount = 0;
        foreach (var guardsPattern in guardsPatterns)
        {
            var guardsMatch = Regex.Match(dieu1Text, guardsPattern, RegexOptions.IgnoreCase);
            if (guardsMatch.Success && int.TryParse(guardsMatch.Groups[1].Value, out var guards))
            {
                guardsCount = guards;
                logger.LogInformation("  ✓ Found guards count: {Count} using pattern: {Pattern}", guardsCount, guardsPattern);
                break;
            }
        }

        if (guardsCount == 0)
        {
            logger.LogWarning("  ⚠ Guards count not found or = 0. ĐIỀU 1 text sample:\n{Sample}",
                dieu1Text.Length > 500 ? dieu1Text.Substring(0, 500) : dieu1Text);
        }

        // 3. Thêm location nếu tìm thấy
        if (!string.IsNullOrWhiteSpace(locationName))
        {
            locations.Add(new LocationInfo
            {
                LocationName = locationName,
                GuardsRequired = guardsCount
            });
        }

        return locations;
    }

    private List<ShiftInfo> ExtractShiftsFromDieu3(string text)
    {
        var shifts = new List<ShiftInfo>();

        // Tìm ĐIỀU 3 section
        var dieu3Match = Regex.Match(text, @"ĐIỀU\s*3[:\.\s]+(.*?)(?=ĐIỀU\s*4|$)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        if (!dieu3Match.Success)
        {
            logger.LogWarning("⚠ ĐIỀU 3 not found for shift extraction");
            return shifts;
        }

        var dieu3Text = dieu3Match.Value;

        // Pattern để tìm shifts trong ĐIỀU 3.1
        // Ca sáng: 06h00-14h00 hoặc Ca sáng: 06:00-14:00
        var shiftPattern = @"Ca\s+(sáng|chiều|tối|đêm|[1-3])[:\s]*(\d{1,2})[:h](\d{2})\s*[-–]\s*(\d{1,2})[:h](\d{2})";
        var matches = Regex.Matches(dieu3Text, shiftPattern, RegexOptions.IgnoreCase);

        foreach (Match match in matches)
        {
            var shiftType = match.Groups[1].Value;
            var startHour = int.Parse(match.Groups[2].Value);
            var startMin = int.Parse(match.Groups[3].Value);
            var endHour = int.Parse(match.Groups[4].Value);
            var endMin = int.Parse(match.Groups[5].Value);

            shifts.Add(new ShiftInfo
            {
                ShiftName = $"Ca {shiftType}",
                StartHour = startHour,
                EndHour = endHour,
                StartTime = new TimeSpan(startHour, startMin, 0),
                EndTime = new TimeSpan(endHour, endMin, 0)
            });
        }

        return shifts;
    }

    private List<HolidayInfo> ExtractPublicHolidaysFromDieu3(string text, DateTime? startDate, DateTime? endDate)
    {
        var holidays = new List<HolidayInfo>();

        // Tìm ĐIỀU 3 section
        var dieu3Match = Regex.Match(text, @"ĐIỀU\s*3[:\.\s]+(.*?)(?=ĐIỀU\s*4|$)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        if (!dieu3Match.Success)
        {
            logger.LogWarning("⚠ ĐIỀU 3 not found for holiday extraction");
            return holidays;
        }

        var dieu3Text = dieu3Match.Value;

        // Tìm section 3.4 về ngày lễ
        var section34Match = Regex.Match(dieu3Text, @"3\.4\.?\s+[^\r\n]*(?:Ngày\s*lễ|Tết)", RegexOptions.IgnoreCase);
        if (!section34Match.Success)
        {
            logger.LogWarning("⚠ Section 3.4 (holidays) not found");
            return holidays;
        }

        var section34Start = section34Match.Index;
        var section34 = dieu3Text.Substring(section34Start, Math.Min(3000, dieu3Text.Length - section34Start));

        // 1. Tết Nguyên Đán - ĐÚNG logic từ ImportContractFromDocumentHandler
        var tetPatterns = new[]
        {
            @"Tết\s+Nguy[eê]n\s+[ĐđDd][áaA]n\s+(\d{4})[:\s,]*.*?(\d{1,2}/\d{1,2}/\d{4}).*?(?:đến|[-–])\s*(?:hết\s+)?.*?(\d{1,2}/\d{1,2}/\d{4})",
            @"Tết\s+Nguy[eê]n\s+[ĐđDd][áaA]n[:\s,]*.*?(\d{1,2}/01/\d{4}|\d{1,2}/02/\d{4}).*?(?:đến|[-–])\s*(?:hết\s+)?.*?(\d{1,2}/01/\d{4}|\d{1,2}/02/\d{4})",
            @"Tết\s+(?:âm\s+lịch|Nguy[eê]n\s+[ĐđDd][áaA]n)[:\s,]*.*?(\d{1,2}/\d{1,2}/\d{4}).*?(?:đến|[-–]).*?(\d{1,2}/\d{1,2}/\d{4})"
        };

        bool tetFound = false;
        foreach (var tetPattern in tetPatterns)
        {
            if (tetFound) break;

            var tetMatch = Regex.Match(section34, tetPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (tetMatch.Success)
            {
                // Extract dates from matched groups
                // Skip year-only groups (e.g., "2025") - only parse full date format "dd/MM/yyyy"
                List<DateTime> tetDates = new List<DateTime>();
                for (int i = 1; i < tetMatch.Groups.Count; i++)
                {
                    var groupValue = tetMatch.Groups[i].Value.Trim();

                    // Only process if it contains "/" (date format), skip year-only values
                    if (!string.IsNullOrEmpty(groupValue) &&
                        groupValue.Contains("/") &&
                        DateTime.TryParse(groupValue, out var date))
                    {
                        tetDates.Add(date);
                    }
                }

                if (tetDates.Count >= 2)
                {
                    tetDates.Sort();
                    var tetStart = tetDates.First();
                    var tetEnd = tetDates.Last();
                    var totalDays = (tetEnd - tetStart).Days + 1;

                    int dayNumber = 1;
                    for (var date = tetStart.Date; date <= tetEnd.Date; date = date.AddDays(1))
                    {
                        holidays.Add(new HolidayInfo
                        {
                            HolidayDate = date,
                            HolidayName = dayNumber == 1 ? "Tết Nguyên Đán" : $"Tết Nguyên Đán (Ngày {dayNumber})",
                            IsTetPeriod = true,
                            IsTetHoliday = true,
                            TetDayNumber = dayNumber,
                            HolidayStartDate = tetStart,
                            HolidayEndDate = tetEnd,
                            TotalHolidayDays = totalDays
                        });
                        dayNumber++;
                    }
                    logger.LogInformation("  ✓ Extracted Tết: {Days} days ({Start} - {End})",
                        dayNumber - 1, tetStart.ToString("dd/MM/yyyy"), tetEnd.ToString("dd/MM/yyyy"));
                    tetFound = true;
                    break;
                }
            }
        }

        // 2. Giỗ Tổ Hùng Vương
        var hungVuongPattern = @"Giỗ\s+Tổ\s+Hùng\s+Vương.*?(\d{1,2}/\d{1,2}/\d{4})";
        var hungVuongMatch = Regex.Match(section34, hungVuongPattern, RegexOptions.IgnoreCase);
        if (hungVuongMatch.Success && DateTime.TryParse(hungVuongMatch.Groups[1].Value, out var hungVuongDate))
        {
            holidays.Add(new HolidayInfo
            {
                HolidayDate = hungVuongDate,
                HolidayName = "Giỗ Tổ Hùng Vương"
            });
        }

        // 3. Ngày 30/4 (Giải phóng miền Nam)
        var liberationPattern = @"(?:30/4|Giải\s*phóng).*?(\d{1,2}/04/\d{4})";
        var liberationMatch = Regex.Match(section34, liberationPattern, RegexOptions.IgnoreCase);
        if (liberationMatch.Success && DateTime.TryParseExact(
                liberationMatch.Groups[1].Value,
                new[] { "d/M/yyyy", "dd/MM/yyyy", "d/MM/yyyy", "dd/M/yyyy" },
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out var day304))
        {
            holidays.Add(new HolidayInfo
            {
                HolidayDate = day304,
                HolidayName = "Ngày Giải phóng miền Nam"
            });
        }

        // 4. Ngày 1/5 (Lao động)
        var laborPattern = @"(?:01/5|1/5|Lao\s*động).*?(\d{1,2}/05/\d{4})";
        var laborMatch = Regex.Match(section34, laborPattern, RegexOptions.IgnoreCase);
        if (laborMatch.Success && DateTime.TryParseExact(
                laborMatch.Groups[1].Value,
                new[] { "d/M/yyyy", "dd/MM/yyyy", "d/MM/yyyy", "dd/M/yyyy" },
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out var day015))
        {
            holidays.Add(new HolidayInfo
            {
                HolidayDate = day015,
                HolidayName = "Ngày Quốc tế Lao động"
            });
        }

        // 5. Quốc khánh 2/9
        var nationalDayPattern = @"Quốc\s*khánh.*?(\d{1,2}/09/\d{4})";
        var nationalDayMatch = Regex.Match(section34, nationalDayPattern, RegexOptions.IgnoreCase);
        if (nationalDayMatch.Success && DateTime.TryParse(nationalDayMatch.Groups[1].Value, out var nationalDay))
        {
            holidays.Add(new HolidayInfo
            {
                HolidayDate = nationalDay,
                HolidayName = "Ngày Quốc khánh"
            });
        }

        // 6. Tết Dương lịch (1/1)
        var newYearPattern = @"Tết\s+Dương\s+lịch.*?(\d{1,2}/01/\d{4})";
        var newYearMatch = Regex.Match(section34, newYearPattern, RegexOptions.IgnoreCase);
        if (newYearMatch.Success && DateTime.TryParse(newYearMatch.Groups[1].Value, out var newYearDay))
        {
            holidays.Add(new HolidayInfo
            {
                HolidayDate = newYearDay,
                HolidayName = "Tết Dương lịch"
            });
        }

        return holidays;
    }

    // ================================================================
    // COMPARISON METHODS
    // ================================================================

    private SectionComparison CompareContractInfo(
        Models.Contract contract,
        Models.Customer? customer,
        ExtractedInfo extracted,
        List<ValidationDifference> differences,
        ref int totalFields,
        ref int matchedFields)
    {
        var fields = new List<FieldComparison>();

        // Contract Number
        totalFields++;
        var contractNumberMatch = string.IsNullOrEmpty(extracted.ContractNumber) ||
            contract.ContractNumber.Equals(extracted.ContractNumber, StringComparison.OrdinalIgnoreCase);

        fields.Add(new FieldComparison
        {
            FieldName = "Contract Number",
            DatabaseValue = contract.ContractNumber,
            DocumentValue = extracted.ContractNumber,
            IsMatch = contractNumberMatch,
            Difference = contractNumberMatch ? null : $"DB: {contract.ContractNumber}, Doc: {extracted.ContractNumber}"
        });

        if (contractNumberMatch) matchedFields++;
        else differences.Add(new ValidationDifference
        {
            Category = "Contract Info",
            Field = "Contract Number",
            Type = "mismatch",
            DatabaseValue = contract.ContractNumber,
            DocumentValue = extracted.ContractNumber,
            Description = $"Contract number mismatch: DB={contract.ContractNumber} vs Doc={extracted.ContractNumber}",
            Severity = "high"
        });

        // Start Date
        totalFields++;
        var startDateMatch = !extracted.StartDate.HasValue ||
            contract.StartDate.Date == extracted.StartDate.Value.Date;

        fields.Add(new FieldComparison
        {
            FieldName = "Start Date",
            DatabaseValue = contract.StartDate.ToString("yyyy-MM-dd"),
            DocumentValue = extracted.StartDate?.ToString("yyyy-MM-dd"),
            IsMatch = startDateMatch,
            Difference = startDateMatch ? null : $"DB: {contract.StartDate:yyyy-MM-dd}, Doc: {extracted.StartDate:yyyy-MM-dd}"
        });

        if (startDateMatch) matchedFields++;
        else differences.Add(new ValidationDifference
        {
            Category = "Contract Info",
            Field = "Start Date",
            Type = "mismatch",
            DatabaseValue = contract.StartDate.ToString("yyyy-MM-dd"),
            DocumentValue = extracted.StartDate?.ToString("yyyy-MM-dd"),
            Description = $"Start date mismatch: DB={contract.StartDate:yyyy-MM-dd} vs Doc={extracted.StartDate:yyyy-MM-dd}",
            Severity = "high"
        });

        // End Date
        totalFields++;
        var endDateMatch = !extracted.EndDate.HasValue ||
            contract.EndDate.Date == extracted.EndDate.Value.Date;

        fields.Add(new FieldComparison
        {
            FieldName = "End Date",
            DatabaseValue = contract.EndDate.ToString("yyyy-MM-dd"),
            DocumentValue = extracted.EndDate?.ToString("yyyy-MM-dd"),
            IsMatch = endDateMatch,
            Difference = endDateMatch ? null : $"DB: {contract.EndDate:yyyy-MM-dd}, Doc: {extracted.EndDate:yyyy-MM-dd}"
        });

        if (endDateMatch) matchedFields++;
        else differences.Add(new ValidationDifference
        {
            Category = "Contract Info",
            Field = "End Date",
            Type = "mismatch",
            DatabaseValue = contract.EndDate.ToString("yyyy-MM-dd"),
            DocumentValue = extracted.EndDate?.ToString("yyyy-MM-dd"),
            Description = $"End date mismatch: DB={contract.EndDate:yyyy-MM-dd} vs Doc={extracted.EndDate:yyyy-MM-dd}",
            Severity = "high"
        });

        // Customer Name
        if (customer != null && !string.IsNullOrEmpty(extracted.CustomerName))
        {
            totalFields++;
            var customerNameMatch = customer.CompanyName.Contains(extracted.CustomerName, StringComparison.OrdinalIgnoreCase) ||
                extracted.CustomerName.Contains(customer.CompanyName, StringComparison.OrdinalIgnoreCase);

            fields.Add(new FieldComparison
            {
                FieldName = "Customer Name",
                DatabaseValue = customer.CompanyName,
                DocumentValue = extracted.CustomerName,
                IsMatch = customerNameMatch,
                Difference = customerNameMatch ? null : $"DB: {customer.CompanyName}, Doc: {extracted.CustomerName}"
            });

            if (customerNameMatch) matchedFields++;
            else differences.Add(new ValidationDifference
            {
                Category = "Contract Info",
                Field = "Customer Name",
                Type = "mismatch",
                DatabaseValue = customer.CompanyName,
                DocumentValue = extracted.CustomerName,
                Description = $"Customer name mismatch: DB={customer.CompanyName} vs Doc={extracted.CustomerName}",
                Severity = "medium"
            });
        }

        var sectionMatchedFields = fields.Count(f => f.IsMatch);
        var sectionTotalFields = fields.Count;

        return new SectionComparison
        {
            SectionName = "Contract Info",
            MatchPercentage = sectionTotalFields > 0 ? Math.Round((decimal)sectionMatchedFields / sectionTotalFields * 100, 2) : 0,
            TotalFields = sectionTotalFields,
            MatchedFields = sectionMatchedFields,
            Fields = fields
        };
    }

    private SectionComparison CompareLocations(
        List<dynamic> contractLocations,
        ExtractedInfo extracted,
        List<ValidationDifference> differences,
        ref int totalFields,
        ref int matchedFields)
    {
        var fields = new List<FieldComparison>();

        foreach (var dbLocation in contractLocations)
        {
            string dbLocationName = dbLocation.LocationName;
            int dbGuards = dbLocation.GuardsRequired;

            // Find matching location in document
            var docLocation = extracted.Locations.FirstOrDefault(l =>
                l.LocationName.Contains(dbLocationName, StringComparison.OrdinalIgnoreCase) ||
                dbLocationName.Contains(l.LocationName, StringComparison.OrdinalIgnoreCase));

            // 1. CHECK LOCATION NAME (separate field)
            totalFields++;

            if (docLocation == null)
            {
                // Location missing in document
                fields.Add(new FieldComparison
                {
                    FieldName = $"Location Name: {dbLocationName}",
                    DatabaseValue = dbLocationName,
                    DocumentValue = null,
                    IsMatch = false,
                    Difference = "Missing in document"
                });

                differences.Add(new ValidationDifference
                {
                    Category = "Locations",
                    Field = dbLocationName,
                    Type = "missing",
                    DatabaseValue = dbLocationName,
                    DocumentValue = null,
                    Description = $"Location '{dbLocationName}' found in DB but not in document",
                    Severity = "high"
                });
            }
            else
            {
                // Location name found
                fields.Add(new FieldComparison
                {
                    FieldName = $"Location Name: {dbLocationName}",
                    DatabaseValue = dbLocationName,
                    DocumentValue = docLocation.LocationName,
                    IsMatch = true,
                    Difference = null
                });
                matchedFields++;

                // 2. CHECK GUARDS COUNT (separate field)
                totalFields++;
                var guardsMatch = dbGuards == docLocation.GuardsRequired;

                fields.Add(new FieldComparison
                {
                    FieldName = $"Guards at {dbLocationName}",
                    DatabaseValue = $"{dbGuards} guards",
                    DocumentValue = $"{docLocation.GuardsRequired} guards",
                    IsMatch = guardsMatch,
                    Difference = guardsMatch ? null : $"DB: {dbGuards}, Doc: {docLocation.GuardsRequired}"
                });

                if (guardsMatch)
                {
                    matchedFields++;
                }
                else
                {
                    differences.Add(new ValidationDifference
                    {
                        Category = "Locations",
                        Field = $"{dbLocationName} - Guards Required",
                        Type = "mismatch",
                        DatabaseValue = dbGuards.ToString(),
                        DocumentValue = docLocation.GuardsRequired.ToString(),
                        Description = $"Guards count mismatch at '{dbLocationName}': DB={dbGuards} vs Doc={docLocation.GuardsRequired}",
                        Severity = "medium"
                    });
                }
            }
        }

        // Check for extra locations in document
        foreach (var docLocation in extracted.Locations)
        {
            var foundInDb = contractLocations.Any(db =>
                ((string)db.LocationName).Contains(docLocation.LocationName, StringComparison.OrdinalIgnoreCase) ||
                docLocation.LocationName.Contains((string)db.LocationName, StringComparison.OrdinalIgnoreCase));

            if (!foundInDb)
            {
                differences.Add(new ValidationDifference
                {
                    Category = "Locations",
                    Field = docLocation.LocationName,
                    Type = "extra",
                    DatabaseValue = null,
                    DocumentValue = $"{docLocation.LocationName} ({docLocation.GuardsRequired} guards)",
                    Description = $"Location '{docLocation.LocationName}' found in document but not in DB",
                    Severity = "medium"
                });
            }
        }

        var sectionMatchedFields = fields.Count(f => f.IsMatch);
        var sectionTotalFields = fields.Count;

        return new SectionComparison
        {
            SectionName = "Locations",
            MatchPercentage = sectionTotalFields > 0 ? Math.Round((decimal)sectionMatchedFields / sectionTotalFields * 100, 2) : 0,
            TotalFields = sectionTotalFields,
            MatchedFields = sectionMatchedFields,
            Fields = fields
        };
    }

    private SectionComparison CompareShiftSchedules(
        List<Models.ContractShiftSchedule> shiftSchedules,
        ExtractedInfo extracted,
        List<ValidationDifference> differences,
        ref int totalFields,
        ref int matchedFields)
    {
        var fields = new List<FieldComparison>();

        foreach (var dbShift in shiftSchedules)
        {
            // Find matching shift in document using TimeSpan if available, fallback to hours
            var docShift = extracted.Shifts.FirstOrDefault(s =>
            {
                if (s.StartTime.HasValue && s.EndTime.HasValue)
                {
                    // Compare using TimeSpan (exact match with tolerance)
                    var startDiff = Math.Abs((s.StartTime.Value - dbShift.ShiftStartTime).TotalMinutes);
                    var endDiff = Math.Abs((s.EndTime.Value - dbShift.ShiftEndTime).TotalMinutes);
                    return startDiff <= 30 && endDiff <= 30; // 30 minutes tolerance
                }
                else
                {
                    // Fallback to hours comparison
                    var dbStartHour = dbShift.ShiftStartTime.Hours;
                    var dbEndHour = dbShift.ShiftEndTime.Hours;
                    return Math.Abs(s.StartHour - dbStartHour) <= 1 && Math.Abs(s.EndHour - dbEndHour) <= 1;
                }
            });

            totalFields++;

            if (docShift == null)
            {
                fields.Add(new FieldComparison
                {
                    FieldName = $"Shift: {dbShift.ScheduleName}",
                    DatabaseValue = $"{dbShift.ShiftStartTime:hh\\:mm}-{dbShift.ShiftEndTime:hh\\:mm}",
                    DocumentValue = null,
                    IsMatch = false,
                    Difference = "Missing in document"
                });

                differences.Add(new ValidationDifference
                {
                    Category = "Shift Schedules",
                    Field = dbShift.ScheduleName,
                    Type = "missing",
                    DatabaseValue = $"{dbShift.ShiftStartTime:hh\\:mm}-{dbShift.ShiftEndTime:hh\\:mm}",
                    DocumentValue = null,
                    Description = $"Shift '{dbShift.ScheduleName}' ({dbShift.ShiftStartTime:hh\\:mm}-{dbShift.ShiftEndTime:hh\\:mm}) found in DB but not in document",
                    Severity = "medium"
                });
            }
            else
            {
                var docTimeStr = docShift.StartTime.HasValue && docShift.EndTime.HasValue
                    ? $"{docShift.StartTime:hh\\:mm}-{docShift.EndTime:hh\\:mm}"
                    : $"{docShift.StartHour:00}:00-{docShift.EndHour:00}:00";

                fields.Add(new FieldComparison
                {
                    FieldName = $"Shift: {dbShift.ScheduleName}",
                    DatabaseValue = $"{dbShift.ShiftStartTime:hh\\:mm}-{dbShift.ShiftEndTime:hh\\:mm}",
                    DocumentValue = docTimeStr,
                    IsMatch = true,
                    Difference = null
                });

                matchedFields++;
            }
        }

        var sectionMatchedFields = fields.Count(f => f.IsMatch);
        var sectionTotalFields = fields.Count;

        return new SectionComparison
        {
            SectionName = "Shift Schedules",
            MatchPercentage = sectionTotalFields > 0 ? Math.Round((decimal)sectionMatchedFields / sectionTotalFields * 100, 2) : 0,
            TotalFields = sectionTotalFields,
            MatchedFields = sectionMatchedFields,
            Fields = fields
        };
    }
    
private static string RemoveDiacritics(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;
    
        var normalizedString = text.Normalize(NormalizationForm.FormD);
        var stringBuilder = new StringBuilder(capacity: normalizedString.Length);
    
        foreach (var c in normalizedString)
        {
            var unicodeCategory = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                stringBuilder.Append(c);
            }
        }
    
        return stringBuilder
            .ToString()
            .Normalize(NormalizationForm.FormC);
    }
    

    private SectionComparison ComparePublicHolidays(
        List<Models.PublicHoliday> dbHolidays,
        ExtractedInfo extracted,
        List<ValidationDifference> differences,
        ref int totalFields,
        ref int matchedFields)
    {
        var fields = new List<FieldComparison>();

        // So sánh các holidays quan trọng
        foreach (var dbHoliday in dbHolidays)
        {
            // Bỏ qua Tết Dương lịch nếu không phải IsTetHoliday
            if (dbHoliday.HolidayName.Contains("Tết Dương lịch") && !dbHoliday.IsTetHoliday)
                continue;

            // Tìm holiday tương ứng trong document (match by date hoặc name)
            var docHoliday = extracted.Holidays.FirstOrDefault(h =>
            {
                // CÁCH 1: So sánh chính xác cả ngày + tháng + năm
                if (h.HolidayDate.Day == dbHoliday.HolidayDate.Day &&
                    h.HolidayDate.Month == dbHoliday.HolidayDate.Month &&
                    h.HolidayDate.Year == dbHoliday.HolidayDate.Year)
                    return true;

                // CÁCH 2: Match by name (với tolerance cho Tết)
                if (h.HolidayName.Contains("Tết") && dbHoliday.HolidayName.Contains("Tết") && dbHoliday.IsTetHoliday)
                    return true;

                // CÁCH 3: Normalize tên (bỏ dấu, lowercase) rồi compare
                var normalizedDoc = RemoveDiacritics(h.HolidayName).ToLower();
                var normalizedDb = RemoveDiacritics(dbHoliday.HolidayName).ToLower();
                if (normalizedDoc.Contains(normalizedDb) || normalizedDb.Contains(normalizedDoc))
                    return true;

                return false;
            });

            totalFields++;

            if (docHoliday == null)
            {
                fields.Add(new FieldComparison
                {
                    FieldName = $"Holiday: {dbHoliday.HolidayName}",
                    DatabaseValue = dbHoliday.HolidayDate.ToString("dd/MM/yyyy"),
                    DocumentValue = null,
                    IsMatch = false,
                    Difference = "Missing in document"
                });

                differences.Add(new ValidationDifference
                {
                    Category = "Public Holidays",
                    Field = dbHoliday.HolidayName,
                    Type = "missing",
                    DatabaseValue = dbHoliday.HolidayDate.ToString("dd/MM/yyyy"),
                    DocumentValue = null,
                    Description = $"Holiday '{dbHoliday.HolidayName}' ({dbHoliday.HolidayDate:dd/MM/yyyy}) found in DB but not in document",
                    Severity = dbHoliday.IsTetHoliday ? "high" : "medium"
                });
            }
            else
            {
                // Check if dates match
                var datesMatch = docHoliday.HolidayDate.Date == dbHoliday.HolidayDate.Date;

                fields.Add(new FieldComparison
                {
                    FieldName = $"Holiday: {dbHoliday.HolidayName}",
                    DatabaseValue = dbHoliday.HolidayDate.ToString("dd/MM/yyyy"),
                    DocumentValue = docHoliday.HolidayDate.ToString("dd/MM/yyyy"),
                    IsMatch = datesMatch,
                    Difference = datesMatch ? null : $"DB: {dbHoliday.HolidayDate:dd/MM/yyyy}, Doc: {docHoliday.HolidayDate:dd/MM/yyyy}"
                });

                if (datesMatch)
                    matchedFields++;
                else
                    differences.Add(new ValidationDifference
                    {
                        Category = "Public Holidays",
                        Field = dbHoliday.HolidayName,
                        Type = "mismatch",
                        DatabaseValue = dbHoliday.HolidayDate.ToString("dd/MM/yyyy"),
                        DocumentValue = docHoliday.HolidayDate.ToString("dd/MM/yyyy"),
                        Description = $"Holiday date mismatch for '{dbHoliday.HolidayName}': DB={dbHoliday.HolidayDate:dd/MM/yyyy} vs Doc={docHoliday.HolidayDate:dd/MM/yyyy}",
                        Severity = "medium"
                    });
            }
        }

        // Check for extra holidays in document (not in DB)
        foreach (var docHoliday in extracted.Holidays)
        {
            var existsInDb = dbHolidays.Any(h =>
            {
                // So sánh chính xác Day + Month + Year thay vì Date (tránh timezone issues)
                if (h.HolidayDate.Day == docHoliday.HolidayDate.Day &&
                    h.HolidayDate.Month == docHoliday.HolidayDate.Month &&
                    h.HolidayDate.Year == docHoliday.HolidayDate.Year)
                    return true;

                // Match by normalized name
                var normalizedDoc = RemoveDiacritics(docHoliday.HolidayName).ToLower();
                var normalizedDb = RemoveDiacritics(h.HolidayName).ToLower();
                return normalizedDoc.Contains(normalizedDb) || normalizedDb.Contains(normalizedDoc);
            });

            if (!existsInDb)
            {
                differences.Add(new ValidationDifference
                {
                    Category = "Public Holidays",
                    Field = docHoliday.HolidayName,
                    Type = "extra",
                    DatabaseValue = null,
                    DocumentValue = docHoliday.HolidayDate.ToString("dd/MM/yyyy"),
                    Description = $"Holiday '{docHoliday.HolidayName}' ({docHoliday.HolidayDate:dd/MM/yyyy}) found in document but not in DB",
                    Severity = "low"
                });
            }
        }


        var sectionMatchedFields = fields.Count(f => f.IsMatch);
        var sectionTotalFields = fields.Count;

        return new SectionComparison
        {
            SectionName = "Public Holidays",
            MatchPercentage = sectionTotalFields > 0 ? Math.Round((decimal)sectionMatchedFields / sectionTotalFields * 100, 2) : 0,
            TotalFields = sectionTotalFields,
            MatchedFields = sectionMatchedFields,
            Fields = fields
        };
    }

    // ================================================================
    // INTERNAL DATA STRUCTURES FOR EXTRACTED INFORMATION
    // ================================================================

    private class ExtractedInfo
    {
        public string? ContractNumber { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string? CustomerName { get; set; }
        public List<LocationInfo> Locations { get; set; } = new();
        public List<ShiftInfo> Shifts { get; set; } = new();
        public List<HolidayInfo> Holidays { get; set; } = new();
    }

    private class LocationInfo
    {
        public string LocationName { get; set; } = string.Empty;
        public int GuardsRequired { get; set; }
    }

    private class ShiftInfo
    {
        public string ShiftName { get; set; } = string.Empty;
        public int StartHour { get; set; }
        public int EndHour { get; set; }
        public int? GuardsPerShift { get; set; }
        public TimeSpan? StartTime { get; set; }
        public TimeSpan? EndTime { get; set; }
    }

    private class HolidayInfo
    {
        public DateTime HolidayDate { get; set; }
        public string HolidayName { get; set; } = string.Empty;
        public bool IsTetPeriod { get; set; } = false;
        public bool IsTetHoliday { get; set; } = false;
        public int? TetDayNumber { get; set; }
        public DateTime? HolidayStartDate { get; set; }
        public DateTime? HolidayEndDate { get; set; }
        public int? TotalHolidayDays { get; set; }
    }

}
