namespace Users.API.Extensions;

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
            email.Sender = new MailboxAddress("Safeguard System", _emailSettings.Sender);
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

    public async Task SendWelcomeEmailAsync(string fullName, string email, string password)
    {
        var emailBody = GenerateWelcomeEmailBody(fullName, email, password);
        var emailRequest = new EmailRequests
        {
            Email = email,
            Subject = "Welcome to Safeguard System! 🎉",
            EmailBody = emailBody
        };

        await SendEmailAsync(emailRequest);
    }

    public async Task SendPasswordResetEmailAsync(string fullName, string email, string resetLink)
    {
        var emailBody = GeneratePasswordResetEmailBody(fullName, resetLink);
        var emailRequest = new EmailRequests
        {
            Email = email,
            Subject = "Reset Your Password - Safeguard System",
            EmailBody = emailBody
        };

        await SendEmailAsync(emailRequest);
    }

    public async Task SendAccountVerificationEmailAsync(string fullName, string email, string verificationLink)
    {
        var emailBody = GenerateAccountVerificationEmailBody(fullName, verificationLink);
        var emailRequest = new EmailRequests
        {
            Email = email,
            Subject = "Verify Your Account - Safeguard System",
            EmailBody = emailBody
        };

        await SendEmailAsync(emailRequest);
    }

    public async Task SendEmailChangeNotificationAsync(string fullName, string oldEmail, string newEmail, bool isOldEmail)
    {
        var emailBody = GenerateEmailChangeNotificationBody(fullName, oldEmail, newEmail, isOldEmail);
        var targetEmail = isOldEmail ? oldEmail : newEmail;
        
        var emailRequest = new EmailRequests
        {
            Email = targetEmail,
            Subject = "⚠️ Email Address Changed - Safeguard System",
            EmailBody = emailBody
        };

        await SendEmailAsync(emailRequest);
    }

    public async Task SendOtpEmailAsync(string fullName, string email, string otpCode, string purpose, int expiryMinutes)
    {
        var emailBody = GenerateOtpEmailBody(fullName, otpCode, purpose, expiryMinutes);
        var emailRequest = new EmailRequests
        {
            Email = email,
            Subject = $"🔐 Your OTP Code - Safeguard System",
            EmailBody = emailBody
        };

        await SendEmailAsync(emailRequest);
    }

    public async Task SendPasswordChangeOtpEmailAsync(string fullName, string email, string otpCode, int expiryMinutes)
    {
        var emailBody = GeneratePasswordChangeOtpEmailBody(fullName, otpCode, expiryMinutes);
        var emailRequest = new EmailRequests
        {
            Email = email,
            Subject = "🔐 Confirm Password Change - Safeguard System",
            EmailBody = emailBody
        };

        await SendEmailAsync(emailRequest);
    }

    public string GenerateWelcomeEmailBody(string fullName, string email, string password)
    {
        var loginUrl = "http://localhost:5173/login";
        var body = @"
<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <style>
        @import url('https://fonts.googleapis.com/css2?family=Inter:wght@400;600;700&display=swap');
    </style>
</head>
<body style='margin: 0; padding: 0; background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); font-family: Inter, Arial, sans-serif;'>
    <table width='100%' cellpadding='0' cellspacing='0' style='background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); padding: 40px 20px;'>
        <tr>
            <td align='center'>
                <table width='600' cellpadding='0' cellspacing='0' style='background: #ffffff; border-radius: 16px; box-shadow: 0 10px 40px rgba(0,0,0,0.1); overflow: hidden;'>
                    <!-- Header with gradient -->
                    <tr>
                        <td style='background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); padding: 40px 30px; text-align: center;'>
                            <h1 style='color: #ffffff; margin: 0; font-size: 42px; font-weight: 700; text-shadow: 2px 2px 4px rgba(0,0,0,0.2);'>
                                🛡️ MyGuard
                            </h1>
                            <p style='color: rgba(255,255,255,0.9); margin: 10px 0 0 0; font-size: 16px;'>Security Management System</p>
                        </td>
                    </tr>
                    
                    <!-- Welcome message -->
                    <tr>
                        <td style='padding: 40px 30px;'>
                            <div style='text-align: center; margin-bottom: 30px;'>
                                <div style='display: inline-block; background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); width: 80px; height: 80px; border-radius: 50%; line-height: 80px; margin-bottom: 20px;'>
                                    <span style='font-size: 40px;'>🎉</span>
                                </div>
                                <h2 style='color: #1a202c; margin: 0; font-size: 28px; font-weight: 700;'>Welcome Aboard!</h2>
                                <p style='color: #718096; margin: 10px 0 0 0; font-size: 16px;'>We're excited to have you join our team</p>
                            </div>
                            
                            <p style='color: #2d3748; font-size: 16px; line-height: 1.6; margin: 0 0 20px 0;'>
                                Hi <strong>" + fullName + @"</strong>,
                            </p>
                            
                            <p style='color: #4a5568; font-size: 15px; line-height: 1.6; margin: 0 0 30px 0;'>
                                Your account has been successfully created in the <strong>Safeguard Assignment & Management System</strong>. Below are your login credentials:
                            </p>
                            
                            <!-- Credentials box -->
                            <div style='background: linear-gradient(135deg, #f7fafc 0%, #edf2f7 100%); border-left: 4px solid #667eea; padding: 20px; border-radius: 8px; margin-bottom: 30px;'>
                                <table width='100%' cellpadding='8' cellspacing='0'>
                                    <tr>
                                        <td style='color: #4a5568; font-size: 14px; font-weight: 600; width: 140px;'>
                                            📧 Email:
                                        </td>
                                        <td style='color: #2d3748; font-size: 14px; font-family: monospace;'>
                                            " + email + @"
                                        </td>
                                    </tr>
                                    <tr>
                                        <td style='color: #4a5568; font-size: 14px; font-weight: 600; padding-top: 8px;'>
                                            🔐 Password:
                                        </td>
                                        <td style='color: #2d3748; font-size: 14px; font-family: monospace; padding-top: 8px;'>
                                            " + password + @"
                                        </td>
                                    </tr>
                                </table>
                            </div>
                            
                            <!-- Security notice -->
                            <div style='background: #fff5f5; border-left: 4px solid #fc8181; padding: 15px; border-radius: 8px; margin-bottom: 30px;'>
                                <p style='color: #c53030; font-size: 14px; margin: 0; line-height: 1.5;'>
                                    <strong>⚠️ Security Notice:</strong> For security reasons, please log in and change your password immediately.
                                </p>
                            </div>
                            
                            <!-- Next Steps -->
                            <h3 style='color: #2d3748; font-size: 18px; margin: 0 0 15px 0; font-weight: 600;'>📋 Next Steps:</h3>
                            <table width='100%' cellpadding='0' cellspacing='0' style='margin-bottom: 30px;'>
                                <tr>
                                    <td style='padding: 10px 0;'>
                                        <table cellpadding='0' cellspacing='0'>
                                            <tr>
                                                <td style='width: 30px; vertical-align: top;'>
                                                    <div style='background: #667eea; color: white; width: 24px; height: 24px; border-radius: 50%; text-align: center; line-height: 24px; font-size: 12px; font-weight: 600;'>1</div>
                                                </td>
                                                <td style='color: #4a5568; font-size: 15px; line-height: 1.5; padding-left: 10px;'>
                                                    Click the button below to log in to your account
                                                </td>
                                            </tr>
                                        </table>
                                    </td>
                                </tr>
                                <tr>
                                    <td style='padding: 10px 0;'>
                                        <table cellpadding='0' cellspacing='0'>
                                            <tr>
                                                <td style='width: 30px; vertical-align: top;'>
                                                    <div style='background: #667eea; color: white; width: 24px; height: 24px; border-radius: 50%; text-align: center; line-height: 24px; font-size: 12px; font-weight: 600;'>2</div>
                                                </td>
                                                <td style='color: #4a5568; font-size: 15px; line-height: 1.5; padding-left: 10px;'>
                                                    Update your password in your profile settings
                                                </td>
                                            </tr>
                                        </table>
                                    </td>
                                </tr>
                                <tr>
                                    <td style='padding: 10px 0;'>
                                        <table cellpadding='0' cellspacing='0'>
                                            <tr>
                                                <td style='width: 30px; vertical-align: top;'>
                                                    <div style='background: #667eea; color: white; width: 24px; height: 24px; border-radius: 50%; text-align: center; line-height: 24px; font-size: 12px; font-weight: 600;'>3</div>
                                                </td>
                                                <td style='color: #4a5568; font-size: 15px; line-height: 1.5; padding-left: 10px;'>
                                                    Explore your dashboard and start managing your tasks
                                                </td>
                                            </tr>
                                        </table>
                                    </td>
                                </tr>
                            </table>
                            
                            <!-- CTA Button -->
                            <div style='text-align: center; margin: 40px 0;'>
                                <a href='" + loginUrl + @"' style='background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: #ffffff; padding: 16px 40px; text-decoration: none; border-radius: 8px; font-size: 16px; font-weight: 600; display: inline-block; box-shadow: 0 4px 15px rgba(102, 126, 234, 0.4); transition: all 0.3s;'>
                                    🚀 Access Your Dashboard
                                </a>
                            </div>
                            
                            <!-- Support -->
                            <div style='background: #f7fafc; padding: 20px; border-radius: 8px; text-align: center; margin-top: 30px;'>
                                <p style='color: #4a5568; font-size: 14px; margin: 0 0 10px 0;'>
                                    Need help? Our support team is here for you!
                                </p>
                                <p style='color: #667eea; font-size: 14px; margin: 0; font-weight: 600;'>
                                    📧 vietanhcodega123@gmail.com
                                </p>
                            </div>
                        </td>
                    </tr>
                    
                    <!-- Footer -->
                    <tr>
                        <td style='background: #2d3748; padding: 30px; text-align: center;'>
                            <p style='color: #a0aec0; font-size: 13px; margin: 0 0 10px 0;'>
                                © 2025 Safeguard Assignment & Management System
                            </p>
                            <p style='color: #718096; font-size: 12px; margin: 0;'>
                                Protecting what matters most 🛡️
                            </p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";
        return body;
    }

    public string GeneratePasswordResetEmailBody(string fullName, string resetLink)
    {
        var body = @"
<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
</head>
<body style='margin: 0; padding: 0; background: linear-gradient(135deg, #f093fb 0%, #f5576c 100%); font-family: Inter, Arial, sans-serif;'>
    <table width='100%' cellpadding='0' cellspacing='0' style='background: linear-gradient(135deg, #f093fb 0%, #f5576c 100%); padding: 40px 20px;'>
        <tr>
            <td align='center'>
                <table width='600' cellpadding='0' cellspacing='0' style='background: #ffffff; border-radius: 16px; box-shadow: 0 10px 40px rgba(0,0,0,0.1); overflow: hidden;'>
                    <tr>
                        <td style='background: linear-gradient(135deg, #f093fb 0%, #f5576c 100%); padding: 40px 30px; text-align: center;'>
                            <h1 style='color: #ffffff; margin: 0; font-size: 42px; font-weight: 700;'>🛡️ MyGuard</h1>
                        </td>
                    </tr>
                    <tr>
                        <td style='padding: 40px 30px; text-align: center;'>
                            <div style='display: inline-block; background: linear-gradient(135deg, #f093fb 0%, #f5576c 100%); width: 80px; height: 80px; border-radius: 50%; line-height: 80px; margin-bottom: 20px;'>
                                <span style='font-size: 40px;'>🔒</span>
                            </div>
                            <h2 style='color: #1a202c; margin: 0 0 10px 0; font-size: 28px; font-weight: 700;'>Password Reset Request</h2>
                            <p style='color: #4a5568; font-size: 16px; margin: 0 0 30px 0;'>
                                Hi <strong>" + fullName + @"</strong>,
                            </p>
                            <p style='color: #4a5568; font-size: 15px; line-height: 1.6; margin: 0 0 30px 0;'>
                                We received a request to reset your password. Click the button below to create a new password:
                            </p>
                            <div style='margin: 40px 0;'>
                                <a href='" + resetLink + @"' style='background: linear-gradient(135deg, #f093fb 0%, #f5576c 100%); color: #ffffff; padding: 16px 40px; text-decoration: none; border-radius: 8px; font-size: 16px; font-weight: 600; display: inline-block; box-shadow: 0 4px 15px rgba(245, 87, 108, 0.4);'>
                                    🔐 Reset Password
                                </a>
                            </div>
                            <div style='background: #fff5f5; border-left: 4px solid #fc8181; padding: 15px; border-radius: 8px; text-align: left;'>
                                <p style='color: #c53030; font-size: 14px; margin: 0;'>
                                    <strong>⏰ Note:</strong> This link will expire in 1 hour for security reasons.
                                </p>
                            </div>
                            <p style='color: #718096; font-size: 14px; margin: 20px 0 0 0;'>
                                If you didn't request this, please ignore this email.
                            </p>
                        </td>
                    </tr>
                    <tr>
                        <td style='background: #2d3748; padding: 30px; text-align: center;'>
                            <p style='color: #a0aec0; font-size: 13px; margin: 0;'>© 2025 Safeguard System. All rights reserved.</p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";
        return body;
    }

    public string GenerateAccountVerificationEmailBody(string fullName, string verificationLink)
    {
        var body = @"
<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
</head>
<body style='margin: 0; padding: 0; background: linear-gradient(135deg, #4facfe 0%, #00f2fe 100%); font-family: Inter, Arial, sans-serif;'>
    <table width='100%' cellpadding='0' cellspacing='0' style='background: linear-gradient(135deg, #4facfe 0%, #00f2fe 100%); padding: 40px 20px;'>
        <tr>
            <td align='center'>
                <table width='600' cellpadding='0' cellspacing='0' style='background: #ffffff; border-radius: 16px; box-shadow: 0 10px 40px rgba(0,0,0,0.1); overflow: hidden;'>
                    <tr>
                        <td style='background: linear-gradient(135deg, #4facfe 0%, #00f2fe 100%); padding: 40px 30px; text-align: center;'>
                            <h1 style='color: #ffffff; margin: 0; font-size: 42px; font-weight: 700;'>🛡️ MyGuard</h1>
                        </td>
                    </tr>
                    <tr>
                        <td style='padding: 40px 30px; text-align: center;'>
                            <div style='display: inline-block; background: linear-gradient(135deg, #4facfe 0%, #00f2fe 100%); width: 80px; height: 80px; border-radius: 50%; line-height: 80px; margin-bottom: 20px;'>
                                <span style='font-size: 40px;'>✉️</span>
                            </div>
                            <h2 style='color: #1a202c; margin: 0 0 10px 0; font-size: 28px; font-weight: 700;'>Verify Your Email</h2>
                            <p style='color: #4a5568; font-size: 16px; margin: 0 0 30px 0;'>
                                Hi <strong>" + fullName + @"</strong>,
                            </p>
                            <p style='color: #4a5568; font-size: 15px; line-height: 1.6; margin: 0 0 30px 0;'>
                                Thank you for registering! Please verify your email address by clicking the button below:
                            </p>
                            <div style='margin: 40px 0;'>
                                <a href='" + verificationLink + @"' style='background: linear-gradient(135deg, #4facfe 0%, #00f2fe 100%); color: #ffffff; padding: 16px 40px; text-decoration: none; border-radius: 8px; font-size: 16px; font-weight: 600; display: inline-block; box-shadow: 0 4px 15px rgba(79, 172, 254, 0.4);'>
                                    ✅ Verify Email
                                </a>
                            </div>
                            <p style='color: #718096; font-size: 14px; margin: 0;'>
                                This verification link will expire in 24 hours.
                            </p>
                        </td>
                    </tr>
                    <tr>
                        <td style='background: #2d3748; padding: 30px; text-align: center;'>
                            <p style='color: #a0aec0; font-size: 13px; margin: 0;'>© 2025 Safeguard System. All rights reserved.</p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";
        return body;
    }

    public string GenerateEmailChangeNotificationBody(string fullName, string oldEmail, string newEmail, bool isOldEmail)
    {
        var supportEmail = "vietanhcodega123@gmail.com";
        var loginUrl = "http://localhost:5173/login";
        
        var body = @"
<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
</head>
<body style='margin: 0; padding: 0; background: linear-gradient(135deg, #ff9a56 0%, #ff6a88 100%); font-family: Inter, Arial, sans-serif;'>
    <table width='100%' cellpadding='0' cellspacing='0' style='background: linear-gradient(135deg, #ff9a56 0%, #ff6a88 100%); padding: 40px 20px;'>
        <tr>
            <td align='center'>
                <table width='600' cellpadding='0' cellspacing='0' style='background: #ffffff; border-radius: 16px; box-shadow: 0 10px 40px rgba(0,0,0,0.1); overflow: hidden;'>
                    <!-- Header -->
                    <tr>
                        <td style='background: linear-gradient(135deg, #ff9a56 0%, #ff6a88 100%); padding: 40px 30px; text-align: center;'>
                            <h1 style='color: #ffffff; margin: 0; font-size: 42px; font-weight: 700; text-shadow: 2px 2px 4px rgba(0,0,0,0.2);'>
                                🛡️ MyGuard
                            </h1>
                            <p style='color: rgba(255,255,255,0.9); margin: 10px 0 0 0; font-size: 16px;'>Security Management System</p>
                        </td>
                    </tr>
                    
                    <!-- Content -->
                    <tr>
                        <td style='padding: 40px 30px;'>
                            <div style='text-align: center; margin-bottom: 30px;'>
                                <div style='display: inline-block; background: linear-gradient(135deg, #ff9a56 0%, #ff6a88 100%); width: 80px; height: 80px; border-radius: 50%; line-height: 80px; margin-bottom: 20px;'>
                                    <span style='font-size: 40px;'>⚠️</span>
                                </div>
                                <h2 style='color: #1a202c; margin: 0; font-size: 28px; font-weight: 700;'>Email Address Changed</h2>
                                <p style='color: #718096; margin: 10px 0 0 0; font-size: 16px;'>Your account email has been updated</p>
                            </div>
                            
                            <p style='color: #2d3748; font-size: 16px; line-height: 1.6; margin: 0 0 20px 0;'>
                                Hi <strong>" + fullName + @"</strong>,
                            </p>
                            
                            <p style='color: #4a5568; font-size: 15px; line-height: 1.6; margin: 0 0 30px 0;'>" 
                                + (isOldEmail 
                                    ? "This is to inform you that the email address associated with your Safeguard account has been changed." 
                                    : "Welcome to your new email address! Your Safeguard account email has been successfully updated.") + @"
                            </p>
                            
                            <!-- Email Change Details -->
                            <div style='background: linear-gradient(135deg, #fff5f5 0%, #fed7d7 100%); border-left: 4px solid #fc8181; padding: 20px; border-radius: 8px; margin-bottom: 30px;'>
                                <table width='100%' cellpadding='8' cellspacing='0'>
                                    <tr>
                                        <td style='color: #c53030; font-size: 14px; font-weight: 600; width: 140px;'>
                                            📧 Old Email:
                                        </td>
                                        <td style='color: #742a2a; font-size: 14px; font-family: monospace;'>
                                            " + oldEmail + @"
                                        </td>
                                    </tr>
                                    <tr>
                                        <td style='color: #c53030; font-size: 14px; font-weight: 600; padding-top: 8px;'>
                                            ✅ New Email:
                                        </td>
                                        <td style='color: #742a2a; font-size: 14px; font-family: monospace; padding-top: 8px;'>
                                            " + newEmail + @"
                                        </td>
                                    </tr>
                                    <tr>
                                        <td style='color: #c53030; font-size: 14px; font-weight: 600; padding-top: 8px;'>
                                            🕐 Changed At:
                                        </td>
                                        <td style='color: #742a2a; font-size: 14px; font-family: monospace; padding-top: 8px;'>
                                            " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC") + @"
                                        </td>
                                    </tr>
                                </table>
                            </div>";

        if (isOldEmail)
        {
            body += @"
                            <!-- Security Warning for Old Email -->
                            <div style='background: #fffaf0; border-left: 4px solid #f6ad55; padding: 20px; border-radius: 8px; margin-bottom: 30px;'>
                                <h3 style='color: #c05621; font-size: 16px; margin: 0 0 10px 0; font-weight: 600;'>🔒 Security Notice</h3>
                                <p style='color: #744210; font-size: 14px; margin: 0; line-height: 1.6;'>
                                    <strong>Did you make this change?</strong><br>
                                    If you did not authorize this email change, please contact our support team immediately at <strong>" + supportEmail + @"</strong> to secure your account.
                                </p>
                            </div>
                            
                            <p style='color: #4a5568; font-size: 14px; line-height: 1.6; margin: 0 0 20px 0;'>
                                • This email address will no longer be able to log in to your account<br>
                                • All future communications will be sent to the new email address<br>
                                • If this change was unauthorized, contact support immediately
                            </p>";
        }
        else
        {
            body += @"
                            <!-- Welcome Message for New Email -->
                            <div style='background: #f0fff4; border-left: 4px solid #48bb78; padding: 20px; border-radius: 8px; margin-bottom: 30px;'>
                                <h3 style='color: #22543d; font-size: 16px; margin: 0 0 10px 0; font-weight: 600;'>✅ Email Successfully Updated</h3>
                                <p style='color: #276749; font-size: 14px; margin: 0; line-height: 1.6;'>
                                    Your email has been successfully changed. You can now use this email address to log in to your Safeguard account.
                                </p>
                            </div>
                            
                            <p style='color: #4a5568; font-size: 14px; line-height: 1.6; margin: 0 0 20px 0;'>
                                • Use this new email address for all future logins<br>
                                • All notifications will now be sent to this email<br>
                                • Your password remains unchanged
                            </p>
                            
                            <div style='text-align: center; margin: 30px 0;'>
                                <a href='" + loginUrl + @"' style='background: linear-gradient(135deg, #ff9a56 0%, #ff6a88 100%); color: #ffffff; padding: 16px 40px; text-decoration: none; border-radius: 8px; font-size: 16px; font-weight: 600; display: inline-block; box-shadow: 0 4px 15px rgba(255, 106, 136, 0.4);'>
                                    🚀 Log In Now
                                </a>
                            </div>";
        }

        body += @"
                            <!-- Support Section -->
                            <div style='background: #f7fafc; padding: 20px; border-radius: 8px; text-align: center; margin-top: 30px;'>
                                <p style='color: #4a5568; font-size: 14px; margin: 0 0 10px 0;'>
                                    Questions or concerns? We're here to help!
                                </p>
                                <p style='color: #ff6a88; font-size: 14px; margin: 0; font-weight: 600;'>
                                    📧 " + supportEmail + @"
                                </p>
                            </div>
                        </td>
                    </tr>
                    
                    <!-- Footer -->
                    <tr>
                        <td style='background: #2d3748; padding: 30px; text-align: center;'>
                            <p style='color: #a0aec0; font-size: 13px; margin: 0 0 10px 0;'>
                                © 2025 Safeguard Assignment & Management System
                            </p>
                            <p style='color: #718096; font-size: 12px; margin: 0;'>
                                Protecting what matters most 🛡️
                            </p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";
        return body;
    }

    public string GenerateOtpEmailBody(string fullName, string otpCode, string purpose, int expiryMinutes)
    {
        var purposeText = purpose switch
        {
            "login" => "sign in to your account",
            "verify_email" => "verify your email address",
            "reset_password" => "reset your password",
            _ => "complete your request"
        };

        var body = @"
<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
</head>
<body style='margin: 0; padding: 0; background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); font-family: Inter, Arial, sans-serif;'>
    <table width='100%' cellpadding='0' cellspacing='0' style='background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); padding: 40px 20px;'>
        <tr>
            <td align='center'>
                <table width='600' cellpadding='0' cellspacing='0' style='background: #ffffff; border-radius: 16px; box-shadow: 0 10px 40px rgba(0,0,0,0.1); overflow: hidden;'>
                    <!-- Header -->
                    <tr>
                        <td style='background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); padding: 40px 30px; text-align: center;'>
                            <h1 style='color: #ffffff; margin: 0; font-size: 42px; font-weight: 700; text-shadow: 2px 2px 4px rgba(0,0,0,0.2);'>
                                🛡️ MyGuard
                            </h1>
                            <p style='color: rgba(255,255,255,0.9); margin: 10px 0 0 0; font-size: 16px;'>Security Management System</p>
                        </td>
                    </tr>
                    
                    <!-- Content -->
                    <tr>
                        <td style='padding: 40px 30px;'>
                            <div style='text-align: center; margin-bottom: 30px;'>
                                <div style='display: inline-block; background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); width: 80px; height: 80px; border-radius: 50%; line-height: 80px; margin-bottom: 20px;'>
                                    <span style='font-size: 40px;'>🔐</span>
                                </div>
                                <h2 style='color: #1a202c; margin: 0; font-size: 28px; font-weight: 700;'>Your OTP Code</h2>
                                <p style='color: #718096; margin: 10px 0 0 0; font-size: 16px;'>Use this code to " + purposeText + @"</p>
                            </div>
                            
                            <p style='color: #2d3748; font-size: 16px; line-height: 1.6; margin: 0 0 20px 0;'>
                                Hi <strong>" + fullName + @"</strong>,
                            </p>
                            
                            <p style='color: #4a5568; font-size: 15px; line-height: 1.6; margin: 0 0 30px 0;'>
                                You have requested an OTP code to " + purposeText + @". Please use the code below:
                            </p>
                            
                            <!-- OTP Code Box -->
                            <div style='text-align: center; margin: 40px 0;'>
                                <div style='background: linear-gradient(135deg, #f7fafc 0%, #edf2f7 100%); border: 3px dashed #667eea; padding: 30px; border-radius: 12px; display: inline-block;'>
                                    <p style='color: #718096; font-size: 14px; margin: 0 0 10px 0; text-transform: uppercase; letter-spacing: 1px;'>Your OTP Code</p>
                                    <p style='color: #667eea; font-size: 48px; font-weight: 700; margin: 0; letter-spacing: 8px; font-family: monospace;'>
                                        " + otpCode + @"
                                    </p>
                                </div>
                            </div>
                            
                            <!-- Expiry Warning -->
                            <div style='background: #fff5f5; border-left: 4px solid #fc8181; padding: 15px; border-radius: 8px; margin-bottom: 30px;'>
                                <p style='color: #c53030; font-size: 14px; margin: 0; line-height: 1.5;'>
                                    <strong>⏰ Important:</strong> This code will expire in <strong>" + expiryMinutes + @" minutes</strong>. Do not share this code with anyone.
                                </p>
                            </div>
                            
                            <!-- Security Tips -->
                            <div style='background: #f7fafc; padding: 20px; border-radius: 8px; margin-bottom: 20px;'>
                                <h3 style='color: #2d3748; font-size: 16px; margin: 0 0 15px 0; font-weight: 600;'>🛡️ Security Tips:</h3>
                                <ul style='color: #4a5568; font-size: 14px; margin: 0; padding-left: 20px; line-height: 1.8;'>
                                    <li>Never share your OTP code with anyone</li>
                                    <li>Safeguard staff will never ask for your OTP</li>
                                    <li>If you didn't request this code, please ignore this email</li>
                                    <li>Contact support if you suspect unauthorized access</li>
                                </ul>
                            </div>
                            
                            <p style='color: #718096; font-size: 14px; margin: 20px 0 0 0; text-align: center;'>
                                If you didn't request this OTP, please ignore this email or contact our support team.
                            </p>
                            
                            <!-- Support -->
                            <div style='background: #f7fafc; padding: 20px; border-radius: 8px; text-align: center; margin-top: 30px;'>
                                <p style='color: #4a5568; font-size: 14px; margin: 0 0 10px 0;'>
                                    Need help? Contact our support team
                                </p>
                                <p style='color: #667eea; font-size: 14px; margin: 0; font-weight: 600;'>
                                    📧 vietanhcodega123@gmail.com
                                </p>
                            </div>
                        </td>
                    </tr>
                    
                    <!-- Footer -->
                    <tr>
                        <td style='background: #2d3748; padding: 30px; text-align: center;'>
                            <p style='color: #a0aec0; font-size: 13px; margin: 0 0 10px 0;'>
                                © 2025 Safeguard Assignment & Management System
                            </p>
                            <p style='color: #718096; font-size: 12px; margin: 0;'>
                                Protecting what matters most 🛡️
                            </p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";
        return body;
    }

    public string GeneratePasswordChangeOtpEmailBody(string fullName, string otpCode, int expiryMinutes)
    {
        var body = @"
<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
</head>
<body style='margin: 0; padding: 0; background: linear-gradient(135deg, #f093fb 0%, #f5576c 100%); font-family: Inter, Arial, sans-serif;'>
    <table width='100%' cellpadding='0' cellspacing='0' style='background: linear-gradient(135deg, #f093fb 0%, #f5576c 100%); padding: 40px 20px;'>
        <tr>
            <td align='center'>
                <table width='600' cellpadding='0' cellspacing='0' style='background: #ffffff; border-radius: 16px; box-shadow: 0 10px 40px rgba(0,0,0,0.1); overflow: hidden;'>
                    <tr>
                        <td style='background: linear-gradient(135deg, #f093fb 0%, #f5576c 100%); padding: 40px 30px; text-align: center;'>
                            <h1 style='color: #ffffff; margin: 0; font-size: 42px; font-weight: 700;'>🛡️ MyGuard</h1>
                        </td>
                    </tr>
                    <tr>
                        <td style='padding: 40px 30px;'>
                            <div style='text-align: center; margin-bottom: 30px;'>
                                <div style='display: inline-block; background: linear-gradient(135deg, #f093fb 0%, #f5576c 100%); width: 80px; height: 80px; border-radius: 50%; line-height: 80px; margin-bottom: 20px;'>
                                    <span style='font-size: 40px;'>🔐</span>
                                </div>
                                <h2 style='color: #1a202c; margin: 0; font-size: 28px; font-weight: 700;'>Password Change Confirmation</h2>
                            </div>
                            <p style='color: #2d3748; font-size: 16px; margin: 0 0 20px 0;'>Hi <strong>" + fullName + @"</strong>,</p>
                            <p style='color: #4a5568; font-size: 15px; margin: 0 0 30px 0;'>You have requested to change your password. Please use the OTP code below to confirm this change:</p>
                            <div style='text-align: center; margin: 40px 0;'>
                                <div style='background: linear-gradient(135deg, #fff5f5 0%, #fed7d7 100%); border: 3px dashed #f5576c; padding: 30px; border-radius: 12px; display: inline-block;'>
                                    <p style='color: #718096; font-size: 14px; margin: 0 0 10px 0;'>YOUR OTP CODE</p>
                                    <p style='color: #f5576c; font-size: 48px; font-weight: 700; margin: 0; letter-spacing: 8px; font-family: monospace;'>" + otpCode + @"</p>
                                </div>
                            </div>
                            <div style='background: #fff5f5; border-left: 4px solid #fc8181; padding: 15px; border-radius: 8px;'>
                                <p style='color: #c53030; font-size: 14px; margin: 0;'><strong>⏰ Important:</strong> This code expires in " + expiryMinutes + @" minutes. If you didn't request this, please ignore this email.</p>
                            </div>
                        </td>
                    </tr>
                    <tr>
                        <td style='background: #2d3748; padding: 30px; text-align: center;'>
                            <p style='color: #a0aec0; font-size: 13px; margin: 0;'>© 2025 Safeguard System</p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";
        return body;
    }
}

