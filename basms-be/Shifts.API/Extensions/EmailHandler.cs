using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using Shifts.API.ExtendModels;

namespace Shifts.API.Extensions;

public class EmailHandler
{
    private readonly EmailSettings _emailSettings;
    private readonly ILogger<EmailHandler> _logger;

    public EmailHandler(IOptions<EmailSettings> emailSettings, ILogger<EmailHandler> logger)
    {
        _emailSettings = emailSettings.Value;
        _logger = logger;
    }

    private async Task SendEmailAsync(EmailRequests emailRequest)
    {
        try
        {
            var email = new MimeMessage();
            email.Sender = new MailboxAddress("BASMS Shift System", _emailSettings.Sender);
            email.To.Add(MailboxAddress.Parse(emailRequest.Email));
            email.Subject = emailRequest.Subject;
            
            var builder = new BodyBuilder { HtmlBody = emailRequest.EmailBody };
            email.Body = builder.ToMessageBody();

            using var smtp = new SmtpClient();
            await smtp.ConnectAsync(_emailSettings.SmtpHost, _emailSettings.SmtpPort, SecureSocketOptions.StartTls);
            await smtp.AuthenticateAsync(_emailSettings.Sender, _emailSettings.Password);
            await smtp.SendAsync(email);
            await smtp.DisconnectAsync(true);

            _logger.LogInformation("Email sent successfully to {Email}", emailRequest.Email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email}", emailRequest.Email);
        }
    }

    public async Task SendShiftCancellationEmailAsync(string guardName, string guardEmail, DateTime shiftDate, TimeSpan startTime, TimeSpan endTime, string location, string cancellationReason)
    {
        try
        {
            var subject = $"[HỦY CA] Ca trực ngày {shiftDate:dd/MM/yyyy} đã bị hủy";
            var emailBody = GenerateShiftCancellationTemplate(guardName, shiftDate, startTime, endTime, location, cancellationReason);
            await SendEmailAsync(new EmailRequests { Email = guardEmail, Subject = subject, EmailBody = emailBody });
            _logger.LogInformation("Shift cancellation email sent to {Email}", guardEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send cancellation email to {Email}", guardEmail);
        }
    }

    public async Task SendShiftCreatedEmailAsync(string guardName, string guardEmail, DateTime shiftDate, TimeSpan startTime, TimeSpan endTime, string location, string shiftType)
    {
        try
        {
            var subject = $"[CA MỚI] Bạn được phân công ca trực ngày {shiftDate:dd/MM/yyyy}";
            var emailBody = GenerateShiftCreatedTemplate(guardName, shiftDate, startTime, endTime, location, shiftType);
            await SendEmailAsync(new EmailRequests { Email = guardEmail, Subject = subject, EmailBody = emailBody });
            _logger.LogInformation("Shift created email sent to {Email}", guardEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send shift created email to {Email}", guardEmail);
        }
    }

    public async Task SendShiftUpdatedEmailAsync(string guardName, string guardEmail, DateTime shiftDate, TimeSpan startTime, TimeSpan endTime, string location, string changes)
    {
        try
        {
            var subject = $"[CẬP NHẬT] Ca trực ngày {shiftDate:dd/MM/yyyy} đã được cập nhật";
            var emailBody = GenerateShiftUpdatedTemplate(guardName, shiftDate, startTime, endTime, location, changes);
            await SendEmailAsync(new EmailRequests { Email = guardEmail, Subject = subject, EmailBody = emailBody });
            _logger.LogInformation("Shift updated email sent to {Email}", guardEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send shift updated email to {Email}", guardEmail);
        }
    }

    // New: Generate HTML for shift cancellation
    private string GenerateShiftCancellationTemplate(string guardName, DateTime shiftDate, TimeSpan startTime, TimeSpan endTime, string location, string cancellationReason)
    {
        var supportEmail = "vietanhcodega123@gmail.com";
        var body = @"
<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
</head>
<body style='margin:0;padding:0;background:linear-gradient(135deg,#ff9a56 0%,#ff6a88 100%);font-family:Inter,Arial,sans-serif;'>
    <table width='100%' cellpadding='0' cellspacing='0' style='padding:40px 20px;'>
        <tr><td align='center'>
            <table width='600' cellpadding='0' cellspacing='0' style='background:#ffffff;border-radius:12px;overflow:hidden;'>
                <tr>
                    <td style='background:linear-gradient(135deg,#ff9a56 0%,#ff6a88 100%);padding:30px;text-align:center;color:#fff;'>
                        <h1 style='margin:0;font-size:28px;'>🛑 Thông báo hủy ca</h1>
                        <p style='margin:6px 0 0 0;opacity:0.95;'>BASMS Shift System</p>
                    </td>
                </tr>
                <tr>
                    <td style='padding:30px;color:#2d3748;'>
                        <p>Xin chào <strong>" + guardName + @"</strong>,</p>
                        <p>Ca trực của bạn đã bị hủy. Chi tiết ca như sau:</p>

                        <div style='background:#fff5f5;border-left:4px solid #fc8181;padding:16px;border-radius:8px;margin:16px 0;'>
                            <table cellpadding='6' cellspacing='0' width='100%'>
                                <tr><td style='font-weight:600;width:140px;'>📅 Ngày:</td><td>" + shiftDate.ToString("dd/MM/yyyy") + @"</td></tr>
                                <tr><td style='font-weight:600;padding-top:8px;'>🕒 Giờ:</td><td>" + startTime.ToString(@"hh\:mm") + " - " + endTime.ToString(@"hh\:mm") + @"</td></tr>
                                <tr><td style='font-weight:600;padding-top:8px;'>📍 Vị trí:</td><td>" + location + @"</td></tr>
                                <tr><td style='font-weight:600;padding-top:8px;'>❗ Lý do:</td><td>" + cancellationReason + @"</td></tr>
                            </table>
                        </div>

                        <p style='color:#744210;'>Nếu bạn không rõ lý do hoặc cần hỗ trợ, vui lòng liên hệ:</p>
                        <p style='color:#ff6a88;font-weight:600;'>📧 " + supportEmail + @"</p>
                    </td>
                </tr>
                <tr>
                    <td style='background:#2d3748;padding:20px;text-align:center;color:#a0aec0;'>
                        © 2025 BASMS - Shift Management
                    </td>
                </tr>
            </table>
        </td></tr>
    </table>
</body>
</html>";
        return body;
    }

    // New: Generate HTML for new shift assignment
    private string GenerateShiftCreatedTemplate(string guardName, DateTime shiftDate, TimeSpan startTime, TimeSpan endTime, string location, string shiftType)
    {
        var supportEmail = "vietanhcodega123@gmail.com";
        var body = @"
<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
</head>
<body style='margin:0;padding:0;background:linear-gradient(135deg,#667eea 0%,#764ba2 100%);font-family:Inter,Arial,sans-serif;'>
    <table width='100%' cellpadding='0' cellspacing='0' style='padding:40px 20px;'>
        <tr><td align='center'>
            <table width='600' cellpadding='0' cellspacing='0' style='background:#ffffff;border-radius:12px;overflow:hidden;'>
                <tr>
                    <td style='background:linear-gradient(135deg,#667eea 0%,#764ba2 100%);padding:30px;text-align:center;color:#fff;'>
                        <h1 style='margin:0;font-size:28px;'>✅ Bạn được phân ca mới</h1>
                        <p style='margin:6px 0 0 0;opacity:0.95;'>BASMS Shift System</p>
                    </td>
                </tr>
                <tr>
                    <td style='padding:30px;color:#2d3748;'>
                        <p>Xin chào <strong>" + guardName + @"</strong>,</p>
                        <p>Bạn đã được phân công ca trực:</p>

                        <div style='background:linear-gradient(135deg,#f7fafc 0%,#edf2f7 100%);border-left:4px solid #667eea;padding:16px;border-radius:8px;margin:16px 0;'>
                            <table cellpadding='6' cellspacing='0' width='100%'>
                                <tr><td style='font-weight:600;width:140px;'>📅 Ngày:</td><td>" + shiftDate.ToString("dd/MM/yyyy") + @"</td></tr>
                                <tr><td style='font-weight:600;padding-top:8px;'>🕒 Giờ:</td><td>" + startTime.ToString(@"hh\:mm") + " - " + endTime.ToString(@"hh\:mm") + @"</td></tr>
                                <tr><td style='font-weight:600;padding-top:8px;'>📍 Vị trí:</td><td>" + location + @"</td></tr>
                                <tr><td style='font-weight:600;padding-top:8px;'>🏷️ Loại ca:</td><td>" + shiftType + @"</td></tr>
                            </table>
                        </div>

                        <p>Vui lòng đến đúng giờ và chuẩn bị đầy đủ thiết bị cần thiết.</p>
                        <p style='margin-top:12px;color:#667eea;font-weight:600;'>Hẹn gặp bạn trên ca!</p>

                        <div style='background:#f7fafc;padding:14px;border-radius:8px;margin-top:18px;text-align:center;'>
                            <p style='margin:0;color:#4a5568;'>Hỗ trợ: <span style='color:#667eea;font-weight:600;'>" + supportEmail + @"</span></p>
                        </div>
                    </td>
                </tr>
                <tr>
                    <td style='background:#2d3748;padding:20px;text-align:center;color:#a0aec0;'>
                        © 2025 BASMS - Shift Management
                    </td>
                </tr>
            </table>
        </td></tr>
    </table>
</body>
</html>";
        return body;
    }

    // New: Generate HTML for shift updates
    private string GenerateShiftUpdatedTemplate(string guardName, DateTime shiftDate, TimeSpan startTime, TimeSpan endTime, string location, string changes)
    {
        var supportEmail = "vietanhcodega123@gmail.com";
        var body = @"
<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
</head>
<body style='margin:0;padding:0;background:linear-gradient(135deg,#4facfe 0%,#00f2fe 100%);font-family:Inter,Arial,sans-serif;'>
    <table width='100%' cellpadding='0' cellspacing='0' style='padding:40px 20px;'>
        <tr><td align='center'>
            <table width='600' cellpadding='0' cellspacing='0' style='background:#ffffff;border-radius:12px;overflow:hidden;'>
                <tr>
                    <td style='background:linear-gradient(135deg,#4facfe 0%,#00f2fe 100%);padding:30px;text-align:center;color:#fff;'>
                        <h1 style='margin:0;font-size:28px;'>✏️ Ca trực đã được cập nhật</h1>
                        <p style='margin:6px 0 0 0;opacity:0.95;'>BASMS Shift System</p>
                    </td>
                </tr>
                <tr>
                    <td style='padding:30px;color:#2d3748;'>
                        <p>Xin chào <strong>" + guardName + @"</strong>,</p>
                        <p>Ca trực của bạn đã có thay đổi. Chi tiết hiện tại:</p>

                        <div style='background:#f7fafc;border-left:4px solid #4facfe;padding:16px;border-radius:8px;margin:16px 0;'>
                            <table cellpadding='6' cellspacing='0' width='100%'>
                                <tr><td style='font-weight:600;width:140px;'>📅 Ngày:</td><td>" + shiftDate.ToString("dd/MM/yyyy") + @"</td></tr>
                                <tr><td style='font-weight:600;padding-top:8px;'>🕒 Giờ:</td><td>" + startTime.ToString(@"hh\:mm") + " - " + endTime.ToString(@"hh\:mm") + @"</td></tr>
                                <tr><td style='font-weight:600;padding-top:8px;'>📍 Vị trí:</td><td>" + location + @"</td></tr>
                            </table>
                        </div>

                        <div style='background:#fffef6;border-left:4px solid #f6ad55;padding:14px;border-radius:8px;margin-bottom:12px;'>
                            <p style='margin:0;font-weight:600;color:#744210;'>Những thay đổi:</p>
                            <p style='margin:6px 0 0 0;color:#4a5568;'>" + changes + @"</p>
                        </div>

                        <p>Nếu bạn cần trợ giúp hoặc muốn phản hồi về thay đổi, vui lòng liên hệ: <strong>" + supportEmail + @"</strong></p>
                    </td>
                </tr>
                <tr>
                    <td style='background:#2d3748;padding:20px;text-align:center;color:#a0aec0;'>
                        © 2025 BASMS - Shift Management
                    </td>
                </tr>
            </table>
        </td></tr>
    </table>
</body>
</html>";
        return body;
    }
}
