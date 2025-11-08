using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Contracts.API.Extensions;

public class EmailHandler
{
    private readonly EmailSettings _emailSettings;
    private readonly ILogger<EmailHandler> _logger;

    public EmailHandler(IOptions<EmailSettings> emailSettings, ILogger<EmailHandler> logger)
    {
        _emailSettings = emailSettings.Value;
        _logger = logger;
    }

    public async Task SendEmailAsync(EmailRequests emailRequest)
    {
        try
        {
            var email = new MimeMessage();
            email.Sender = new MailboxAddress("BASMS System", _emailSettings.Sender);
            email.To.Add(MailboxAddress.Parse(emailRequest.Email));
            email.Subject = emailRequest.Subject;

            var builder = new BodyBuilder
            {
                HtmlBody = emailRequest.EmailBody
            };
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
            throw;
        }
    }

    /// <summary>
    /// Gửi email thông tin đăng nhập cho customer mới
    /// </summary>
    public async Task SendCustomerLoginInfoEmailAsync(
        string customerName,
        string email,
        string password,
        string contractNumber)
    {
        var emailBody = GenerateCustomerLoginEmailBody(customerName, email, password, contractNumber);
        var emailRequest = new EmailRequests
        {
            Email = email,
            Subject = "Thông tin đăng nhập hệ thống BASMS 🔐",
            EmailBody = emailBody
        };

        await SendEmailAsync(emailRequest);
    }

    private string GenerateCustomerLoginEmailBody(
        string customerName,
        string email,
        string password,
        string contractNumber)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #4CAF50; color: white; padding: 20px; text-align: center; border-radius: 5px 5px 0 0; }}
        .content {{ background-color: #f9f9f9; padding: 30px; border: 1px solid #ddd; }}
        .credentials {{ background-color: #fff; padding: 20px; border-left: 4px solid #4CAF50; margin: 20px 0; }}
        .credentials-label {{ font-weight: bold; color: #666; }}
        .credentials-value {{ font-size: 18px; color: #333; margin: 5px 0; padding: 10px; background-color: #f0f0f0; border-radius: 3px; }}
        .warning {{ background-color: #fff3cd; border-left: 4px solid #ffc107; padding: 15px; margin: 20px 0; }}
        .footer {{ background-color: #333; color: white; padding: 15px; text-align: center; font-size: 12px; border-radius: 0 0 5px 5px; }}
        .button {{ display: inline-block; padding: 12px 30px; background-color: #4CAF50; color: white; text-decoration: none; border-radius: 5px; margin: 20px 0; }}
        .info-box {{ background-color: #e3f2fd; border-left: 4px solid #2196F3; padding: 15px; margin: 20px 0; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>🎉 Chào mừng đến với BASMS</h1>
            <p>Building & Assets Security Management System</p>
        </div>

        <div class='content'>
            <p>Kính gửi <strong>{customerName}</strong>,</p>

            <p>Cảm ơn Quý khách đã tin tưởng và ký hợp đồng bảo vệ với chúng tôi!</p>

            <div class='info-box'>
                <strong>📋 Hợp đồng của bạn:</strong> {contractNumber}<br>
                Hợp đồng đã được nhập hệ thống thành công.
            </div>

            <p>Chúng tôi đã tạo tài khoản truy cập hệ thống BASMS cho Quý khách. Vui lòng sử dụng thông tin đăng nhập sau:</p>

            <div class='credentials'>
                <div class='credentials-label'>📧 Email đăng nhập:</div>
                <div class='credentials-value'>{email}</div>

                <div class='credentials-label' style='margin-top: 15px;'>🔑 Mật khẩu:</div>
                <div class='credentials-value'>{password}</div>
            </div>

            <div class='warning'>
                <strong>⚠️ Lưu ý quan trọng:</strong><br>
                • Vui lòng đổi mật khẩu ngay sau lần đăng nhập đầu tiên<br>
                • Không chia sẻ thông tin đăng nhập cho người khác<br>
                • Liên hệ ngay với chúng tôi nếu phát hiện truy cập bất thường
            </div>

            <p><strong>Quyền lợi của tài khoản Customer:</strong></p>
            <ul>
                <li>✅ Xem thông tin hợp đồng và địa điểm</li>
                <li>✅ Theo dõi lịch ca trực bảo vệ</li>
                <li>✅ Xem báo cáo và thống kê dịch vụ</li>
                <li>✅ Quản lý thông tin liên hệ</li>
                <li>✅ Gửi yêu cầu hỗ trợ trực tuyến</li>
            </ul>

            <center>
                <a href='http://localhost:3000/login' class='button'>Đăng nhập ngay</a>
            </center>

            <p style='margin-top: 30px;'>Nếu có bất kỳ thắc mắc nào, vui lòng liên hệ:</p>
            <p>
                📞 Hotline: 1900-xxxx<br>
                📧 Email: support@basms.com<br>
                🌐 Website: www.basms.com
            </p>

            <p>Trân trọng,<br><strong>Đội ngũ BASMS</strong></p>
        </div>

        <div class='footer'>
            <p>© 2025 BASMS - Building & Assets Security Management System</p>
            <p>Email này được gửi tự động, vui lòng không trả lời trực tiếp.</p>
        </div>
    </div>
</body>
</html>
";
    }
}
