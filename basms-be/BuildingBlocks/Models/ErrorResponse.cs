namespace BuildingBlocks.Models;

/// <summary>
/// Standardized error response model for all APIs
/// </summary>
public class ErrorResponse
{
    /// <summary>
    /// HTTP status code
    /// </summary>
    public int StatusCode { get; set; }

    /// <summary>
    /// Error message for end users
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Specific error code for client-side handling
    /// </summary>
    public string ErrorCode { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the error occurred
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Request path that caused the error
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Additional error details (only in development environment)
    /// </summary>
    public object? Details { get; set; }

    /// <summary>
    /// Validation errors (for 400 Bad Request with validation failures)
    /// </summary>
    public Dictionary<string, string[]>? ValidationErrors { get; set; }

    /// <summary>
    /// Stack trace (only in development environment)
    /// </summary>
    public string? StackTrace { get; set; }

    /// <summary>
    /// Inner exception message (only in development environment)
    /// </summary>
    public string? InnerException { get; set; }
}
