namespace Shifts.API.ExtendModels;

public class EmailSettings
{
    public string Sender { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; }
}
