namespace BuildingBlocks.Exceptions;

/// <summary>
/// Exception for validation errors (HTTP 400)
/// Used when input validation fails
/// </summary>
public class ValidationException : BaseException
{
    /// <summary>
    /// Dictionary of field names and their validation errors
    /// </summary>
    public Dictionary<string, string[]> ValidationErrors { get; }

    public ValidationException(Dictionary<string, string[]> validationErrors, string errorCode = "VALIDATION_ERROR")
        : base("One or more validation errors occurred", errorCode)
    {
        ValidationErrors = validationErrors;
    }

    public ValidationException(string fieldName, string errorMessage, string errorCode = "VALIDATION_ERROR")
        : base("One or more validation errors occurred", errorCode)
    {
        ValidationErrors = new Dictionary<string, string[]>
        {
            { fieldName, new[] { errorMessage } }
        };
    }
}
