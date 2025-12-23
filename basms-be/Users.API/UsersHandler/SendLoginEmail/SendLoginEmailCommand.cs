using BuildingBlocks.CQRS;

namespace Users.API.UsersHandler.SendLoginEmail;

public record SendLoginEmailCommand(
    string Email,
    string PhoneNumber
) : ICommand<SendLoginEmailResult>;

public record SendLoginEmailResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public string? Email { get; init; }
    public string? FullName { get; init; }
    public bool EmailSent { get; init; }
}
