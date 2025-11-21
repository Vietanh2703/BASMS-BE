namespace BuildingBlocks.Exceptions;

/// <summary>
/// Exception for conflict errors (HTTP 409)
/// Used when request conflicts with current state (e.g., duplicate resource)
/// </summary>
public class ConflictException : BaseException
{
    public ConflictException(string message, string errorCode = "CONFLICT", Dictionary<string, object>? details = null)
        : base(message, errorCode, details)
    {
    }

    public ConflictException(string message, Exception innerException, string errorCode = "CONFLICT", Dictionary<string, object>? details = null)
        : base(message, errorCode, innerException, details)
    {
    }
}
