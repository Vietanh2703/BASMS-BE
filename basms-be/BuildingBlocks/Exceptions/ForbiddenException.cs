namespace BuildingBlocks.Exceptions;

/// <summary>
/// Exception for forbidden access (HTTP 403)
/// Used when user is authenticated but doesn't have permission to access resource
/// </summary>
public class ForbiddenException : BaseException
{
    public ForbiddenException(string message, string errorCode = "FORBIDDEN", Dictionary<string, object>? details = null)
        : base(message, errorCode, details)
    {
    }

    public ForbiddenException(string message, Exception innerException, string errorCode = "FORBIDDEN", Dictionary<string, object>? details = null)
        : base(message, errorCode, innerException, details)
    {
    }
}
