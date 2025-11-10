using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using UglyToad.PdfPig;
using System.Text;
using System.Text.RegularExpressions;

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

            var customer = await connection.QueryFirstOrDefaultAsync<Models.Customer>(
                "SELECT * FROM customers WHERE Id = @CustomerId",
                new { CustomerId = contract.CustomerId });

            var contractLocations = (await connection.QueryAsync<dynamic>(
                @"SELECT cl.*, loc.LocationName, loc.Address, loc.City, loc.District
                  FROM contract_locations cl
                  INNER JOIN customer_locations loc ON cl.LocationId = loc.Id
                  WHERE cl.ContractId = @ContractId AND cl.IsDeleted = 0",
                new { ContractId = contract.Id })).ToList();

            var shiftSchedules = (await connection.QueryAsync<Models.ContractShiftSchedule>(
                "SELECT * FROM contract_shift_schedules WHERE ContractId = @ContractId AND IsDeleted = 0",
                new { ContractId = contract.Id })).ToList();

            var workingConditions = await connection.QueryFirstOrDefaultAsync<Models.ContractWorkingConditions>(
                "SELECT * FROM contract_working_conditions WHERE ContractId = @ContractId AND IsActive = 1",
                new { ContractId = contract.Id });

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
            var conditionsComparison = CompareWorkingConditions(workingConditions, extracted, differences, ref totalFields, ref matchedFields);

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
                WorkingConditions = conditionsComparison,
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
            ".pdf" => ExtractFromPdf(stream),
            ".docx" or ".doc" => ExtractFromDocx(stream),
            _ => throw new NotSupportedException($"File type {extension} is not supported. Only PDF and DOCX are allowed.")
        };
    }

    private string ExtractFromPdf(Stream stream)
    {
        var sb = new StringBuilder();
        using var pdfDocument = UglyToad.PdfPig.PdfDocument.Open(stream);

        foreach (var page in pdfDocument.GetPages())
        {
            sb.AppendLine(page.Text);
        }

        return sb.ToString();
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

        // Contract Number
        var contractNumberMatch = Regex.Match(text,
            @"(?:Số\s*hợp\s*đồng|Hợp\s*đồng\s*số)[\s:：]*([A-Z0-9\-\/]+)",
            RegexOptions.IgnoreCase);
        if (contractNumberMatch.Success)
            extracted.ContractNumber = contractNumberMatch.Groups[1].Value.Trim();

        // Start Date
        var startDateMatch = Regex.Match(text,
            @"(?:Ngày\s*bắt\s*đầu|hiệu\s*lực\s*từ\s*ngày)[\s:：]*(\d{1,2}[\/\-]\d{1,2}[\/\-]\d{4})",
            RegexOptions.IgnoreCase);
        if (startDateMatch.Success && DateTime.TryParse(startDateMatch.Groups[1].Value, out var startDate))
            extracted.StartDate = startDate;

        // End Date
        var endDateMatch = Regex.Match(text,
            @"(?:Ngày\s*kết\s*thúc|đến\s*ngày)[\s:：]*(\d{1,2}[\/\-]\d{1,2}[\/\-]\d{4})",
            RegexOptions.IgnoreCase);
        if (endDateMatch.Success && DateTime.TryParse(endDateMatch.Groups[1].Value, out var endDate))
            extracted.EndDate = endDate;

        // Customer Name
        var customerMatch = Regex.Match(text,
            @"(?:Bên\s*A|Khách\s*hàng|Customer)[\s:：]+([^\n]+?)(?:Địa\s*chỉ|,|\n)",
            RegexOptions.IgnoreCase);
        if (customerMatch.Success)
            extracted.CustomerName = customerMatch.Groups[1].Value.Trim();

        // Extract Locations
        extracted.Locations = ExtractLocations(text);

        // Extract Shifts
        extracted.Shifts = ExtractShifts(text);

        // Extract Working Conditions
        extracted.WorkingConditions = ExtractWorkingConditions(text);

        return extracted;
    }

    private List<LocationInfo> ExtractLocations(string text)
    {
        var locations = new List<LocationInfo>();

        var locationPattern = @"(?:Địa\s*điểm|Chi\s*nhánh)\s*\d*[\s:：]+([^\n]+?)(?:-|,)?\s*(?:Số\s*(?:lượng\s*)?bảo\s*vệ|guards?)[\s:：]*(\d+)";
        var matches = Regex.Matches(text, locationPattern, RegexOptions.IgnoreCase);

        foreach (Match match in matches)
        {
            locations.Add(new LocationInfo
            {
                LocationName = match.Groups[1].Value.Trim(),
                GuardsRequired = int.Parse(match.Groups[2].Value)
            });
        }

        return locations;
    }

    private List<ShiftInfo> ExtractShifts(string text)
    {
        var shifts = new List<ShiftInfo>();

        var shiftPattern = @"(?:Ca\s*(?:sáng|chiều|tối|đêm|[1-3]))[\s:：]*(\d{1,2})h?[-–](\d{1,2})h?(?:.*?(\d+)\s*(?:bảo\s*vệ|guards?))?";
        var matches = Regex.Matches(text, shiftPattern, RegexOptions.IgnoreCase);

        foreach (Match match in matches)
        {
            var shift = new ShiftInfo
            {
                ShiftName = match.Value.Split(':')[0].Trim(),
                StartHour = int.Parse(match.Groups[1].Value),
                EndHour = int.Parse(match.Groups[2].Value)
            };

            if (match.Groups[3].Success && int.TryParse(match.Groups[3].Value, out var guards))
                shift.GuardsPerShift = guards;

            shifts.Add(shift);
        }

        return shifts;
    }

    private WorkingConditionsInfo ExtractWorkingConditions(string text)
    {
        var wc = new WorkingConditionsInfo();

        // Max overtime hours per day
        var maxOvertimeDayMatch = Regex.Match(text, @"(?:tăng\s*ca\s*tối\s*đa|max.*overtime).*?(\d+)\s*(?:giờ|h|hours?)", RegexOptions.IgnoreCase);
        if (maxOvertimeDayMatch.Success && decimal.TryParse(maxOvertimeDayMatch.Groups[1].Value, out var maxOvertimeDay))
            wc.MaxOvertimeHoursPerDay = maxOvertimeDay;

        // Max overtime hours per month
        var maxOvertimeMonthMatch = Regex.Match(text, @"(?:tăng\s*ca.*?tháng|overtime.*?month).*?(\d+)\s*(?:giờ|h|hours?)", RegexOptions.IgnoreCase);
        if (maxOvertimeMonthMatch.Success && decimal.TryParse(maxOvertimeMonthMatch.Groups[1].Value, out var maxOvertimeMonth))
            wc.MaxOvertimeHoursPerMonth = maxOvertimeMonth;

        // Night shift time
        var nightTimeMatch = Regex.Match(text, @"(?:ca\s*đêm|night\s*shift).*?(\d{1,2})[h:]?00?\s*[-–]\s*(\d{1,2})[h:]?00?", RegexOptions.IgnoreCase);
        if (nightTimeMatch.Success)
        {
            if (int.TryParse(nightTimeMatch.Groups[1].Value, out var startHour))
                wc.NightShiftStartTime = new TimeSpan(startHour, 0, 0);
            if (int.TryParse(nightTimeMatch.Groups[2].Value, out var endHour))
                wc.NightShiftEndTime = new TimeSpan(endHour, 0, 0);
        }

        // Standard hours per day
        var standardHoursMatch = Regex.Match(text, @"(?:làm\s*việc|working).*?(\d+)\s*(?:giờ|h|hours?)\s*(?:\/|per\s*)?(?:ngày|day)", RegexOptions.IgnoreCase);
        if (standardHoursMatch.Success && decimal.TryParse(standardHoursMatch.Groups[1].Value, out var standardHours))
            wc.StandardHoursPerDay = standardHours;

        // Annual leave days
        var annualLeaveMatch = Regex.Match(text, @"(?:nghỉ\s*phép|annual\s*leave).*?(\d+)\s*(?:ngày|days?)", RegexOptions.IgnoreCase);
        if (annualLeaveMatch.Success && int.TryParse(annualLeaveMatch.Groups[1].Value, out var annualLeave))
            wc.AnnualLeaveDays = annualLeave;

        // Minimum rest hours between shifts
        var restHoursMatch = Regex.Match(text, @"(?:nghỉ.*?giữa.*?ca|rest.*?between.*?shift).*?(\d+)\s*(?:giờ|h|hours?)", RegexOptions.IgnoreCase);
        if (restHoursMatch.Success && decimal.TryParse(restHoursMatch.Groups[1].Value, out var restHours))
            wc.MinimumRestHoursBetweenShifts = restHours;

        return wc;
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

            totalFields++;

            if (docLocation == null)
            {
                // Location missing in document
                fields.Add(new FieldComparison
                {
                    FieldName = $"Location: {dbLocationName}",
                    DatabaseValue = $"{dbLocationName} ({dbGuards} guards)",
                    DocumentValue = null,
                    IsMatch = false,
                    Difference = "Missing in document"
                });

                differences.Add(new ValidationDifference
                {
                    Category = "Locations",
                    Field = dbLocationName,
                    Type = "missing",
                    DatabaseValue = $"{dbLocationName} ({dbGuards} guards)",
                    DocumentValue = null,
                    Description = $"Location '{dbLocationName}' found in DB but not in document",
                    Severity = "high"
                });
            }
            else
            {
                // Check guards count
                var guardsMatch = dbGuards == docLocation.GuardsRequired;

                fields.Add(new FieldComparison
                {
                    FieldName = $"Location: {dbLocationName}",
                    DatabaseValue = $"{dbGuards} guards",
                    DocumentValue = $"{docLocation.GuardsRequired} guards",
                    IsMatch = guardsMatch,
                    Difference = guardsMatch ? null : $"DB: {dbGuards}, Doc: {docLocation.GuardsRequired}"
                });

                if (guardsMatch) matchedFields++;
                else differences.Add(new ValidationDifference
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
            var dbStartHour = dbShift.ShiftStartTime.Hours;
            var dbEndHour = dbShift.ShiftEndTime.Hours;

            // Find matching shift in document
            var docShift = extracted.Shifts.FirstOrDefault(s =>
                Math.Abs(s.StartHour - dbStartHour) <= 1 && Math.Abs(s.EndHour - dbEndHour) <= 1);

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
                fields.Add(new FieldComparison
                {
                    FieldName = $"Shift: {dbShift.ScheduleName}",
                    DatabaseValue = $"{dbShift.ShiftStartTime:hh\\:mm}-{dbShift.ShiftEndTime:hh\\:mm}",
                    DocumentValue = $"{docShift.StartHour:00}:00-{docShift.EndHour:00}:00",
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

    private SectionComparison CompareWorkingConditions(
        Models.ContractWorkingConditions? workingConditions,
        ExtractedInfo extracted,
        List<ValidationDifference> differences,
        ref int totalFields,
        ref int matchedFields)
    {
        var fields = new List<FieldComparison>();

        if (workingConditions == null)
        {
            return new SectionComparison
            {
                SectionName = "Working Conditions",
                MatchPercentage = 0,
                TotalFields = 0,
                MatchedFields = 0,
                Fields = fields
            };
        }

        var wc = workingConditions;
        var docWc = extracted.WorkingConditions;

        // Max Overtime Hours Per Day
        if (wc.MaxOvertimeHoursPerDay.HasValue && docWc.MaxOvertimeHoursPerDay.HasValue)
        {
            totalFields++;
            var match = wc.MaxOvertimeHoursPerDay.Value == docWc.MaxOvertimeHoursPerDay.Value;

            fields.Add(new FieldComparison
            {
                FieldName = "Max Overtime Hours Per Day",
                DatabaseValue = $"{wc.MaxOvertimeHoursPerDay}h",
                DocumentValue = $"{docWc.MaxOvertimeHoursPerDay}h",
                IsMatch = match,
                Difference = match ? null : $"DB: {wc.MaxOvertimeHoursPerDay}h, Doc: {docWc.MaxOvertimeHoursPerDay}h"
            });

            if (match) matchedFields++;
            else differences.Add(new ValidationDifference
            {
                Category = "Working Conditions",
                Field = "Max Overtime Hours Per Day",
                Type = "mismatch",
                DatabaseValue = $"{wc.MaxOvertimeHoursPerDay}h",
                DocumentValue = $"{docWc.MaxOvertimeHoursPerDay}h",
                Description = $"Max overtime hours per day mismatch: DB={wc.MaxOvertimeHoursPerDay}h vs Doc={docWc.MaxOvertimeHoursPerDay}h",
                Severity = "medium"
            });
        }

        // Max Overtime Hours Per Month
        if (wc.MaxOvertimeHoursPerMonth.HasValue && docWc.MaxOvertimeHoursPerMonth.HasValue)
        {
            totalFields++;
            var match = wc.MaxOvertimeHoursPerMonth.Value == docWc.MaxOvertimeHoursPerMonth.Value;

            fields.Add(new FieldComparison
            {
                FieldName = "Max Overtime Hours Per Month",
                DatabaseValue = $"{wc.MaxOvertimeHoursPerMonth}h",
                DocumentValue = $"{docWc.MaxOvertimeHoursPerMonth}h",
                IsMatch = match,
                Difference = match ? null : $"DB: {wc.MaxOvertimeHoursPerMonth}h, Doc: {docWc.MaxOvertimeHoursPerMonth}h"
            });

            if (match) matchedFields++;
            else differences.Add(new ValidationDifference
            {
                Category = "Working Conditions",
                Field = "Max Overtime Hours Per Month",
                Type = "mismatch",
                DatabaseValue = $"{wc.MaxOvertimeHoursPerMonth}h",
                DocumentValue = $"{docWc.MaxOvertimeHoursPerMonth}h",
                Description = $"Max overtime hours per month mismatch: DB={wc.MaxOvertimeHoursPerMonth}h vs Doc={docWc.MaxOvertimeHoursPerMonth}h",
                Severity = "medium"
            });
        }

        // Night Shift Start Time
        if (wc.NightShiftStartTime.HasValue && docWc.NightShiftStartTime.HasValue)
        {
            totalFields++;
            var match = wc.NightShiftStartTime.Value == docWc.NightShiftStartTime.Value;

            fields.Add(new FieldComparison
            {
                FieldName = "Night Shift Start Time",
                DatabaseValue = wc.NightShiftStartTime.Value.ToString(@"hh\:mm"),
                DocumentValue = docWc.NightShiftStartTime.Value.ToString(@"hh\:mm"),
                IsMatch = match,
                Difference = match ? null : $"DB: {wc.NightShiftStartTime:hh\\:mm}, Doc: {docWc.NightShiftStartTime:hh\\:mm}"
            });

            if (match) matchedFields++;
            else differences.Add(new ValidationDifference
            {
                Category = "Working Conditions",
                Field = "Night Shift Start Time",
                Type = "mismatch",
                DatabaseValue = wc.NightShiftStartTime.Value.ToString(@"hh\:mm"),
                DocumentValue = docWc.NightShiftStartTime.Value.ToString(@"hh\:mm"),
                Description = $"Night shift start time mismatch: DB={wc.NightShiftStartTime:hh\\:mm} vs Doc={docWc.NightShiftStartTime:hh\\:mm}",
                Severity = "medium"
            });
        }

        // Standard Hours Per Day
        if (wc.StandardHoursPerDay.HasValue && docWc.StandardHoursPerDay.HasValue)
        {
            totalFields++;
            var match = wc.StandardHoursPerDay.Value == docWc.StandardHoursPerDay.Value;

            fields.Add(new FieldComparison
            {
                FieldName = "Standard Hours Per Day",
                DatabaseValue = $"{wc.StandardHoursPerDay}h",
                DocumentValue = $"{docWc.StandardHoursPerDay}h",
                IsMatch = match,
                Difference = match ? null : $"DB: {wc.StandardHoursPerDay}h, Doc: {docWc.StandardHoursPerDay}h"
            });

            if (match) matchedFields++;
            else differences.Add(new ValidationDifference
            {
                Category = "Working Conditions",
                Field = "Standard Hours Per Day",
                Type = "mismatch",
                DatabaseValue = $"{wc.StandardHoursPerDay}h",
                DocumentValue = $"{docWc.StandardHoursPerDay}h",
                Description = $"Standard hours per day mismatch: DB={wc.StandardHoursPerDay}h vs Doc={docWc.StandardHoursPerDay}h",
                Severity = "medium"
            });
        }

        // Annual Leave Days
        if (wc.AnnualLeaveDays.HasValue && docWc.AnnualLeaveDays.HasValue)
        {
            totalFields++;
            var match = wc.AnnualLeaveDays.Value == docWc.AnnualLeaveDays.Value;

            fields.Add(new FieldComparison
            {
                FieldName = "Annual Leave Days",
                DatabaseValue = $"{wc.AnnualLeaveDays} days",
                DocumentValue = $"{docWc.AnnualLeaveDays} days",
                IsMatch = match,
                Difference = match ? null : $"DB: {wc.AnnualLeaveDays} days, Doc: {docWc.AnnualLeaveDays} days"
            });

            if (match) matchedFields++;
            else differences.Add(new ValidationDifference
            {
                Category = "Working Conditions",
                Field = "Annual Leave Days",
                Type = "mismatch",
                DatabaseValue = $"{wc.AnnualLeaveDays} days",
                DocumentValue = $"{docWc.AnnualLeaveDays} days",
                Description = $"Annual leave days mismatch: DB={wc.AnnualLeaveDays} vs Doc={docWc.AnnualLeaveDays}",
                Severity = "low"
            });
        }

        // Minimum Rest Hours Between Shifts
        if (wc.MinimumRestHoursBetweenShifts.HasValue && docWc.MinimumRestHoursBetweenShifts.HasValue)
        {
            totalFields++;
            var match = wc.MinimumRestHoursBetweenShifts.Value == docWc.MinimumRestHoursBetweenShifts.Value;

            fields.Add(new FieldComparison
            {
                FieldName = "Minimum Rest Hours Between Shifts",
                DatabaseValue = $"{wc.MinimumRestHoursBetweenShifts}h",
                DocumentValue = $"{docWc.MinimumRestHoursBetweenShifts}h",
                IsMatch = match,
                Difference = match ? null : $"DB: {wc.MinimumRestHoursBetweenShifts}h, Doc: {docWc.MinimumRestHoursBetweenShifts}h"
            });

            if (match) matchedFields++;
            else differences.Add(new ValidationDifference
            {
                Category = "Working Conditions",
                Field = "Minimum Rest Hours Between Shifts",
                Type = "mismatch",
                DatabaseValue = $"{wc.MinimumRestHoursBetweenShifts}h",
                DocumentValue = $"{docWc.MinimumRestHoursBetweenShifts}h",
                Description = $"Minimum rest hours mismatch: DB={wc.MinimumRestHoursBetweenShifts}h vs Doc={docWc.MinimumRestHoursBetweenShifts}h",
                Severity = "high"
            });
        }

        var sectionMatchedFields = fields.Count(f => f.IsMatch);
        var sectionTotalFields = fields.Count;

        return new SectionComparison
        {
            SectionName = "Working Conditions",
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
        public WorkingConditionsInfo WorkingConditions { get; set; } = new();
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
    }

    private class WorkingConditionsInfo
    {
        public decimal? MaxOvertimeHoursPerDay { get; set; }
        public decimal? MaxOvertimeHoursPerMonth { get; set; }
        public TimeSpan? NightShiftStartTime { get; set; }
        public TimeSpan? NightShiftEndTime { get; set; }
        public decimal? StandardHoursPerDay { get; set; }
        public int? AnnualLeaveDays { get; set; }
        public decimal? MinimumRestHoursBetweenShifts { get; set; }
    }
}
