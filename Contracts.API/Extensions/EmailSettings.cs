namespace Contracts.API.Extensions;

public class EmailSettings
{
    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; }
    public string Sender { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
