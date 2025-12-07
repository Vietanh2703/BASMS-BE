namespace Contracts.API.ContractsHandler.CheckExpiredContracts;

/// <summary>
/// Command để check và update expired contracts
/// Chạy định kỳ hoặc manual trigger
/// </summary>
public record CheckExpiredContractsCommand() : ICommand<CheckExpiredContractsResult>;

/// <summary>
/// Result của việc check expired contracts
/// </summary>
public record CheckExpiredContractsResult
{
    public bool Success { get; init; }
    public int NearExpiredCount { get; init; }
    public int ExpiredCount { get; init; }
    public int ManagersDeactivated { get; init; }
    public int GuardsDeactivated { get; init; }
    public int CustomersDeactivated { get; init; }
    public string? ErrorMessage { get; init; }
}

internal class CheckExpiredContractsHandler(
    IDbConnectionFactory connectionFactory,
    IPublishEndpoint publishEndpoint,
    ILogger<CheckExpiredContractsHandler> logger)
    : ICommandHandler<CheckExpiredContractsCommand, CheckExpiredContractsResult>
{
    public async Task<CheckExpiredContractsResult> Handle(
        CheckExpiredContractsCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Starting expired contracts check job...");

            // Get Vietnam timezone (UTC+7)
            var vietnamTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            var nowVietnam = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, vietnamTimeZone);
            var sevenDaysFromNow = nowVietnam.AddDays(7);

            logger.LogInformation("Current Vietnam time: {Time}", nowVietnam);

            using var connection = await connectionFactory.CreateConnectionAsync();

            int nearExpiredCount = 0;
            int expiredCount = 0;
            int managersDeactivated = 0;
            int guardsDeactivated = 0;
            int customersDeactivated = 0;

            // ================================================================
            // GET ALL ACTIVE CONTRACT DOCUMENTS
            // ================================================================

            var documents = await connection.QueryAsync<ContractDocument>(@"
                SELECT * FROM contract_documents
                WHERE IsDeleted = 0
                AND EndDate IS NOT NULL
                AND DocumentType NOT IN ('expired_document', 'near_expired')
                ORDER BY EndDate ASC");

            logger.LogInformation("Found {Count} active documents to check", documents.Count());

            foreach (var document in documents)
            {
                if (document.EndDate == null) continue;

                var endDateVietnam = TimeZoneInfo.ConvertTimeFromUtc(document.EndDate.Value, vietnamTimeZone);

                // ================================================================
                // CHECK EXPIRED (EndDate đã qua)
                // ================================================================
                if (endDateVietnam <= nowVietnam)
                {
                    logger.LogInformation(
                        "Document {DocumentId} expired: EndDate={EndDate}",
                        document.Id,
                        endDateVietnam);

                    // Update document type to expired
                    await connection.ExecuteAsync(@"
                        UPDATE contract_documents
                        SET DocumentType = 'expired_document'
                        WHERE Id = @DocumentId",
                        new { DocumentId = document.Id });

                    // Get related contract
                    var contract = await connection.QueryFirstOrDefaultAsync<Contract>(@"
                        SELECT * FROM contracts
                        WHERE DocumentId = @DocumentId
                        AND IsDeleted = 0",
                        new { DocumentId = document.Id });

                    if (contract != null)
                    {
                        // Update contract to inactive
                        await connection.ExecuteAsync(@"
                            UPDATE contracts
                            SET Status = 'expired',
                                UpdatedAt = @UpdatedAt
                            WHERE Id = @ContractId",
                            new
                            {
                                ContractId = contract.Id,
                                UpdatedAt = DateTime.UtcNow
                            });

                        logger.LogInformation(
                            "Contract {ContractId} marked as expired, Type={Type}",
                            contract.Id,
                            contract.ContractType);

                        // ================================================================
                        // DEACTIVATE RELATED USERS BASED ON CONTRACT TYPE
                        // ================================================================

                        if (contract.ContractType == "manager_working_contract" ||
                            contract.ContractType == "extended_working_contract")
                        {
                            // Manager working contract - deactivate manager
                            if (!string.IsNullOrEmpty(document.DocumentEmail))
                            {
                                // Publish event to deactivate User in Users.API
                                await publishEndpoint.Publish(new DeactivateUserEvent
                                {
                                    Email = document.DocumentEmail,
                                    UserType = "manager",
                                    Reason = "Contract expired",
                                    DeactivatedAt = DateTime.UtcNow
                                }, cancellationToken);

                                // Publish event to deactivate Manager in Shifts.API
                                await publishEndpoint.Publish(new DeactivateManagerEvent
                                {
                                    ManagerId = Guid.Empty, // Will be found by email
                                    Email = document.DocumentEmail,
                                    Reason = "Contract expired",
                                    DeactivatedAt = DateTime.UtcNow
                                }, cancellationToken);

                                managersDeactivated++;

                                logger.LogInformation(
                                    "Published deactivation events for manager: Email={Email}",
                                    document.DocumentEmail);
                            }
                        }
                        else if (contract.ContractType == "working_contract")
                        {
                            // Guard working contract - deactivate guard
                            if (!string.IsNullOrEmpty(document.DocumentEmail))
                            {
                                // Publish event to deactivate User in Users.API
                                await publishEndpoint.Publish(new DeactivateUserEvent
                                {
                                    Email = document.DocumentEmail,
                                    UserType = "guard",
                                    Reason = "Contract expired",
                                    DeactivatedAt = DateTime.UtcNow
                                }, cancellationToken);

                                // Publish event to deactivate Guard in Shifts.API
                                await publishEndpoint.Publish(new DeactivateGuardEvent
                                {
                                    GuardId = Guid.Empty, // Will be found by email
                                    Email = document.DocumentEmail,
                                    Reason = "Contract expired",
                                    DeactivatedAt = DateTime.UtcNow
                                }, cancellationToken);

                                guardsDeactivated++;

                                logger.LogInformation(
                                    "Published deactivation events for guard: Email={Email}",
                                    document.DocumentEmail);
                            }
                        }
                        else if (contract.ContractType.Contains("service"))
                        {
                            // Service contract - deactivate customer
                            if (!string.IsNullOrEmpty(document.DocumentEmail))
                            {
                                await publishEndpoint.Publish(new DeactivateUserEvent
                                {
                                    Email = document.DocumentEmail,
                                    UserType = "customer",
                                    Reason = "Contract expired",
                                    DeactivatedAt = DateTime.UtcNow
                                }, cancellationToken);

                                customersDeactivated++;

                                logger.LogInformation(
                                    "Published deactivation event for customer: Email={Email}",
                                    document.DocumentEmail);
                            }
                        }
                    }

                    expiredCount++;
                }
                // ================================================================
                // CHECK NEAR EXPIRED (còn 7 ngày)
                // ================================================================
                else if (endDateVietnam <= sevenDaysFromNow && endDateVietnam > nowVietnam)
                {
                    logger.LogInformation(
                        "Document {DocumentId} near expired: EndDate={EndDate}, DaysRemaining={Days}",
                        document.Id,
                        endDateVietnam,
                        (endDateVietnam - nowVietnam).Days);

                    // Update document type to near_expired
                    await connection.ExecuteAsync(@"
                        UPDATE contract_documents
                        SET DocumentType = 'near_expired'
                        WHERE Id = @DocumentId",
                        new { DocumentId = document.Id });

                    nearExpiredCount++;
                }
            }

            logger.LogInformation(
                "Expired contracts check completed: NearExpired={Near}, Expired={Expired}, " +
                "ManagersDeactivated={Managers}, GuardsDeactivated={Guards}, CustomersDeactivated={Customers}",
                nearExpiredCount,
                expiredCount,
                managersDeactivated,
                guardsDeactivated,
                customersDeactivated);

            return new CheckExpiredContractsResult
            {
                Success = true,
                NearExpiredCount = nearExpiredCount,
                ExpiredCount = expiredCount,
                ManagersDeactivated = managersDeactivated,
                GuardsDeactivated = guardsDeactivated,
                CustomersDeactivated = customersDeactivated
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error checking expired contracts");
            return new CheckExpiredContractsResult
            {
                Success = false,
                ErrorMessage = $"Error: {ex.Message}"
            };
        }
    }

}
