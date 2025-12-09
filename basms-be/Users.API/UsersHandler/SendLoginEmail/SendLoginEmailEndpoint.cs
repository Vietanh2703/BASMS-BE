using Carter;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Users.API.UsersHandler.SendLoginEmail;

/// <summary>
/// Endpoint để gửi email chứa thông tin đăng nhập cho user
/// </summary>
public class SendLoginEmailEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/users/send-login-email", async (
            [FromBody] SendLoginEmailRequest request,
            ISender sender,
            ILogger<SendLoginEmailEndpoint> logger,
            CancellationToken cancellationToken) =>
        {
            logger.LogInformation(
                "POST /api/users/send-login-email - Email: {Email}, Phone: {Phone}",
                request.Email,
                request.PhoneNumber);

            var command = new SendLoginEmailCommand(
                Email: request.Email,
                PhoneNumber: request.PhoneNumber
            );

            var result = await sender.Send(command, cancellationToken);

            if (!result.Success)
            {
                logger.LogWarning(
                    "Failed to send login email: {Error}",
                    result.ErrorMessage);

                return Results.BadRequest(new
                {
                    success = false,
                    error = result.ErrorMessage
                });
            }

            logger.LogInformation(
                "Login email sent successfully to {Email}",
                result.Email);

            return Results.Ok(new
            {
                success = true,
                email = result.Email,
                fullName = result.FullName,
                emailSent = result.EmailSent,
                message = $"Login credentials have been sent to {result.Email}. Please check your email and change your password after logging in."
            });
        })
        // .RequireAuthorization()
        .WithName("SendLoginEmail")
        .WithTags("Users - Authentication")
        .WithSummary("Gửi email chứa thông tin đăng nhập")
        .WithDescription(@"
Gửi email chứa thông tin đăng nhập cho user với password tạm thời mới.

## Request Body:
```json
{
  ""email"": ""user@example.com"",
  ""phoneNumber"": ""0901234567""
}
```

## Logic:
1. Tìm user theo email HOẶC phone number (tối ưu với index, LIMIT 1)
2. Generate password tạm thời mới (format: TEMPxxxxxxxx)
3. Hash password và update vào database
4. Gửi email đơn giản chứa thông tin đăng nhập

## Response Success (200):
```json
{
  ""success"": true,
  ""email"": ""user@example.com"",
  ""fullName"": ""Nguyen Van A"",
  ""emailSent"": true,
  ""message"": ""Login credentials have been sent to user@example.com...""
}
```

## Response Error (400):
```json
{
  ""success"": false,
  ""error"": ""User not found with provided email or phone number""
}
```

## Performance Notes:
- Query tối ưu với LIMIT 1 để handle 1 triệu users
- Sử dụng index trên Email và Phone columns
- MySQL sẽ dừng scan ngay khi tìm thấy match đầu tiên

## Security Notes:
- Password được hash bằng BCrypt trước khi lưu vào DB
- Password tạm thời nên được đổi ngay sau khi đăng nhập
- Email sẽ nhắc user đổi password
")
        .Produces(200)
        .Produces(400);
    }
}

/// <summary>
/// Request DTO cho send login email
/// </summary>
public record SendLoginEmailRequest
{
    /// <summary>
    /// Email của user
    /// </summary>
    public string Email { get; init; } = string.Empty;

    /// <summary>
    /// Số điện thoại của user
    /// </summary>
    public string PhoneNumber { get; init; } = string.Empty;
}
