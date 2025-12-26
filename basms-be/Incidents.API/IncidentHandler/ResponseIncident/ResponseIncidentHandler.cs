using Incidents.API.Extensions;

namespace Incidents.API.IncidentHandler.ResponseIncident;

public record ResponseIncidentCommand(
    Guid IncidentId,
    Guid ResponderId,
    string ResponseContent
) : ICommand<ResponseIncidentResult>;

public record ResponseIncidentResult
{
    public bool Success { get; init; }
    public Guid? IncidentId { get; init; }
    public string? IncidentCode { get; init; }
    public string? Status { get; init; }
    public DateTime? RespondedAt { get; init; }
    public string? ErrorMessage { get; init; }
    public string Message { get; init; } = string.Empty;
}

internal class ResponseIncidentHandler(
    IDbConnectionFactory dbFactory,
    ILogger<ResponseIncidentHandler> logger)
    : ICommandHandler<ResponseIncidentCommand, ResponseIncidentResult>
{
    public async Task<ResponseIncidentResult> Handle(
        ResponseIncidentCommand command,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Processing response for Incident={IncidentId} by Responder={ResponderId}",
            command.IncidentId,
            command.ResponderId);

        using var connection = await dbFactory.CreateConnectionAsync();

        try
        {
            var validator = new ResponseIncidentValidator();
            var validationResult = await validator.ValidateAsync(command, cancellationToken);

            if (!validationResult.IsValid)
            {
                var errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
                logger.LogWarning("Validation failed: {Errors}", errors);

                return new ResponseIncidentResult
                {
                    Success = false,
                    ErrorMessage = $"Validation failed: {errors}"
                };
            }

            // Check if incident exists
            var incident = await connection.QueryFirstOrDefaultAsync<Models.Incidents>(
                @"SELECT * FROM incidents
                  WHERE Id = @IncidentId AND IsDeleted = 0",
                new { IncidentId = command.IncidentId });

            if (incident == null)
            {
                logger.LogWarning(
                    "Incident not found: {IncidentId}",
                    command.IncidentId);

                return new ResponseIncidentResult
                {
                    Success = false,
                    ErrorMessage = $"Incident with ID {command.IncidentId} not found"
                };
            }

            // Check if already responded
            if (incident.Status == "RESPONDED" || incident.Status == "RESOLVED" || incident.Status == "CLOSED")
            {
                logger.LogWarning(
                    "Incident {IncidentCode} is already {Status}",
                    incident.IncidentCode,
                    incident.Status);

                return new ResponseIncidentResult
                {
                    Success = false,
                    IncidentId = incident.Id,
                    IncidentCode = incident.IncidentCode,
                    Status = incident.Status,
                    ErrorMessage = $"Incident {incident.IncidentCode} is already {incident.Status}"
                };
            }

            var now = DateTimeExtensions.GetVietnamTime();

            // Update incident with response
            var updateSql = @"
                UPDATE incidents
                SET
                    ResponderId = @ResponderId,
                    ResponseContent = @ResponseContent,
                    RespondedAt = @RespondedAt,
                    Status = @Status,
                    UpdatedAt = @UpdatedAt,
                    UpdatedBy = @UpdatedBy
                WHERE Id = @IncidentId";

            var rowsAffected = await connection.ExecuteAsync(updateSql, new
            {
                IncidentId = command.IncidentId,
                ResponderId = command.ResponderId,
                ResponseContent = command.ResponseContent,
                RespondedAt = now,
                Status = "RESPONDED",
                UpdatedAt = now,
                UpdatedBy = command.ResponderId
            });

            if (rowsAffected == 0)
            {
                logger.LogError(
                    "Failed to update incident {IncidentId}",
                    command.IncidentId);

                return new ResponseIncidentResult
                {
                    Success = false,
                    ErrorMessage = "Failed to update incident"
                };
            }

            logger.LogInformation(
                "Incident {IncidentCode} responded successfully by user {ResponderId}",
                incident.IncidentCode,
                command.ResponderId);

            return new ResponseIncidentResult
            {
                Success = true,
                IncidentId = incident.Id,
                IncidentCode = incident.IncidentCode,
                Status = "RESPONDED",
                RespondedAt = now,
                Message = $"Incident {incident.IncidentCode} responded successfully"
            };
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Error responding to incident {IncidentId}",
                command.IncidentId);

            return new ResponseIncidentResult
            {
                Success = false,
                ErrorMessage = $"Error responding to incident: {ex.Message}"
            };
        }
    }
}
