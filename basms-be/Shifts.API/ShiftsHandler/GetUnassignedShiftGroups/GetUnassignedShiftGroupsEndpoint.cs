namespace Shifts.API.ShiftsHandler.GetUnassignedShiftGroups;

public class GetUnassignedShiftGroupsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/shifts/unassigned-groups", async (
            [FromQuery] Guid managerId,
            [FromQuery] Guid? contractId,
            ISender sender,
            ILogger<GetUnassignedShiftGroupsEndpoint> logger,
            CancellationToken cancellationToken) =>
        {
            logger.LogInformation(
                "GET /api/shifts/unassigned-groups - Getting unassigned shift groups for Manager {ManagerId}, ContractId={ContractId}",
                managerId,
                contractId?.ToString() ?? "ALL");

            var query = new GetUnassignedShiftGroupsQuery(managerId, contractId);

            var result = await sender.Send(query, cancellationToken);

            if (!result.Success)
            {
                logger.LogWarning(
                    "Failed to get unassigned shift groups: {Error}",
                    result.ErrorMessage);

                return Results.BadRequest(new
                {
                    success = false,
                    error = result.ErrorMessage
                });
            }

            var totalUnassigned = result.ShiftGroups.Sum(g => g.UnassignedShiftCount);

            logger.LogInformation(
                "Retrieved {GroupCount} unassigned shift groups ({TotalShifts} total shifts) for Manager {ManagerId}, ContractId={ContractId}",
                result.ShiftGroups.Count,
                totalUnassigned,
                managerId,
                contractId?.ToString() ?? "ALL");

            return Results.Ok(new
            {
                success = true,
                data = result.ShiftGroups,
                totalGroups = result.TotalGroups,
                totalUnassignedShifts = totalUnassigned,
                message = $"Found {result.TotalGroups} shift groups with {totalUnassigned} unassigned shifts",
                note = "Each group represents shifts with same TemplateId and ContractId. Only one representative shift is shown per group."
            });
        })
        .WithName("GetUnassignedShiftGroups")
        .WithTags("Shifts")
        .Produces(200)
        .Produces(400)
        .WithSummary("Get unassigned shift groups by manager");
    }
}
