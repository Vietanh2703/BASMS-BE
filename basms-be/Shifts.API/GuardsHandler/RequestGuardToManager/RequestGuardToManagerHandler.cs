namespace Shifts.API.GuardsHandler.RequestGuardToManager;

public record RequestGuardToManagerCommand(
    Guid GuardId,
    Guid ManagerId,
    string? RequestNote
) : ICommand<RequestGuardToManagerResult>;


public record RequestGuardToManagerResult(
    bool Success,
    string Message,
    Guid GuardId,
    Guid ManagerId,
    DateTime RequestedAt
);

internal class RequestGuardToManagerHandler(
    IDbConnectionFactory connectionFactory,
    ILogger<RequestGuardToManagerHandler> logger)
    : ICommandHandler<RequestGuardToManagerCommand, RequestGuardToManagerResult>
{
    public async Task<RequestGuardToManagerResult> Handle(
        RequestGuardToManagerCommand command,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation(
                "Processing join request from Guard {GuardId} to Manager {ManagerId}",
                command.GuardId,
                command.ManagerId);

            using var connection = await connectionFactory.CreateConnectionAsync();
            
            var guard = await connection.QueryFirstOrDefaultAsync<Guards>(
                @"SELECT * FROM guards
                  WHERE Id = @GuardId
                  AND IsDeleted = 0",
                new { command.GuardId });

            if (guard == null)
            {
                logger.LogWarning("Guard {GuardId} not found", command.GuardId);
                return new RequestGuardToManagerResult(
                    Success: false,
                    Message: $"Guard with ID {command.GuardId} not found",
                    GuardId: command.GuardId,
                    ManagerId: command.ManagerId,
                    RequestedAt: DateTime.UtcNow
                );
            }

            var manager = await connection.QueryFirstOrDefaultAsync<Managers>(
                @"SELECT * FROM managers
                  WHERE Id = @ManagerId
                  AND IsDeleted = 0
                  AND IsActive = 1",
                new { command.ManagerId });

            if (manager == null)
            {
                logger.LogWarning("Manager {ManagerId} not found or inactive", command.ManagerId);
                return new RequestGuardToManagerResult(
                    Success: false,
                    Message: $"Manager with ID {command.ManagerId} not found or inactive",
                    GuardId: command.GuardId,
                    ManagerId: command.ManagerId,
                    RequestedAt: DateTime.UtcNow
                );
            }

            // CRITICAL FIX: Enhanced validation for pending requests
            if (guard.ContractType == "join_in_request" && guard.DirectManagerId != null)
            {
                // Allow guard to update request to SAME manager
                if (guard.DirectManagerId == command.ManagerId)
                {
                    logger.LogInformation(
                        "Guard {GuardId} is updating request to same Manager {ManagerId}",
                        command.GuardId,
                        command.ManagerId);
                    // Continue to update the request
                }
                else
                {
                    // Get existing manager info for better error message
                    var existingManager = await connection.QueryFirstOrDefaultAsync<Managers>(
                        @"SELECT Id, FullName, EmployeeCode FROM managers
                          WHERE Id = @ManagerId AND IsDeleted = 0",
                        new { ManagerId = guard.DirectManagerId });

                    string existingManagerInfo = existingManager != null
                        ? $"{existingManager.FullName} ({existingManager.EmployeeCode})"
                        : guard.DirectManagerId.ToString();

                    logger.LogWarning(
                        "Guard {GuardId} already has pending request to Manager {ExistingManagerId}, cannot request to {NewManagerId}",
                        command.GuardId,
                        guard.DirectManagerId,
                        command.ManagerId);

                    return new RequestGuardToManagerResult(
                        Success: false,
                        Message: $"Bạn đã có yêu cầu join đang chờ duyệt với Manager {existingManagerInfo}. " +
                                $"Vui lòng đợi phê duyệt hoặc hủy yêu cầu hiện tại trước khi gửi yêu cầu mới đến manager khác.",
                        GuardId: command.GuardId,
                        ManagerId: command.ManagerId,
                        RequestedAt: DateTime.UtcNow
                    );
                }
            }

            using var transaction = connection.BeginTransaction();

            try
            {
                var updateGuardSql = @"
                    UPDATE guards
                    SET DirectManagerId = @ManagerId,
                        ContractType = 'join_in_request',
                        UpdatedAt = @UpdatedAt
                    WHERE Id = @GuardId
                    AND IsDeleted = 0";

                var rowsAffected = await connection.ExecuteAsync(
                    updateGuardSql,
                    new
                    {
                        command.ManagerId,
                        UpdatedAt = DateTime.UtcNow,
                        command.GuardId
                    },
                    transaction);

                if (rowsAffected == 0)
                {
                    transaction.Rollback();
                    logger.LogError(
                        "Failed to update guard {GuardId} with join request",
                        command.GuardId);

                    return new RequestGuardToManagerResult(
                        Success: false,
                        Message: "Failed to create join request. Please try again.",
                        GuardId: command.GuardId,
                        ManagerId: command.ManagerId,
                        RequestedAt: DateTime.UtcNow
                    );
                }

                transaction.Commit();
            }
            catch (Exception)
            {
                transaction.Rollback();
                throw;
            }

            logger.LogInformation(
                "Guard {GuardId} ({GuardName}) successfully sent join request to Manager {ManagerId} ({ManagerName})",
                command.GuardId,
                guard.FullName,
                command.ManagerId,
                manager.FullName);
            

            return new RequestGuardToManagerResult(
                Success: true,
                Message: $"Join request successfully sent to {manager.FullName}. Please wait for approval.",
                GuardId: command.GuardId,
                ManagerId: command.ManagerId,
                RequestedAt: DateTime.UtcNow
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Error processing join request from Guard {GuardId} to Manager {ManagerId}",
                command.GuardId,
                command.ManagerId);
            throw;
        }
    }
}
