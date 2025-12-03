using Dapper;

namespace Shifts.API.ManagersHandler.ConfirmGuardJoinRequest;

/// <summary>
/// Command để manager xác nhận hoặc từ chối join request
/// </summary>
public record ConfirmGuardJoinRequestCommand(
    Guid ManagerId,
    Guid GuardId,
    bool IsApproved,
    string? ResponseNote
) : ICommand<ConfirmGuardJoinRequestResult>;

/// <summary>
/// Result trả về sau khi xác nhận
/// </summary>
public record ConfirmGuardJoinRequestResult(
    bool Success,
    string Message,
    Guid GuardId,
    Guid ManagerId,
    bool IsApproved,
    string NewContractType,
    DateTime ProcessedAt
);

internal class ConfirmGuardJoinRequestHandler(
    IDbConnectionFactory connectionFactory,
    ILogger<ConfirmGuardJoinRequestHandler> logger)
    : ICommandHandler<ConfirmGuardJoinRequestCommand, ConfirmGuardJoinRequestResult>
{
    public async Task<ConfirmGuardJoinRequestResult> Handle(
        ConfirmGuardJoinRequestCommand command,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation(
                "Manager {ManagerId} processing join request from Guard {GuardId} - Action: {Action}",
                command.ManagerId,
                command.GuardId,
                command.IsApproved ? "APPROVE" : "REJECT");

            using var connection = await connectionFactory.CreateConnectionAsync();
            using var transaction = connection.BeginTransaction();

            try
            {
                // ================================================================
                // BƯỚC 1: VALIDATE MANAGER EXISTS & HAS PERMISSION
                // ================================================================
                var manager = await connection.QueryFirstOrDefaultAsync<Managers>(
                    @"SELECT * FROM managers
                      WHERE Id = @ManagerId
                      AND IsDeleted = 0
                      AND IsActive = 1",
                    new { command.ManagerId },
                    transaction);

                if (manager == null)
                {
                    logger.LogWarning("Manager {ManagerId} not found or inactive", command.ManagerId);
                    return new ConfirmGuardJoinRequestResult(
                        Success: false,
                        Message: "Manager not found or inactive",
                        GuardId: command.GuardId,
                        ManagerId: command.ManagerId,
                        IsApproved: command.IsApproved,
                        NewContractType: string.Empty,
                        ProcessedAt: DateTime.UtcNow
                    );
                }

                // ================================================================
                // BƯỚC 2: VALIDATE GUARD EXISTS & HAS PENDING REQUEST
                // ================================================================
                var guard = await connection.QueryFirstOrDefaultAsync<Guards>(
                    @"SELECT * FROM guards
                      WHERE Id = @GuardId
                      AND IsDeleted = 0",
                    new { command.GuardId },
                    transaction);

                if (guard == null)
                {
                    logger.LogWarning("Guard {GuardId} not found", command.GuardId);
                    return new ConfirmGuardJoinRequestResult(
                        Success: false,
                        Message: "Guard not found",
                        GuardId: command.GuardId,
                        ManagerId: command.ManagerId,
                        IsApproved: command.IsApproved,
                        NewContractType: string.Empty,
                        ProcessedAt: DateTime.UtcNow
                    );
                }

                // ================================================================
                // BƯỚC 3: VALIDATE REQUEST IS FOR THIS MANAGER
                // ================================================================
                if (guard.DirectManagerId != command.ManagerId)
                {
                    logger.LogWarning(
                        "Guard {GuardId} request is not for Manager {ManagerId}. Current manager: {CurrentManagerId}",
                        command.GuardId,
                        command.ManagerId,
                        guard.DirectManagerId);

                    return new ConfirmGuardJoinRequestResult(
                        Success: false,
                        Message: "This join request is not assigned to you",
                        GuardId: command.GuardId,
                        ManagerId: command.ManagerId,
                        IsApproved: command.IsApproved,
                        NewContractType: string.Empty,
                        ProcessedAt: DateTime.UtcNow
                    );
                }

                // ================================================================
                // BƯỚC 4: VALIDATE REQUEST IS PENDING
                // ================================================================
                if (guard.ContractType != "join_in_request")
                {
                    logger.LogWarning(
                        "Guard {GuardId} does not have pending request. Current ContractType: {ContractType}",
                        command.GuardId,
                        guard.ContractType);

                    return new ConfirmGuardJoinRequestResult(
                        Success: false,
                        Message: $"No pending join request found. Current status: {guard.ContractType ?? "none"}",
                        GuardId: command.GuardId,
                        ManagerId: command.ManagerId,
                        IsApproved: command.IsApproved,
                        NewContractType: guard.ContractType ?? string.Empty,
                        ProcessedAt: DateTime.UtcNow
                    );
                }

                // ================================================================
                // BƯỚC 5: UPDATE GUARD BASED ON APPROVAL STATUS
                // ================================================================
                string newContractType;
                string updateSql;

                if (command.IsApproved)
                {
                    // APPROVE: Change to "accepted_request"
                    newContractType = "accepted_request";
                    updateSql = @"
                        UPDATE guards
                        SET ContractType = 'accepted_request',
                            UpdatedAt = @UpdatedAt
                        WHERE Id = @GuardId
                        AND IsDeleted = 0";

                    logger.LogInformation(
                        "Approving join request - Guard {GuardId} will be added to Manager {ManagerId}'s team",
                        command.GuardId,
                        command.ManagerId);
                }
                else
                {
                    // REJECT: Reset DirectManagerId and ContractType
                    newContractType = "rejected";
                    updateSql = @"
                        UPDATE guards
                        SET DirectManagerId = NULL,
                            ContractType = NULL,
                            UpdatedAt = @UpdatedAt
                        WHERE Id = @GuardId
                        AND IsDeleted = 0";

                    logger.LogInformation(
                        "Rejecting join request - Guard {GuardId} request to Manager {ManagerId} denied",
                        command.GuardId,
                        command.ManagerId);
                }

                var rowsAffected = await connection.ExecuteAsync(
                    updateSql,
                    new
                    {
                        UpdatedAt = DateTime.UtcNow,
                        command.GuardId
                    },
                    transaction);

                if (rowsAffected == 0)
                {
                    transaction.Rollback();
                    logger.LogError(
                        "Failed to update guard {GuardId} - no rows affected",
                        command.GuardId);

                    return new ConfirmGuardJoinRequestResult(
                        Success: false,
                        Message: "Failed to process join request. Please try again.",
                        GuardId: command.GuardId,
                        ManagerId: command.ManagerId,
                        IsApproved: command.IsApproved,
                        NewContractType: string.Empty,
                        ProcessedAt: DateTime.UtcNow
                    );
                }

                // ================================================================
                // BƯỚC 6: UPDATE MANAGER STATISTICS (if approved)
                // ================================================================
                if (command.IsApproved)
                {
                    await connection.ExecuteAsync(
                        @"UPDATE managers
                          SET TotalGuardsSupervised = (
                              SELECT COUNT(*) FROM guards
                              WHERE DirectManagerId = @ManagerId
                              AND ContractType = 'accepted_request'
                              AND IsDeleted = 0
                          ),
                          UpdatedAt = @UpdatedAt
                          WHERE Id = @ManagerId",
                        new
                        {
                            command.ManagerId,
                            UpdatedAt = DateTime.UtcNow
                        },
                        transaction);
                }

                transaction.Commit();

                logger.LogInformation(
                    "✅ Manager {ManagerId} ({ManagerName}) {Action} join request from Guard {GuardId} ({GuardName})",
                    command.ManagerId,
                    manager.FullName,
                    command.IsApproved ? "APPROVED" : "REJECTED",
                    command.GuardId,
                    guard.FullName);

                // TODO: Send notification to guard about approval/rejection
                // await SendNotificationToGuard(guard, manager, command.IsApproved, command.ResponseNote);

                var message = command.IsApproved
                    ? $"Successfully approved {guard.FullName}'s join request. They are now part of your team."
                    : $"Join request from {guard.FullName} has been rejected.";

                return new ConfirmGuardJoinRequestResult(
                    Success: true,
                    Message: message,
                    GuardId: command.GuardId,
                    ManagerId: command.ManagerId,
                    IsApproved: command.IsApproved,
                    NewContractType: newContractType,
                    ProcessedAt: DateTime.UtcNow
                );
            }
            catch (Exception)
            {
                transaction.Rollback();
                throw;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Error processing join request - Manager {ManagerId}, Guard {GuardId}",
                command.ManagerId,
                command.GuardId);
            throw;
        }
    }
}
