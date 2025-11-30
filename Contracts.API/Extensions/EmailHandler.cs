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
            Subject = "Yêu cầu ký hợp đồng điện tử - BASMS ✍️",
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
            Subject = "Xác nhận chữ ký hợp đồng thành công - BASMS ✅",
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
