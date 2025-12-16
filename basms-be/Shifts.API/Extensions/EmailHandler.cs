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

    public async Task SendCustomerShiftCancellationEmailAsync(string customerName, string customerEmail, DateTime shiftDate, TimeSpan startTime, TimeSpan endTime, string location, string cancellationReason, string contractId, string managerName)
    {
        try
        {
            var subject = $"[THÔNG BÁO HỦY CA] Ca trực ngày {shiftDate:dd/MM/yyyy} - Contract {contractId}";
            var emailBody = GenerateCustomerShiftCancellationTemplate(customerName, shiftDate, startTime, endTime, location, cancellationReason, contractId, managerName);
            await SendEmailAsync(new EmailRequests { Email = customerEmail, Subject = subject, EmailBody = emailBody });
            _logger.LogInformation("Customer shift cancellation email sent to {Email}", customerEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send customer cancellation email to {Email}", customerEmail);
        }
    }

    public async Task SendDirectorShiftCancellationEmailAsync(DateTime shiftDate, TimeSpan startTime, TimeSpan endTime, string location, string locationAddress, string cancellationReason, string contractId, string managerName, string managerEmail, int affectedGuardsCount, string guardsList)
    {
        try
        {
            var subject = $"[BÁO CÁO HỦY CA] Contract {contractId} - {shiftDate:dd/MM/yyyy}";
            var emailBody = GenerateDirectorShiftCancellationTemplate(shiftDate, startTime, endTime, location, locationAddress, cancellationReason, contractId, managerName, managerEmail, affectedGuardsCount, guardsList);
            await SendEmailAsync(new EmailRequests { Email = "director@basms.com", Subject = subject, EmailBody = emailBody });
            _logger.LogInformation("Director shift cancellation email sent to director@basms.com");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send director cancellation email");
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

    // New: Generate HTML template for customer shift cancellation notification
    private string GenerateCustomerShiftCancellationTemplate(string customerName, DateTime shiftDate, TimeSpan startTime, TimeSpan endTime, string location, string cancellationReason, string contractId, string managerName)
    {
        var supportEmail = "vietanhcodega123@gmail.com";
        var body = @"
<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
</head>
<body style='margin:0;padding:0;background:linear-gradient(135deg,#f093fb 0%,#f5576c 100%);font-family:Inter,Arial,sans-serif;'>
    <table width='100%' cellpadding='0' cellspacing='0' style='padding:40px 20px;'>
        <tr><td align='center'>
            <table width='600' cellpadding='0' cellspacing='0' style='background:#ffffff;border-radius:12px;overflow:hidden;box-shadow:0 10px 40px rgba(0,0,0,0.1);'>
                <tr>
                    <td style='background:linear-gradient(135deg,#f093fb 0%,#f5576c 100%);padding:35px;text-align:center;color:#fff;'>
                        <h1 style='margin:0;font-size:32px;font-weight:700;'>⚠️ Thông báo hủy ca trực</h1>
                        <p style='margin:8px 0 0 0;opacity:0.95;font-size:15px;'>BASMS - Hệ thống quản lý ca trực bảo vệ</p>
                    </td>
                </tr>
                <tr>
                    <td style='padding:35px;color:#2d3748;'>
                        <p style='font-size:16px;margin-bottom:8px;'>Kính gửi <strong style='color:#f5576c;'>" + customerName + @"</strong>,</p>
                        <p style='font-size:15px;line-height:1.6;color:#4a5568;'>Chúng tôi xin thông báo ca trực thuộc hợp đồng của quý khách đã bị hủy. Chi tiết như sau:</p>

                        <div style='background:#fff5f5;border-left:5px solid #f5576c;padding:20px;border-radius:10px;margin:20px 0;box-shadow:0 2px 8px rgba(245,87,108,0.1);'>
                            <h3 style='margin:0 0 16px 0;color:#f5576c;font-size:18px;'>📋 Thông tin ca trực</h3>
                            <table cellpadding='8' cellspacing='0' width='100%' style='font-size:14px;'>
                                <tr><td style='font-weight:600;width:150px;color:#4a5568;'>📅 Ngày:</td><td style='color:#2d3748;'>" + shiftDate.ToString("dd/MM/yyyy") + @"</td></tr>
                                <tr><td style='font-weight:600;color:#4a5568;'>🕒 Thời gian:</td><td style='color:#2d3748;'>" + startTime.ToString(@"hh\:mm") + " - " + endTime.ToString(@"hh\:mm") + @"</td></tr>
                                <tr><td style='font-weight:600;color:#4a5568;'>📍 Địa điểm:</td><td style='color:#2d3748;'>" + location + @"</td></tr>
                                <tr><td style='font-weight:600;color:#4a5568;'>📄 Hợp đồng:</td><td style='color:#2d3748;'>" + contractId + @"</td></tr>
                                <tr><td style='font-weight:600;color:#4a5568;'>👤 Quản lý:</td><td style='color:#2d3748;'>" + managerName + @"</td></tr>
                            </table>
                        </div>

                        <div style='background:#fffbeb;border-left:5px solid #fbbf24;padding:18px;border-radius:10px;margin:20px 0;'>
                            <p style='margin:0;font-weight:600;color:#92400e;font-size:15px;'>💡 Lý do hủy ca:</p>
                            <p style='margin:8px 0 0 0;color:#78350f;line-height:1.6;'>" + cancellationReason + @"</p>
                        </div>

                        <div style='background:#f0fdf4;border-left:5px solid #10b981;padding:18px;border-radius:10px;margin:20px 0;'>
                            <p style='margin:0;color:#065f46;line-height:1.6;'>
                                <strong>📞 Liên hệ hỗ trợ:</strong><br>
                                Nếu quý khách cần hỗ trợ hoặc có thắc mắc về việc hủy ca, vui lòng liên hệ:<br>
                                <span style='color:#10b981;font-weight:600;'>Email: " + supportEmail + @"</span>
                            </p>
                        </div>

                        <p style='font-size:14px;color:#4a5568;margin-top:24px;line-height:1.6;'>Chúng tôi xin lỗi vì sự bất tiện này và cam kết cung cấp dịch vụ tốt nhất cho quý khách.</p>
                        <p style='font-size:14px;color:#4a5568;font-weight:600;margin-top:12px;'>Trân trọng,<br><span style='color:#f5576c;'>Đội ngũ BASMS</span></p>
                    </td>
                </tr>
                <tr>
                    <td style='background:#2d3748;padding:24px;text-align:center;color:#a0aec0;font-size:13px;'>
                        <p style='margin:0;'>© 2025 BASMS - Hệ thống quản lý ca trực bảo vệ</p>
                        <p style='margin:6px 0 0 0;opacity:0.8;'>Công ty TNHH BASMS Việt Nam</p>
                    </td>
                </tr>
            </table>
        </td></tr>
    </table>
</body>
</html>";
        return body;
    }

    // New: Generate HTML template for director shift cancellation report
    private string GenerateDirectorShiftCancellationTemplate(DateTime shiftDate, TimeSpan startTime, TimeSpan endTime, string location, string locationAddress, string cancellationReason, string contractId, string managerName, string managerEmail, int affectedGuardsCount, string guardsList)
    {
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
            <table width='700' cellpadding='0' cellspacing='0' style='background:#ffffff;border-radius:12px;overflow:hidden;box-shadow:0 10px 50px rgba(0,0,0,0.15);'>
                <tr>
                    <td style='background:linear-gradient(135deg,#667eea 0%,#764ba2 100%);padding:40px;text-align:center;color:#fff;'>
                        <h1 style='margin:0;font-size:34px;font-weight:700;'>🚨 BÁO CÁO HỦY CA TRỰC</h1>
                        <p style='margin:10px 0 0 0;opacity:0.95;font-size:16px;'>Dành cho Ban Giám Đốc</p>
                    </td>
                </tr>
                <tr>
                    <td style='padding:40px;color:#2d3748;'>
                        <p style='font-size:16px;margin-bottom:12px;'>Kính gửi <strong style='color:#667eea;'>Ban Giám Đốc</strong>,</p>
                        <p style='font-size:15px;line-height:1.6;color:#4a5568;'>Có một ca trực đã bị hủy. Dưới đây là báo cáo chi tiết:</p>

                        <div style='background:#eef2ff;border-left:6px solid #667eea;padding:24px;border-radius:12px;margin:24px 0;box-shadow:0 2px 10px rgba(102,126,234,0.1);'>
                            <h3 style='margin:0 0 18px 0;color:#667eea;font-size:20px;border-bottom:2px solid #c7d2fe;padding-bottom:10px;'>📊 THÔNG TIN CA TRỰC</h3>
                            <table cellpadding='10' cellspacing='0' width='100%' style='font-size:15px;'>
                                <tr style='background:#f5f7ff;'>
                                    <td style='font-weight:600;width:180px;color:#4a5568;border-radius:6px;'>📅 Ngày:</td>
                                    <td style='color:#2d3748;font-weight:500;'>" + shiftDate.ToString("dd/MM/yyyy (dddd)", new System.Globalization.CultureInfo("vi-VN")) + @"</td>
                                </tr>
                                <tr><td style='font-weight:600;color:#4a5568;padding-top:4px;'>🕒 Thời gian:</td><td style='color:#2d3748;font-weight:500;'>" + startTime.ToString(@"hh\:mm") + " - " + endTime.ToString(@"hh\:mm") + @"</td></tr>
                                <tr style='background:#f5f7ff;'>
                                    <td style='font-weight:600;color:#4a5568;border-radius:6px;'>📍 Địa điểm:</td>
                                    <td style='color:#2d3748;font-weight:500;'>" + location + @"</td>
                                </tr>
                                <tr><td style='font-weight:600;color:#4a5568;'>📮 Địa chỉ:</td><td style='color:#2d3748;'>" + locationAddress + @"</td></tr>
                                <tr style='background:#f5f7ff;'>
                                    <td style='font-weight:600;color:#4a5568;border-radius:6px;'>📄 Mã hợp đồng:</td>
                                    <td style='color:#667eea;font-weight:600;'>" + contractId + @"</td>
                                </tr>
                            </table>
                        </div>

                        <div style='background:#fef3c7;border-left:6px solid #f59e0b;padding:24px;border-radius:12px;margin:24px 0;box-shadow:0 2px 10px rgba(245,158,11,0.1);'>
                            <h3 style='margin:0 0 14px 0;color:#b45309;font-size:18px;'>⚠️ LÝ DO HỦY CA</h3>
                            <p style='margin:0;color:#78350f;font-size:15px;line-height:1.7;font-weight:500;'>" + cancellationReason + @"</p>
                        </div>

                        <div style='background:#f0fdf4;border-left:6px solid #10b981;padding:24px;border-radius:12px;margin:24px 0;'>
                            <h3 style='margin:0 0 14px 0;color:#065f46;font-size:18px;'>👤 QUẢN LÝ PHỤ TRÁCH</h3>
                            <table cellpadding='8' cellspacing='0' width='100%' style='font-size:15px;'>
                                <tr><td style='font-weight:600;width:140px;color:#047857;'>Họ tên:</td><td style='color:#065f46;font-weight:500;'>" + managerName + @"</td></tr>
                                <tr><td style='font-weight:600;color:#047857;'>Email:</td><td style='color:#065f46;'><a href='mailto:" + managerEmail + @"' style='color:#10b981;text-decoration:none;'>" + managerEmail + @"</a></td></tr>
                            </table>
                        </div>

                        <div style='background:#fee2e2;border-left:6px solid #ef4444;padding:24px;border-radius:12px;margin:24px 0;'>
                            <h3 style='margin:0 0 14px 0;color:#991b1b;font-size:18px;'>👥 NHÂN SỰ BỊ ẢNH HƯỞNG</h3>
                            <p style='margin:0 0 12px 0;font-size:18px;font-weight:700;color:#dc2626;'>Tổng số: " + affectedGuardsCount + @" bảo vệ</p>
                            <div style='background:#ffffff;padding:16px;border-radius:8px;'>
                                <p style='margin:0;color:#4a5568;font-size:14px;line-height:1.8;'>" + guardsList + @"</p>
                            </div>
                        </div>

                        <div style='background:#eff6ff;border:2px dashed #3b82f6;padding:20px;border-radius:10px;margin-top:24px;'>
                            <p style='margin:0;color:#1e40af;font-size:14px;line-height:1.7;'>
                                <strong>📝 Ghi chú:</strong> Vui lòng kiểm tra và xác nhận với khách hàng về việc hủy ca này.
                                Đảm bảo các bảo vệ đã được thông báo kịp thời và có phương án backup nếu cần thiết.
                            </p>
                        </div>

                        <p style='font-size:13px;color:#9ca3af;margin-top:28px;text-align:center;'>
                            Thời gian gửi báo cáo: " + DateTime.UtcNow.AddHours(7).ToString("dd/MM/yyyy HH:mm:ss") + @" (UTC+7)
                        </p>
                    </td>
                </tr>
                <tr>
                    <td style='background:#2d3748;padding:28px;text-align:center;color:#a0aec0;font-size:14px;'>
                        <p style='margin:0;font-weight:600;'>© 2025 BASMS - Hệ thống quản lý ca trực bảo vệ</p>
                        <p style='margin:8px 0 0 0;opacity:0.8;'>Công ty TNHH BASMS Việt Nam | Hotline: 1900-xxxx</p>
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
