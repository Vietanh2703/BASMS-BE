using BuildingBlocks.CQRS;

namespace Users.API.UsersHandler.SendLoginEmail;

/// <summary>
/// Command để gửi email chứa thông tin đăng nhập cho user
/// Reset password và gửi password tạm thời qua email
/// </summary>
public record SendLoginEmailCommand(
    string Email,
    string PhoneNumber
) : ICommand<SendLoginEmailResult>;

/// <summary>
/// Result sau khi gửi email đăng nhập
/// </summary>
public record SendLoginEmailResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public string? Email { get; init; }
    public string? FullName { get; init; }
    public bool EmailSent { get; init; }
}
