using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Contracts.API.Extensions;

public class EmailHandler
{
    private readonly EmailSettings _emailSettings;
    private readonly ILogger<EmailHandler> _logger;
    private readonly IS3Service _s3Service;

    public EmailHandler(IOptions<EmailSettings> emailSettings, ILogger<EmailHandler> logger, IS3Service s3Service)
    {
        _emailSettings = emailSettings.Value;
        _logger = logger;
        _s3Service = s3Service;
    }

    public async Task SendEmailAsync(EmailRequests emailRequest)
    {
        try
        {
            // Validate email settings
            if (string.IsNullOrEmpty(_emailSettings.Sender))
            {
                throw new InvalidOperationException("EMAIL_SENDER environment variable is not set");
            }

            if (string.IsNullOrEmpty(_emailSettings.Password))
            {
                throw new InvalidOperationException("EMAIL_PASSWORD environment variable is not set");
            }

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
        }
        catch
        {
            // Suppress email errors silently
            throw;
        }
    }
    
    public async Task SendGuardLoginInfoEmailAsync(
        string guardName,
        string guardEmail,
        string password,
        string contractNumber)
    {
        var emailBody = GenerateGuardLoginEmailBody(guardName, guardEmail, password, contractNumber);
        var emailRequest = new EmailRequests
        {
            Email = guardEmail,
            Subject = "Thông tin đăng nhập ứng dụng BASMS",
            EmailBody = emailBody
        };

        await SendEmailAsync(emailRequest);
    }
    
    private string GenerateGuardLoginEmailBody(
    string guardName,
    string email,
    string password,
    string contractNumber)
{
    return $@"
<!DOCTYPE html>
<html lang=""vi"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Thông tin đăng nhập Hệ thống quản lý bảo vệ</title>
</head>
<body style=""margin: 0; padding: 0; font-family: Arial, sans-serif; background-color: #f4f4f4;"">
    <table width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background-color: #f4f4f4; padding: 20px;"">
        <tr>
            <td align=""center"">
                <table width=""600"" cellpadding=""0"" cellspacing=""0"" style=""background-color: #ffffff; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.1);"">
                    <!-- Header -->
                    <tr>
                        <td style=""background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); padding: 30px; text-align: center; border-radius: 8px 8px 0 0;"">
                            <h1 style=""color: #ffffff; margin: 0; font-size: 28px; font-weight: bold;"">
                                Chào mừng đến với Hệ thống quản lý bảo vệ
                            </h1>
                        </td>
                    </tr>

                    <!-- Content -->
                    <tr>
                        <td style=""padding: 40px 30px;"">
                            <p style=""color: #333333; font-size: 16px; line-height: 1.6; margin: 0 0 20px 0;"">
                                Xin chào <strong>{guardName}</strong>,
                            </p>
                            
                            <p style=""color: #333333; font-size: 16px; line-height: 1.6; margin: 0 0 20px 0;"">
                                Tài khoản <strong>Nhân viên bảo vệ</strong> của bạn đã được tạo thành công trong hệ thống BASMS 
                                cho <strong>Hợp đồng {contractNumber}</strong>.
                            </p>

                            <div style=""background-color: #f8f9fa; border-left: 4px solid #667eea; padding: 20px; margin: 20px 0; border-radius: 4px;"">
                                <h2 style=""color: #667eea; margin: 0 0 15px 0; font-size: 18px;"">
                                    📋 Thông tin đăng nhập
                                </h2>
                                
                                <table style=""width: 100%; border-collapse: collapse;"">
                                    <tr>
                                        <td style=""padding: 8px 0; color: #666666; font-size: 14px; width: 30%;"">
                                            <strong>Email:</strong>
                                        </td>
                                        <td style=""padding: 8px 0; color: #333333; font-size: 14px;"">
                                            <code style=""background-color: #e9ecef; padding: 4px 8px; border-radius: 4px; font-family: 'Courier New', monospace;"">{email}</code>
                                        </td>
                                    </tr>
                                    <tr>
                                        <td style=""padding: 8px 0; color: #666666; font-size: 14px;"">
                                            <strong>Mật khẩu:</strong>
                                        </td>
                                        <td style=""padding: 8px 0; color: #333333; font-size: 14px;"">
                                            <code style=""background-color: #fff3cd; padding: 4px 8px; border-radius: 4px; font-family: 'Courier New', monospace; color: #856404;"">{password}</code>
                                        </td>
                                    </tr>
                                    <tr>
                                        <td style=""padding: 8px 0; color: #666666; font-size: 14px;"">
                                            <strong>Vai trò:</strong>
                                        </td>
                                        <td style=""padding: 8px 0; color: #333333; font-size: 14px;"">
                                            <span style=""background-color: #cfe2ff; color: #084298; padding: 4px 12px; border-radius: 12px; font-size: 13px; font-weight: 600;"">
                                                Guard (Nhân viên bảo vệ)
                                            </span>
                                        </td>
                                    </tr>
                                </table>
                            </div>

                            <div style=""background-color: #fff3cd; border-left: 4px solid #ffc107; padding: 15px; margin: 20px 0; border-radius: 4px;"">
                                <p style=""color: #856404; margin: 0; font-size: 14px; line-height: 1.6;"">
                                    <strong>Quan trọng:</strong> Đây là mật khẩu tạm thời. 
                                    Vui lòng đổi mật khẩu ngay sau khi đăng nhập lần đầu để đảm bảo bảo mật tài khoản.
                                </p>
                            </div>

                            <div style=""background-color: #e7f3ff; border-left: 4px solid #2196F3; padding: 20px; margin: 20px 0; border-radius: 4px; text-align: center;"">
                                <h3 style=""color: #2196F3; margin: 0 0 15px 0; font-size: 18px;"">
                                  Đăng nhập qua ứng dụng di động
                                </h3>
                                <p style=""color: #333333; font-size: 14px; line-height: 1.6; margin: 0 0 15px 0;"">
                                    Vui lòng tải và cài đặt ứng dụng BASMS trên điện thoại của bạn:
                                </p>
                                <div style=""margin: 20px 0;"">
                                    <p style=""margin: 10px 0;"">
                                        <strong>📲 Android:</strong> Tìm kiếm ""BASMS"" trên Google Play Store
                                    </p>
                                    <p style=""margin: 10px 0;"">
                                        <strong>📲 iOS:</strong> Tìm kiếm ""BASMS"" trên App Store
                                    </p>
                                </div>
                                <p style=""color: #666666; font-size: 13px; margin: 0; font-style: italic;"">
                                    Sau khi cài đặt, sử dụng email và mật khẩu ở trên để đăng nhập
                                </p>
                            </div>

                            <div style=""background-color: #e8f5e9; border-left: 4px solid #4CAF50; padding: 15px; margin: 20px 0; border-radius: 4px;"">
                                <h3 style=""color: #4CAF50; margin: 0 0 10px 0; font-size: 16px;"">
                                    🎯 Chức năng của ứng dụng
                                </h3>
                                <ul style=""color: #333333; font-size: 14px; line-height: 1.8; margin: 0; padding-left: 20px;"">
                                    <li>Xem lịch trực và ca làm việc của bạn</li>
                                    <li>Check-in/Check-out khi bắt đầu và kết thúc ca trực</li>
                                    <li>Báo cáo sự cố và tình huống bất thường</li>
                                    <li>Nhận thông báo về lịch trực và thay đổi ca</li>
                                    <li>Gửi yêu cầu nghỉ phép hoặc đổi ca</li>
                                </ul>
                            </div>

                            <p style=""color: #666666; font-size: 14px; line-height: 1.6; margin: 20px 0 0 0;"">
                                Nếu bạn có bất kỳ câu hỏi nào hoặc cần hỗ trợ, vui lòng liên hệ với quản lý của bạn.
                            </p>
                        </td>
                    </tr>

                    <!-- Footer -->
                    <tr>
                        <td style=""background-color: #f8f9fa; padding: 20px 30px; border-radius: 0 0 8px 8px;"">
                            <p style=""color: #666666; font-size: 12px; line-height: 1.6; margin: 0 0 10px 0; text-align: center;"">
                                Email này được gửi tự động từ hệ thống BASMS<br>
                                Vui lòng không trả lời email này
                            </p>
                            <p style=""color: #999999; font-size: 11px; margin: 0; text-align: center;"">
                                © 2025 BASMS - Building & Apartment Security Management System
                            </p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";
}

    public async Task SendManagerLoginInfoEmailAsync(
        string managerName,
        string managerEmail,
        string password,
        string contractNumber)
    {
        var emailBody = GenerateManagerLoginEmailBody(managerName, managerEmail, password, contractNumber);
        var emailRequest = new EmailRequests
        {
            Email = managerEmail,
            Subject = "Thông tin đăng nhập hệ thống BASMS",
            EmailBody = emailBody
        };

        await SendEmailAsync(emailRequest);
    }
    
    private string GenerateManagerLoginEmailBody(
    string managerName,
    string email,
    string password,
    string contractNumber)
    
{
    var loginUrl = "https://anninhsinhtrac.com/login";
    
    return $@"
<!DOCTYPE html>
<html lang=""vi"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Thông tin đăng nhập BASMS</title>
</head>
<body style=""margin: 0; padding: 0; font-family: Arial, sans-serif; background-color: #f4f4f4;"">
    <table width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background-color: #f4f4f4; padding: 20px;"">
        <tr>
            <td align=""center"">
                <table width=""600"" cellpadding=""0"" cellspacing=""0"" style=""background-color: #ffffff; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.1);"">
                    <!-- Header -->
                    <tr>
                        <td style=""background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); padding: 30px; text-align: center; border-radius: 8px 8px 0 0;"">
                            <h1 style=""color: #ffffff; margin: 0; font-size: 28px; font-weight: bold;"">
                                🎉 Chào mừng đến với BASMS
                            </h1>
                            <p style=""color: #ffffff; margin: 10px 0 0 0; font-size: 16px;"">
                                Hệ thống quản lý bảo vệ thông minh
                            </p>
                        </td>
                    </tr>

                    <!-- Content -->
                    <tr>
                        <td style=""padding: 40px 30px;"">
                            <p style=""color: #333333; font-size: 16px; line-height: 1.6; margin: 0 0 20px 0;"">
                                Xin chào <strong>{managerName}</strong>,
                            </p>
                            
                            <p style=""color: #333333; font-size: 16px; line-height: 1.6; margin: 0 0 20px 0;"">
                                Tài khoản <strong>Quản lý</strong> của bạn đã được tạo thành công trong hệ thống BASMS 
                                cho <strong>Hợp đồng {contractNumber}</strong>.
                            </p>

                            <div style=""background-color: #f8f9fa; border-left: 4px solid #667eea; padding: 20px; margin: 20px 0; border-radius: 4px;"">
                                <h2 style=""color: #667eea; margin: 0 0 15px 0; font-size: 18px;"">
                                    📋 Thông tin đăng nhập
                                </h2>
                                
                                <table style=""width: 100%; border-collapse: collapse;"">
                                    <tr>
                                        <td style=""padding: 8px 0; color: #666666; font-size: 14px; width: 30%;"">
                                            <strong>Email:</strong>
                                        </td>
                                        <td style=""padding: 8px 0; color: #333333; font-size: 14px;"">
                                            <code style=""background-color: #e9ecef; padding: 4px 8px; border-radius: 4px; font-family: 'Courier New', monospace;"">{email}</code>
                                        </td>
                                    </tr>
                                    <tr>
                                        <td style=""padding: 8px 0; color: #666666; font-size: 14px;"">
                                            <strong>Mật khẩu:</strong>
                                        </td>
                                        <td style=""padding: 8px 0; color: #333333; font-size: 14px;"">
                                            <code style=""background-color: #fff3cd; padding: 4px 8px; border-radius: 4px; font-family: 'Courier New', monospace; color: #856404;"">{password}</code>
                                        </td>
                                    </tr>
                                    <tr>
                                        <td style=""padding: 8px 0; color: #666666; font-size: 14px;"">
                                            <strong>Vai trò:</strong>
                                        </td>
                                        <td style=""padding: 8px 0; color: #333333; font-size: 14px;"">
                                            <span style=""background-color: #d4edda; color: #155724; padding: 4px 12px; border-radius: 12px; font-size: 13px; font-weight: 600;"">
                                                Manager (Quản lý)
                                            </span>
                                        </td>
                                    </tr>
                                </table>
                            </div>

                            <div style=""background-color: #fff3cd; border-left: 4px solid #ffc107; padding: 15px; margin: 20px 0; border-radius: 4px;"">
                                <p style=""color: #856404; margin: 0; font-size: 14px; line-height: 1.6;"">
                                    <strong>⚠️ Quan trọng:</strong> Đây là mật khẩu tạm thời. 
                                    Vui lòng đổi mật khẩu ngay sau khi đăng nhập lần đầu để đảm bảo bảo mật tài khoản.
                                </p>
                            </div>

                            <div style=""text-align: center; margin: 30px 0;"">
                                <a href=""{loginUrl}"" 
                                   style=""background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); 
                                          color: #ffffff; 
                                          padding: 14px 40px; 
                                          text-decoration: none; 
                                          border-radius: 6px; 
                                          font-size: 16px; 
                                          font-weight: bold; 
                                          display: inline-block;
                                          box-shadow: 0 4px 6px rgba(102, 126, 234, 0.25);"">
                                    🔐 Đăng nhập ngay
                                </a>
                            </div>

                            <div style=""background-color: #e7f3ff; border-left: 4px solid #2196F3; padding: 15px; margin: 20px 0; border-radius: 4px;"">
                                <h3 style=""color: #2196F3; margin: 0 0 10px 0; font-size: 16px;"">
                                    🎯 Vai trò và quyền hạn của bạn
                                </h3>
                                <ul style=""color: #333333; font-size: 14px; line-height: 1.8; margin: 0; padding-left: 20px;"">
                                    <li>Quản lý lịch làm việc của đội ngũ bảo vệ</li>
                                    <li>Giám sát ca trực và phân công nhân viên</li>
                                    <li>Xem báo cáo và thống kê hoạt động</li>
                                    <li>Quản lý thông tin nhân viên bảo vệ</li>
                                    <li>Xử lý các yêu cầu nghỉ phép và thay đổi ca</li>
                                </ul>
                            </div>

                            <p style=""color: #666666; font-size: 14px; line-height: 1.6; margin: 20px 0 0 0;"">
                                Nếu bạn có bất kỳ câu hỏi nào hoặc cần hỗ trợ, vui lòng liên hệ với chúng tôi.
                            </p>
                        </td>
                    </tr>

                    <!-- Footer -->
                    <tr>
                        <td style=""background-color: #f8f9fa; padding: 20px 30px; border-radius: 0 0 8px 8px;"">
                            <p style=""color: #666666; font-size: 12px; line-height: 1.6; margin: 0 0 10px 0; text-align: center;"">
                                Email này được gửi tự động từ hệ thống BASMS<br>
                                Vui lòng không trả lời email này
                            </p>
                            <p style=""color: #999999; font-size: 11px; margin: 0; text-align: center;"">
                                © 2025 BASMS - Building & Apartment Security Management System
                            </p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";
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
            Subject = "Thông tin đăng nhập hệ thống BASMS",
            EmailBody = emailBody
        };

        await SendEmailAsync(emailRequest);
    }

    /// <summary>
    /// Gửi email ký hợp đồng điện tử với link và token bảo mật
    /// </summary>
    public async Task SendContractSigningEmailAsync(
        string customerName,
        string email,
        string contractNumber,
        Guid documentId,
        string securityToken,
        DateTime tokenExpiredDay)
    {
        var emailBody = GenerateContractSigningEmailBody(customerName, contractNumber, documentId, securityToken, tokenExpiredDay);
        var emailRequest = new EmailRequests
        {
            Email = email,
            Subject = "Yêu cầu ký hợp đồng điện tử",
            EmailBody = emailBody
        };

        await SendEmailAsync(emailRequest);
    }

    /// <summary>
    /// Gửi email xác nhận đã ký hợp đồng thành công
    /// </summary>
    public async Task SendContractSignedConfirmationEmailAsync(
        string customerName,
        string email,
        string contractNumber,
        DateTime signedDate,
        string s3FileKey)
    {
        var emailBody = GenerateContractSignedConfirmationEmailBody(customerName, contractNumber, signedDate, s3FileKey);
        var emailRequest = new EmailRequests
        {
            Email = email,
            Subject = "Xác nhận chữ ký hợp đồng thành công - BASMS",
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
        var template = @"
<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <style>
        body { font-family: Arial, sans-serif; line-height: 1.6; color: #333; }
        .container { max-width: 600px; margin: 0 auto; padding: 20px; }
        .header { background-color: #4CAF50; color: white; padding: 20px; text-align: center; border-radius: 5px 5px 0 0; }
        .content { background-color: #f9f9f9; padding: 30px; border: 1px solid #ddd; }
        .credentials { background-color: #fff; padding: 20px; border-left: 4px solid #4CAF50; margin: 20px 0; }
        .credentials-label { font-weight: bold; color: #666; }
        .credentials-value { font-size: 18px; color: #333; margin: 5px 0; padding: 10px; background-color: #f0f0f0; border-radius: 3px; }
        .warning { background-color: #fff3cd; border-left: 4px solid #ffc107; padding: 15px; margin: 20px 0; }
        .footer { background-color: #333; color: white; padding: 15px; text-align: center; font-size: 12px; border-radius: 0 0 5px 5px; }
        .button { display: inline-block; padding: 12px 30px; background-color: #4CAF50; color: white; text-decoration: none; border-radius: 5px; margin: 20px 0; }
        .info-box { background-color: #e3f2fd; border-left: 4px solid #2196F3; padding: 15px; margin: 20px 0; }
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
                <a href='https://anninhsinhtrac.com/login' class='button'>Đăng nhập ngay</a>
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

        return template
            .Replace("{customerName}", customerName)
            .Replace("{email}", email)
            .Replace("{password}", password)
            .Replace("{contractNumber}", contractNumber);
    }

    private string GenerateContractSigningEmailBody(
        string customerName,
        string contractNumber,
        Guid documentId,
        string securityToken,
        DateTime tokenExpiredDay)
    {
        var signingUrl = $"https://anninhsinhtrac.com/{documentId}/sign?token={securityToken}";
        var expiredDateStr = tokenExpiredDay.ToString("dd/MM/yyyy HH:mm");

        var template = @"
<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <style>
        body { font-family: Arial, sans-serif; line-height: 1.6; color: #333; }
        .container { max-width: 600px; margin: 0 auto; padding: 20px; }
        .header { background-color: #2196F3; color: white; padding: 20px; text-align: center; border-radius: 5px 5px 0 0; }
        .content { background-color: #f9f9f9; padding: 30px; border: 1px solid #ddd; }
        .info-box { background-color: #e3f2fd; border-left: 4px solid #2196F3; padding: 15px; margin: 20px 0; }
        .warning { background-color: #fff3cd; border-left: 4px solid #ffc107; padding: 15px; margin: 20px 0; }
        .footer { background-color: #333; color: white; padding: 15px; text-align: center; font-size: 12px; border-radius: 0 0 5px 5px; }
        .button { display: inline-block; padding: 15px 40px; background-color: #2196F3; color: white; text-decoration: none; border-radius: 5px; margin: 20px 0; font-size: 16px; font-weight: bold; }
        .button:hover { background-color: #1976D2; }
        .signing-info { background-color: #fff; padding: 20px; border-left: 4px solid #2196F3; margin: 20px 0; }
        .expiry-notice { background-color: #ffebee; border-left: 4px solid #f44336; padding: 15px; margin: 20px 0; }
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>Yêu cầu ký hợp đồng điện tử</h1>
            <p>Building & Assets Security Management System</p>
        </div>

        <div class='content'>
            <p>Kính gửi <strong>{customerName}</strong>,</p>

            <p>Chúng tôi đã hoàn tất việc chuẩn bị hợp đồng dịch vụ bảo vệ. Vui lòng xem xét và ký hợp đồng điện tử để hoàn tất thủ tục.</p>

            <div class='info-box'>
                <strong>Thông tin hợp đồng:</strong><br>
                <strong>Mã hợp đồng:</strong> {contractNumber}<br>
                <strong>Mã tài liệu:</strong> {documentId}
            </div>

            <div class='signing-info'>
                <strong>Hướng dẫn ký hợp đồng:</strong><br>
                1. Nhấn vào nút ""Ký hợp đồng ngay"" bên dưới<br>
                2. Đăng nhập vào hệ thống (nếu cần)<br>
                3. Xem xét kỹ nội dung hợp đồng<br>
                4. Thực hiện ký điện tử theo hướng dẫn
            </div>

            <center>
                <a href='{signingUrl}' class='button'>Ký hợp đồng ngay</a>
            </center>

            <div class='expiry-notice'>
                <strong>Lưu ý quan trọng:</strong><br>
                • Link ký hợp đồng này sẽ hết hạn vào: <strong>{expiredDateStr}</strong><br>
                • Vui lòng hoàn tất ký trước thời hạn trên<br>
                • Nếu link hết hạn, vui lòng liên hệ với chúng tôi để được cấp link mới
            </div>

            <div class='warning'>
                <strong>Bảo mật:</strong><br>
                • Link này chỉ dành riêng cho bạn, không chia sẻ cho người khác<br>
                • Nếu bạn không yêu cầu ký hợp đồng, vui lòng bỏ qua email này và thông báo cho chúng tôi<br>
                • Link có mã bảo mật và sẽ tự động hết hạn sau thời gian quy định
            </div>

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

        return template
            .Replace("{customerName}", customerName)
            .Replace("{contractNumber}", contractNumber)
            .Replace("{documentId}", documentId.ToString())
            .Replace("{signingUrl}", signingUrl)
            .Replace("{expiredDateStr}", expiredDateStr);
    }

    private string GenerateContractSignedConfirmationEmailBody(
        string customerName,
        string contractNumber,
        DateTime signedDate,
        string s3FileKey)
    {
        var signedDateStr = signedDate.ToString("dd/MM/yyyy HH:mm");

        // Extract tên file ngắn từ s3Key để tránh lỗi Word khi mở file
        var shortFileName = ExtractShortFileName(s3FileKey);

        // Tạo presigned URL từ S3 - hết hạn sau 7 ngày (10080 phút)
        var downloadUrl = _s3Service.GetPresignedUrl(s3FileKey, expirationMinutes: 10080, downloadFileName: shortFileName);

        var template = @"
<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <style>
        body { font-family: Arial, sans-serif; line-height: 1.6; color: #333; }
        .container { max-width: 600px; margin: 0 auto; padding: 20px; }
        .header { background-color: #4CAF50; color: white; padding: 20px; text-align: center; border-radius: 5px 5px 0 0; }
        .content { background-color: #f9f9f9; padding: 30px; border: 1px solid #ddd; }
        .success-box { background-color: #e8f5e9; border-left: 4px solid #4CAF50; padding: 20px; margin: 20px 0; border-radius: 5px; }
        .info-box { background-color: #e3f2fd; border-left: 4px solid #2196F3; padding: 15px; margin: 20px 0; }
        .next-steps { background-color: #fff; padding: 20px; border-left: 4px solid #FF9800; margin: 20px 0; }
        .reminder-box { background-color: #fff3cd; border-left: 4px solid #ffc107; padding: 15px; margin: 20px 0; }
        .footer { background-color: #333; color: white; padding: 15px; text-align: center; font-size: 12px; border-radius: 0 0 5px 5px; }
        .checkmark { font-size: 48px; color: #4CAF50; text-align: center; margin: 20px 0; }
        .highlight { color: #4CAF50; font-weight: bold; }
        ul { padding-left: 20px; }
        ul li { margin: 10px 0; }
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>Xác nhận chữ ký thành công</h1>
            <p>Building & Assets Security Management System</p>
        </div>

        <div class='content'>
            <div class='checkmark'>✓</div>

            <p>Kính gửi <strong>{customerName}</strong>,</p>

            <p>Chúng tôi xin chân thành cảm ơn Quý khách đã hoàn tất việc ký điện tử hợp đồng.</p>

            <div class='success-box'>
                <strong>Chữ ký điện tử của Quý khách đã được xác nhận thành công!</strong><br><br>
                <strong>📋 Mã hợp đồng:</strong> {contractNumber}<br>
                <strong>📅 Thời gian ký:</strong> {signedDateStr}<br>
                <strong>✅ Trạng thái:</strong> <span class='highlight'>Đã ký - Chờ phê duyệt</span>
            </div>

            <div class='info-box'>
                <strong>📥 Tải về hợp đồng đã ký</strong><br><br>
                Quý khách có thể tải về bản hợp đồng đã ký (định dạng DOCX) để lưu trữ và tham khảo.<br>
                <strong>Lưu ý:</strong> Link tải sẽ hết hạn sau <strong>7 ngày</strong>.<br><br>
                <center>
                    <a href='{downloadUrl}' class='button' style='background-color: #FF9800;'>📄 Tải về hợp đồng (DOCX)</a>
                </center>
            </div>

            <div class='next-steps'>
                <strong>📌 Các bước tiếp theo:</strong><br><br>
                <ul>
                    <li><strong>Xét duyệt hợp đồng:</strong> Bộ phận pháp lý và quản lý của chúng tôi sẽ xem xét và phê duyệt hợp đồng trong thời gian sớm nhất</li>
                    <li><strong>Thông báo kết quả:</strong> Quý khách sẽ nhận được email xác nhận ngay khi hợp đồng được phê duyệt</li>
                    <li><strong>Triển khai dịch vụ:</strong> Sau khi phê duyệt, chúng tôi sẽ liên hệ để sắp xếp lịch triển khai dịch vụ</li>
                </ul>
            </div>

            <div class='reminder-box'>
                <strong>📧 Lưu ý quan trọng:</strong><br>
                • Vui lòng <strong>thường xuyên kiểm tra hòm thư email</strong> của Quý khách để không bỏ lỡ các thông báo quan trọng<br>
                • Kiểm tra cả <strong>thư mục Spam/Junk Mail</strong> nếu không thấy email từ chúng tôi<br>
                • Thêm địa chỉ email <strong>support@basms.com</strong> vào danh bạ để đảm bảo nhận được thông báo<br>
                • Mọi cập nhật về tiến độ xét duyệt sẽ được gửi qua email này
            </div>

            <div class='info-box'>
                <strong>ℹ️ Thời gian xử lý dự kiến:</strong><br>
                Thông thường, quá trình xét duyệt hợp đồng sẽ hoàn tất trong vòng <strong>1-2 ngày làm việc</strong>.
                Chúng tôi cam kết sẽ xử lý hồ sơ của Quý khách một cách nhanh chóng và chính xác nhất.
            </div>

            <p style='margin-top: 30px;'>Nếu có bất kỳ thắc mắc hoặc cần hỗ trợ, vui lòng liên hệ:</p>
            <p>
                📞 Hotline: 1900-xxxx<br>
                📧 Email: support@basms.com<br>
                🌐 Website: www.basms.com<br>
                ⏰ Thời gian hỗ trợ: 8:00 - 17:30 (Thứ 2 - Thứ 6)
            </p>

            <p>Trân trọng,<br><strong>Đội ngũ BASMS</strong><br><em>Building & Assets Security Management System</em></p>
        </div>

        <div class='footer'>
            <p>© 2025 BASMS - Building & Assets Security Management System</p>
            <p>Email này được gửi tự động, vui lòng không trả lời trực tiếp.</p>
        </div>
    </div>
</body>
</html>
";

        return template
            .Replace("{customerName}", customerName)
            .Replace("{contractNumber}", contractNumber)
            .Replace("{signedDateStr}", signedDateStr)
            .Replace("{downloadUrl}", downloadUrl);
    }

    /// <summary>
    /// Gửi email cảnh báo hợp đồng sắp hết hạn (tiếng Việt)
    /// </summary>
    public async Task SendContractNearExpiryNotificationAsync(
        string recipientName,
        string recipientEmail,
        string contractNumber,
        string contractType,
        DateTime endDate,
        int daysRemaining)
    {
        var emailBody = GenerateContractNearExpiryEmailBody(
            recipientName,
            contractNumber,
            contractType,
            endDate,
            daysRemaining);

        var emailRequest = new EmailRequests
        {
            Email = recipientEmail,
            Subject = $"⚠️ Thông báo: Hợp đồng {contractNumber} sắp hết hạn trong {daysRemaining} ngày",
            EmailBody = emailBody
        };

        await SendEmailAsync(emailRequest);
    }

    private string GenerateContractNearExpiryEmailBody(
        string recipientName,
        string contractNumber,
        string contractType,
        DateTime endDate,
        int daysRemaining)
    {
        var endDateStr = endDate.ToString("dd/MM/yyyy");

        // Xác định loại hợp đồng bằng tiếng Việt
        var contractTypeVi = contractType switch
        {
            "working_contract" => "Hợp đồng lao động nhân viên bảo vệ",
            "manager_working_contract" => "Hợp đồng lao động quản lý",
            "extended_working_contract" => "Hợp đồng gia hạn",
            _ when contractType.Contains("service") => "Hợp đồng dịch vụ bảo vệ",
            _ => "Hợp đồng"
        };

        var urgencyColor = daysRemaining <= 3 ? "#f44336" : "#ff9800";
        var urgencyText = daysRemaining <= 3 ? "Khẩn cấp" : "Quan trọng";

        var template = @"
<!DOCTYPE html>
<html lang=""vi"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Thông báo hợp đồng sắp hết hạn</title>
</head>
<body style=""margin: 0; padding: 0; font-family: Arial, sans-serif; background-color: #f4f4f4;"">
    <table width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background-color: #f4f4f4; padding: 20px;"">
        <tr>
            <td align=""center"">
                <table width=""600"" cellpadding=""0"" cellspacing=""0"" style=""background-color: #ffffff; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.1);"">

                    <!-- Header với cảnh báo -->
                    <tr>
                        <td style=""background: linear-gradient(135deg, {urgencyColor} 0%, #d32f2f 100%); padding: 30px; text-align: center; border-radius: 8px 8px 0 0;"">
                            <div style=""font-size: 48px; margin-bottom: 10px;"">⚠️</div>
                            <h1 style=""color: #ffffff; margin: 0; font-size: 24px; font-weight: bold;"">
                                Thông báo {urgencyText}
                            </h1>
                            <p style=""color: #ffffff; margin: 10px 0 0 0; font-size: 16px;"">
                                Hợp đồng của bạn sắp hết hạn
                            </p>
                        </td>
                    </tr>

                    <!-- Content -->
                    <tr>
                        <td style=""padding: 40px 30px;"">
                            <p style=""color: #333333; font-size: 16px; line-height: 1.6; margin: 0 0 20px 0;"">
                                Kính gửi <strong>{recipientName}</strong>,
                            </p>

                            <p style=""color: #333333; font-size: 16px; line-height: 1.6; margin: 0 0 20px 0;"">
                                Chúng tôi xin thông báo rằng hợp đồng của bạn trong hệ thống BASMS sắp hết hạn.
                            </p>

                            <!-- Thông tin hợp đồng -->
                            <div style=""background-color: #fff3cd; border-left: 4px solid {urgencyColor}; padding: 20px; margin: 20px 0; border-radius: 4px;"">
                                <h2 style=""color: {urgencyColor}; margin: 0 0 15px 0; font-size: 18px;"">
                                    📋 Thông tin hợp đồng
                                </h2>

                                <table style=""width: 100%; border-collapse: collapse;"">
                                    <tr>
                                        <td style=""padding: 8px 0; color: #666666; font-size: 14px; width: 40%;"">
                                            <strong>Mã hợp đồng:</strong>
                                        </td>
                                        <td style=""padding: 8px 0; color: #333333; font-size: 14px;"">
                                            <strong>{contractNumber}</strong>
                                        </td>
                                    </tr>
                                    <tr>
                                        <td style=""padding: 8px 0; color: #666666; font-size: 14px;"">
                                            <strong>Loại hợp đồng:</strong>
                                        </td>
                                        <td style=""padding: 8px 0; color: #333333; font-size: 14px;"">
                                            {contractTypeVi}
                                        </td>
                                    </tr>
                                    <tr>
                                        <td style=""padding: 8px 0; color: #666666; font-size: 14px;"">
                                            <strong>Ngày hết hạn:</strong>
                                        </td>
                                        <td style=""padding: 8px 0; color: #333333; font-size: 14px;"">
                                            <strong style=""color: {urgencyColor};"">{endDateStr}</strong>
                                        </td>
                                    </tr>
                                </table>
                            </div>

                            <!-- Cảnh báo thời gian còn lại -->
                            <div style=""background-color: #ffebee; border: 2px solid {urgencyColor}; padding: 20px; margin: 20px 0; border-radius: 8px; text-align: center;"">
                                <div style=""font-size: 48px; font-weight: bold; color: {urgencyColor}; margin-bottom: 10px;"">
                                    {daysRemaining}
                                </div>
                                <div style=""font-size: 18px; color: #333333; font-weight: bold;"">
                                    Ngày còn lại đến khi hợp đồng hết hạn
                                </div>
                            </div>

                            <!-- Hành động cần thực hiện -->
                            <div style=""background-color: #e3f2fd; border-left: 4px solid #2196F3; padding: 20px; margin: 20px 0; border-radius: 4px;"">
                                <h3 style=""color: #2196F3; margin: 0 0 15px 0; font-size: 16px;"">
                                    📌 Hành động cần thực hiện
                                </h3>
                                <ul style=""color: #333333; font-size: 14px; line-height: 1.8; margin: 0; padding-left: 20px;"">
                                    <li><strong>Liên hệ ngay:</strong> Vui lòng liên hệ với bộ phận nhân sự hoặc quản lý để thảo luận về việc gia hạn hợp đồng</li>
                                    <li><strong>Chuẩn bị hồ sơ:</strong> Nếu có nhu cầu gia hạn, hãy chuẩn bị các giấy tờ cần thiết</li>
                                    <li><strong>Xác nhận quyết định:</strong> Thông báo quyết định của bạn về việc gia hạn hoặc kết thúc hợp đồng</li>
                                </ul>
                            </div>

                            <div style=""background-color: #f8f9fa; border-left: 4px solid #6c757d; padding: 15px; margin: 20px 0; border-radius: 4px;"">
                                <p style=""color: #333333; margin: 0; font-size: 14px; line-height: 1.6;"">
                                    <strong>Lưu ý:</strong> Nếu hợp đồng hết hạn mà chưa được gia hạn, quyền truy cập hệ thống của bạn sẽ bị tạm ngưng để đảm bảo bảo mật.
                                </p>
                            </div>

                            <center>
                                <a href=""https://anninhsinhtrac.com/login""
                                   style=""background: linear-gradient(135deg, #2196F3 0%, #1976D2 100%);
                                          color: #ffffff;
                                          padding: 14px 40px;
                                          text-decoration: none;
                                          border-radius: 6px;
                                          font-size: 16px;
                                          font-weight: bold;
                                          display: inline-block;
                                          box-shadow: 0 4px 6px rgba(33, 150, 243, 0.25);"">
                                    🔐 Đăng nhập hệ thống
                                </a>
                            </center>

                            <p style=""color: #666666; font-size: 14px; line-height: 1.6; margin: 30px 0 0 0;"">
                                Nếu bạn có bất kỳ câu hỏi nào hoặc cần hỗ trợ, vui lòng liên hệ:
                            </p>
                            <p style=""color: #666666; font-size: 14px; line-height: 1.8; margin: 10px 0;"">
                                📞 Hotline: 1900-xxxx<br>
                                📧 Email: support@basms.com<br>
                                🌐 Website: www.basms.com<br>
                                ⏰ Thời gian hỗ trợ: 8:00 - 17:30 (Thứ 2 - Thứ 6)
                            </p>
                        </td>
                    </tr>

                    <!-- Footer -->
                    <tr>
                        <td style=""background-color: #f8f9fa; padding: 20px 30px; border-radius: 0 0 8px 8px;"">
                            <p style=""color: #666666; font-size: 12px; line-height: 1.6; margin: 0 0 10px 0; text-align: center;"">
                                Email này được gửi tự động từ hệ thống BASMS<br>
                                Vui lòng không trả lời email này
                            </p>
                            <p style=""color: #999999; font-size: 11px; margin: 0; text-align: center;"">
                                © 2025 BASMS - Building & Apartment Security Management System
                            </p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";

        return template
            .Replace("{recipientName}", recipientName)
            .Replace("{contractNumber}", contractNumber)
            .Replace("{contractTypeVi}", contractTypeVi)
            .Replace("{endDateStr}", endDateStr)
            .Replace("{daysRemaining}", daysRemaining.ToString())
            .Replace("{urgencyColor}", urgencyColor)
            .Replace("{urgencyText}", urgencyText);
    }

    /// <summary>
    /// Extract tên file ngắn từ S3 key để tránh lỗi Word khi mở file
    /// VD: contracts/signed/.../SIGNED_abc123_HOP_DONG_LAO_DONG_NV_BAO_VE_22_11_2025.docx
    /// => HOP_DONG_LAO_DONG_NV_BAO_VE.docx
    /// </summary>
    private string ExtractShortFileName(string s3FileKey)
    {
        try
        {
            // Lấy filename từ S3 key (phần cuối sau dấu /)
            var fileName = Path.GetFileName(s3FileKey);
            var fileExtension = Path.GetExtension(fileName);
            var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);

            // Remove prefix SIGNED_ hoặc FILLED_
            if (nameWithoutExt.StartsWith("SIGNED_"))
                nameWithoutExt = nameWithoutExt.Substring("SIGNED_".Length);
            else if (nameWithoutExt.StartsWith("FILLED_"))
                nameWithoutExt = nameWithoutExt.Substring("FILLED_".Length);

            // Split by underscore
            var parts = nameWithoutExt.Split('_');

            if (parts.Length <= 2)
            {
                // Nếu không đủ parts, trả về tên gốc
                return fileName;
            }

            // Remove GUID (part[0]) và date (3 parts cuối: dd_MM_yyyy)
            // Giữ lại phần giữa (template key: HOP_DONG_LAO_DONG_...)
            var templateKeyParts = parts.Skip(1).Take(parts.Length - 4).ToArray();
            var shortName = string.Join("_", templateKeyParts);

            // Nếu shortName rỗng, fallback về tên gốc
            if (string.IsNullOrEmpty(shortName))
            {
                return fileName;
            }

            return $"{shortName}{fileExtension}";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract short filename from {S3Key}, using original", s3FileKey);
            return Path.GetFileName(s3FileKey);
        }
    }
}
