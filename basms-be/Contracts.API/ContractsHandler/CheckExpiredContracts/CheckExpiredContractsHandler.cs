namespace Contracts.API.ContractsHandler.CheckExpiredContracts;

public record CheckExpiredContractsCommand() : ICommand<CheckExpiredContractsResult>;

public record CheckExpiredContractsResult
{
    public bool Success { get; init; }
    public int NearExpiredCount { get; init; }
    public int ExpiredCount { get; init; }
    public int ManagersDeactivated { get; init; }
    public int GuardsDeactivated { get; init; }
    public int CustomersDeactivated { get; init; }
    public string? ErrorMessage { get; init; }
    public List<ContractExpirationDetail> NearExpiredContracts { get; init; } = new();
    public List<ContractExpirationDetail> ExpiredContracts { get; init; } = new();
}

public record ContractExpirationDetail
{
    public Guid ContractId { get; init; }
    public Guid DocumentId { get; init; }
    public string ContractType { get; init; } = string.Empty;
    public string? DocumentEmail { get; init; }
    public DateTime EndDate { get; init; }
    public int DaysRemaining { get; init; }
    public string Status { get; init; } = string.Empty;
}

internal class CheckExpiredContractsHandler(
    IDbConnectionFactory connectionFactory,
    IPublishEndpoint publishEndpoint,
    EmailHandler emailHandler,
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
            var nearExpiredContracts = new List<ContractExpirationDetail>();
            var expiredContracts = new List<ContractExpirationDetail>();

            var documents = await connection.QueryAsync<ContractDocument>(@"
                SELECT * FROM contract_documents
                WHERE IsDeleted = 0
                AND EndDate IS NOT NULL
                AND EndDate <= @SevenDaysFromNow
                AND DocumentType NOT IN ('expired_document', 'near_expired')
                ORDER BY EndDate ASC",
                new { SevenDaysFromNow = sevenDaysFromNow.ToUniversalTime() });

            logger.LogInformation("Found {Count} active documents to check", documents.Count());

            foreach (var document in documents)
            {
                if (document.EndDate == null) continue;

                var endDateVietnam = TimeZoneInfo.ConvertTimeFromUtc(document.EndDate.Value, vietnamTimeZone);
                
                if (endDateVietnam <= nowVietnam)
                {
                    var daysRemaining = (int)(endDateVietnam - nowVietnam).TotalDays;

                    logger.LogInformation(
                        "Document {DocumentId} expired: EndDate={EndDate}, DaysRemaining={Days}",
                        document.Id,
                        endDateVietnam,
                        daysRemaining);
                    
                    var contract = await connection.QueryFirstOrDefaultAsync<Contract>(@"
                        SELECT * FROM contracts
                        WHERE DocumentId = @DocumentId
                        AND IsDeleted = 0",
                        new { DocumentId = document.Id });

                    if (contract != null)
                    {
                        bool wasAlreadyExpired = contract.Status == "expired";
                        using var transaction = connection.BeginTransaction();
                        try
                        {
                            await connection.ExecuteAsync(@"
                                UPDATE contract_documents
                                SET DocumentType = 'expired_document',
                                    UpdatedAt = @UpdatedAt
                                WHERE Id = @DocumentId",
                                new {
                                    DocumentId = document.Id,
                                    UpdatedAt = DateTime.UtcNow
                                },
                                transaction);
                            
                            if (!wasAlreadyExpired)
                            {
                                await connection.ExecuteAsync(@"
                                    UPDATE contracts
                                    SET Status = 'expired',
                                        UpdatedAt = @UpdatedAt
                                    WHERE Id = @ContractId",
                                    new
                                    {
                                        ContractId = contract.Id,
                                        UpdatedAt = DateTime.UtcNow
                                    },
                                    transaction);

                                logger.LogInformation(
                                    "Contract {ContractId} marked as expired, Type={Type}",
                                    contract.Id,
                                    contract.ContractType);
                            }
                            
                            transaction.Commit();

                            logger.LogInformation(
                                "Transaction committed successfully for document {DocumentId}",
                                document.Id);
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback();
                            logger.LogError(ex,
                                "Failed to update expired status for document {DocumentId}, transaction rolled back",
                                document.Id);
                            throw;
                        }
                        
                        expiredContracts.Add(new ContractExpirationDetail
                        {
                            ContractId = contract.Id,
                            DocumentId = document.Id,
                            ContractType = contract.ContractType,
                            DocumentEmail = document.DocumentEmail,
                            EndDate = endDateVietnam,
                            DaysRemaining = daysRemaining,
                            Status = "expired"
                        });
                        

                        if (!wasAlreadyExpired)
                        {
                            if (contract.ContractType == "manager_working_contract" ||
                                contract.ContractType == "extended_working_contract")
                            {
                                if (!string.IsNullOrEmpty(document.DocumentEmail))
                                {
                                    await publishEndpoint.Publish(new DeactivateUserEvent
                                    {
                                        Email = document.DocumentEmail,
                                        UserType = "manager",
                                        Reason = "Contract expired",
                                        DeactivatedAt = DateTime.UtcNow
                                    }, cancellationToken);
                                    
                                    await publishEndpoint.Publish(new DeactivateManagerEvent
                                    {
                                        ManagerId = Guid.Empty, 
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
                                if (!string.IsNullOrEmpty(document.DocumentEmail))
                                {
                                    await publishEndpoint.Publish(new DeactivateUserEvent
                                    {
                                        Email = document.DocumentEmail,
                                        UserType = "guard",
                                        Reason = "Contract expired",
                                        DeactivatedAt = DateTime.UtcNow
                                    }, cancellationToken);
                                    
                                    await publishEndpoint.Publish(new DeactivateGuardEvent
                                    {
                                        GuardId = Guid.Empty,
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
                        else
                        {
                            logger.LogInformation(
                                "Contract {ContractId} already expired, skipping update and user deactivation",
                                contract.Id);
                        }
                    }

                    expiredCount++;
                }
                else if (endDateVietnam <= sevenDaysFromNow && endDateVietnam > nowVietnam)
                {
                    var daysRemaining = (int)(endDateVietnam - nowVietnam).TotalDays;

                    logger.LogInformation(
                        "Document {DocumentId} near expired: EndDate={EndDate}, DaysRemaining={Days}",
                        document.Id,
                        endDateVietnam,
                        daysRemaining);

                    var contract = await connection.QueryFirstOrDefaultAsync<Contract>(@"
                        SELECT * FROM contracts
                        WHERE DocumentId = @DocumentId
                        AND IsDeleted = 0",
                        new { DocumentId = document.Id });
                    
                    using var transaction = connection.BeginTransaction();
                    try
                    {
                        await connection.ExecuteAsync(@"
                            UPDATE contract_documents
                            SET DocumentType = 'near_expired',
                                UpdatedAt = @UpdatedAt
                            WHERE Id = @DocumentId",
                            new {
                                DocumentId = document.Id,
                                UpdatedAt = DateTime.UtcNow
                            },
                            transaction);
                        
                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        logger.LogError(ex,
                            "Failed to update near expired status for document {DocumentId}, transaction rolled back",
                            document.Id);
                        throw;
                    }

                    if (contract != null)
                    {
                        nearExpiredContracts.Add(new ContractExpirationDetail
                        {
                            ContractId = contract.Id,
                            DocumentId = document.Id,
                            ContractType = contract.ContractType,
                            DocumentEmail = document.DocumentEmail,
                            EndDate = endDateVietnam,
                            DaysRemaining = daysRemaining,
                            Status = "near_expired"
                        });

                        if (!string.IsNullOrEmpty(document.DocumentEmail))
                        {
                            try
                            {
                                var recipientName = document.DocumentEmail.Split('@')[0];
                                
                                var contractNumber = contract.ContractNumber;

                                await emailHandler.SendContractNearExpiryNotificationAsync(
                                    recipientName,
                                    document.DocumentEmail,
                                    contractNumber,
                                    contract.ContractType,
                                    endDateVietnam,
                                    daysRemaining);

                                logger.LogInformation(
                                    "Sent near expiry email notification to {Email} for contract {ContractId}",
                                    document.DocumentEmail,
                                    contract.Id);
                            }
                            catch (Exception ex)
                            {
                                logger.LogWarning(ex,
                                    "Failed to send near expiry email to {Email} for contract {ContractId}",
                                    document.DocumentEmail,
                                    contract.Id);
                            }
                        }
                    }

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
                CustomersDeactivated = customersDeactivated,
                NearExpiredContracts = nearExpiredContracts,
                ExpiredContracts = expiredContracts
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
