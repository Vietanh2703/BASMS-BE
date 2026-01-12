namespace Shifts.API.Utilities;

/// <summary>
/// Helper class for date validation operations
/// </summary>
public static class DateValidationHelper
{
    /// <summary>
    /// Validates that a date is after today (must be future only)
    /// </summary>
    /// <param name="dateToValidate">The date to validate</param>
    /// <param name="fieldName">Name of the field being validated (for error messages)</param>
    /// <returns>Validation result with IsValid flag and error message</returns>
    public static DateValidationResult ValidateDateNotInPast(DateTime dateToValidate, string fieldName = "Date")
    {
        var today = DateTime.UtcNow.Date;
        var dateValue = dateToValidate.Date;

        if (dateValue <= today)
        {
            return new DateValidationResult
            {
                IsValid = false,
                ErrorMessage = $"{fieldName} {dateValue:yyyy-MM-dd} phải sau ngày hôm nay ({today:yyyy-MM-dd}). " +
                              $"Không thể tạo với ngày trong quá khứ hoặc hôm nay.",
                Today = today,
                ValidatedDate = dateValue
            };
        }

        return new DateValidationResult
        {
            IsValid = true,
            Today = today,
            ValidatedDate = dateValue
        };
    }

    /// <summary>
    /// Validates that EffectiveFrom date is not in the past
    /// </summary>
    public static DateValidationResult ValidateEffectiveFrom(DateTime effectiveFrom)
    {
        return ValidateDateNotInPast(effectiveFrom, "EffectiveFrom");
    }

    /// <summary>
    /// Validates that ShiftDate is not in the past
    /// </summary>
    public static DateValidationResult ValidateShiftDate(DateTime shiftDate)
    {
        return ValidateDateNotInPast(shiftDate, "ShiftDate");
    }

    /// <summary>
    /// Validates that GenerateFromDate is not in the past
    /// </summary>
    public static DateValidationResult ValidateGenerateFromDate(DateTime generateFromDate)
    {
        return ValidateDateNotInPast(generateFromDate, "GenerateFromDate");
    }

    /// <summary>
    /// Validates that end date is not before start date
    /// </summary>
    public static DateValidationResult ValidateDateRange(DateTime startDate, DateTime? endDate, string startFieldName = "StartDate", string endFieldName = "EndDate")
    {
        if (!endDate.HasValue)
        {
            return new DateValidationResult
            {
                IsValid = true,
                ValidatedDate = startDate.Date
            };
        }

        if (endDate.Value.Date < startDate.Date)
        {
            return new DateValidationResult
            {
                IsValid = false,
                ErrorMessage = $"{endFieldName} {endDate.Value:yyyy-MM-dd} không thể trước {startFieldName} {startDate:yyyy-MM-dd}",
                ValidatedDate = startDate.Date
            };
        }

        return new DateValidationResult
        {
            IsValid = true,
            ValidatedDate = startDate.Date
        };
    }

    /// <summary>
    /// Gets today's date (UTC)
    /// </summary>
    public static DateTime GetToday() => DateTime.UtcNow.Date;
}

/// <summary>
/// Result of date validation
/// </summary>
public class DateValidationResult
{
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime? Today { get; set; }
    public DateTime? ValidatedDate { get; set; }
}
