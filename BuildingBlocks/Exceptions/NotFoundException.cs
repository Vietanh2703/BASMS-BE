namespace BuildingBlocks.Exceptions;

/// <summary>
/// Exception for resource not found (HTTP 404)
/// Used when requested resource doesn't exist
/// </summary>
public class NotFoundException : BaseException
{
    public NotFoundException(string message, string errorCode = "NOT_FOUND", Dictionary<string, object>? details = null)
        : base(message, errorCode, details)
    {
    }

    public NotFoundException(string message, Exception innerException, string errorCode = "NOT_FOUND", Dictionary<string, object>? details = null)
        : base(message, errorCode, innerException, details)
    {
    }
}
