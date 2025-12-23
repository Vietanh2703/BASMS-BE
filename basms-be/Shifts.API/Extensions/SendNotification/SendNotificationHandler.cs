using Shifts.API.Extensions;

namespace Shifts.API.Handlers.SendNotification;

public record SendNotificationCommand(
    Guid ShiftId,
    Guid? ContractId,
    Guid RecipientId,
    string RecipientType,    
    string Action,              
    string Title,
    string Message,
    string? Metadata = null,
    string Priority = "NORMAL"
) : ICommand<SendNotificationResult>;

public record SendNotificationResult(bool Success, Guid? NotificationId);

internal class SendNotificationHandler(
    IDbConnectionFactory dbFactory,
    ILogger<SendNotificationHandler> logger)
    : ICommandHandler<SendNotificationCommand, SendNotificationResult>
{
    public async Task<SendNotificationResult> Handle(
        SendNotificationCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation(
                "Sending {Action} notification to {RecipientType} {RecipientId}",
                request.Action,
                request.RecipientType,
                request.RecipientId);

            using var connection = await dbFactory.CreateConnectionAsync();

            var notification = new NotificationLog
            {
                Id = Guid.NewGuid(),
                ShiftId = request.ShiftId,
                ContractId = request.ContractId,
                RecipientId = request.RecipientId,
                RecipientType = request.RecipientType,
                Action = request.Action,
                Title = request.Title,
                Message = request.Message,
                Metadata = request.Metadata,
                DeliveryMethod = "IN_APP",
                Status = "PENDING",
                Priority = request.Priority,
                ExpiresAt = DateTime.UtcNow.AddDays(30),
                CreatedAt = DateTime.UtcNow
            };

            await connection.InsertAsync(notification);

            logger.LogInformation(
                "✓ Notification {NotificationId} created successfully",
                notification.Id);

            return new SendNotificationResult(true, notification.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Error sending notification to {RecipientType} {RecipientId}",
                request.RecipientType,
                request.RecipientId);

            return new SendNotificationResult(false, null);
        }
    }
}
