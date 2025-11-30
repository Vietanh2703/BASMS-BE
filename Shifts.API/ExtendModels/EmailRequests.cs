namespace Shifts.API.ExtendModels;

/// <summary>
/// DTO cho email request
/// </summary>
public class EmailRequests
{
    /// <summary>
    /// Email người nhận
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Tiêu đề email
    /// </summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>
    /// Nội dung HTML của email
    /// </summary>
    public string EmailBody { get; set; } = string.Empty;
}
