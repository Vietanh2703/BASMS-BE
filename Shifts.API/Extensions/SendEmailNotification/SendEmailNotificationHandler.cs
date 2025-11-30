using Shifts.API.Extensions;

namespace Shifts.API.Handlers.SendEmailNotification;

/// <summary>
/// Command để gửi email notification
/// </summary>
public record SendEmailNotificationCommand(
    string GuardName,
    string GuardEmail,
    DateTime ShiftDate,
    TimeSpan StartTime,
    TimeSpan EndTime,
    string Location,
    string EmailType,           // CANCELLATION | CREATED | UPDATED
    string? AdditionalInfo = null  // Cancellation reason hoặc changes
) : ICommand<SendEmailNotificationResult>;

public record SendEmailNotificationResult(bool Success, string Message);

internal class SendEmailNotificationHandler(
    EmailHandler emailHandler,
    ILogger<SendEmailNotificationHandler> logger)
    : ICommandHandler<SendEmailNotificationCommand, SendEmailNotificationResult>
{
    public async Task<SendEmailNotificationResult> Handle(
        SendEmailNotificationCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation(
                "Sending {EmailType} email to {Email}",
                request.EmailType,
                request.GuardEmail);

            switch (request.EmailType.ToUpper())
            {
                case "CANCELLATION":
                    await emailHandler.SendShiftCancellationEmailAsync(
                        request.GuardName,
                        request.GuardEmail,
                        request.ShiftDate,
                        request.StartTime,
                        request.EndTime,
                        request.Location,
                        request.AdditionalInfo ?? "Không có lý do");
                    break;

                case "CREATED":
                    await emailHandler.SendShiftCreatedEmailAsync(
                        request.GuardName,
                        request.GuardEmail,
                        request.ShiftDate,
                        request.StartTime,
                        request.EndTime,
                        request.Location,
                        request.AdditionalInfo ?? "REGULAR");
                    break;

                case "UPDATED":
                    await emailHandler.SendShiftUpdatedEmailAsync(
                        request.GuardName,
                        request.GuardEmail,
                        request.ShiftDate,
                        request.StartTime,
                        request.EndTime,
                        request.Location,
                        request.AdditionalInfo ?? "Ca trực đã được cập nhật");
                    break;

                default:
                    logger.LogWarning("Unknown email type: {EmailType}", request.EmailType);
                    return new SendEmailNotificationResult(false, $"Unknown email type: {request.EmailType}");
            }

            logger.LogInformation(
                "✓ Email sent successfully to {Email}",
                request.GuardEmail);

            return new SendEmailNotificationResult(true, "Email sent successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Error sending {EmailType} email to {Email}",
                request.EmailType,
                request.GuardEmail);

            return new SendEmailNotificationResult(false, $"Failed to send email: {ex.Message}");
        }
    }
}
