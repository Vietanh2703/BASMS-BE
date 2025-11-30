namespace BuildingBlocks.Exceptions;

/// <summary>
/// Base exception class for all custom exceptions in the application
/// </summary>
public abstract class BaseException : Exception
{
    /// <summary>
    /// Error code for identifying specific error types
    /// </summary>
    public string ErrorCode { get; }

    /// <summary>
    /// Additional details about the error
    /// </summary>
    public Dictionary<string, object>? Details { get; }

    protected BaseException(string message, string errorCode, Dictionary<string, object>? details = null)
        : base(message)
    {
        ErrorCode = errorCode;
        Details = details;
    }

    protected BaseException(string message, string errorCode, Exception innerException, Dictionary<string, object>? details = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
        Details = details;
    }
}
