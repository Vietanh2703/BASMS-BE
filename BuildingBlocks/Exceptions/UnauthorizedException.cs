namespace BuildingBlocks.Exceptions;

/// <summary>
/// Exception for unauthorized access (HTTP 401)
/// Used when authentication fails or credentials are invalid
/// </summary>
public class UnauthorizedException : BaseException
{
    public UnauthorizedException(string message, string errorCode = "UNAUTHORIZED", Dictionary<string, object>? details = null)
        : base(message, errorCode, details)
    {
    }

    public UnauthorizedException(string message, Exception innerException, string errorCode = "UNAUTHORIZED", Dictionary<string, object>? details = null)
        : base(message, errorCode, innerException, details)
    {
    }
}
