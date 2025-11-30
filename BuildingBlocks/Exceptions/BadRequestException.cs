namespace BuildingBlocks.Exceptions;

/// <summary>
/// Exception for bad request errors (HTTP 400)
/// Used when client sends invalid data or malformed requests
/// </summary>
public class BadRequestException : BaseException
{
    public BadRequestException(string message, string errorCode = "BAD_REQUEST", Dictionary<string, object>? details = null)
        : base(message, errorCode, details)
    {
    }

    public BadRequestException(string message, Exception innerException, string errorCode = "BAD_REQUEST", Dictionary<string, object>? details = null)
        : base(message, errorCode, innerException, details)
    {
    }
}
