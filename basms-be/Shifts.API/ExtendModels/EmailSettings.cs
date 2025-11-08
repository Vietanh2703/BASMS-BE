namespace Shifts.API.ExtendModels;

/// <summary>
/// Cấu hình SMTP email settings
/// </summary>
public class EmailSettings
{
    /// <summary>
    /// Email gửi đi (Gmail account)
    /// </summary>
    public string Sender { get; set; } = string.Empty;

    /// <summary>
    /// App password của Gmail (không phải password thường)
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// SMTP host (smtp.gmail.com)
    /// </summary>
    public string SmtpHost { get; set; } = string.Empty;

    /// <summary>
    /// SMTP port (587 cho StartTls)
    /// </summary>
    public int SmtpPort { get; set; }
}
