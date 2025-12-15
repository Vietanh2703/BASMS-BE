namespace Shifts.API.ShiftsHandler.GetUnassignedShiftGroups;

/// <summary>
/// Endpoint để lấy danh sách ca trực chưa được phân công, nhóm theo TemplateId và ContractId
/// </summary>
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
                "✓ Retrieved {GroupCount} unassigned shift groups ({TotalShifts} total shifts) for Manager {ManagerId}, ContractId={ContractId}",
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
        // .RequireAuthorization()
        .WithName("GetUnassignedShiftGroups")
        .WithTags("Shifts")
        .Produces(200)
        .Produces(400)
        .WithSummary("Get unassigned shift groups by manager")
        .WithDescription(@"
            Returns a list of shift groups that have not been assigned any guards yet (AssignedGuardsCount = 0).

            Important Notes:
            - Shifts are grouped by (ShiftTemplateId, ContractId) combination
            - Only one representative shift is returned per group (the nearest shift by date)
            - Each group shows the total count of unassigned shifts in that group
            - Only shows shifts from contracts managed by the specified manager

            Use Case:
            - Manager can see which shift templates still need team assignment
            - Shows how many shifts are pending assignment for each template
            - Displays the nearest and farthest shift dates for planning

            Query Parameters:
            - managerId (required): The manager ID to filter shifts
            - contractId (optional): Filter by specific contract ID for more detailed results

            Response includes:
            - RepresentativeShiftId: ID of the representative shift (nearest shift in the group)
            - ShiftTemplateId: Template ID that defines this shift pattern
            - ContractId: Contract this shift belongs to
            - TemplateName: Name of the template (e.g., 'Ca Sáng', 'Ca Chiều')
            - TemplateCode: Code of the template (e.g., 'MORNING-8H')
            - LocationName, LocationAddress: Where the shift takes place
            - ShiftStart, ShiftEnd, WorkDurationHours: Timing information
            - UnassignedShiftCount: How many shifts in this group need assignment
            - RequiredGuards: Number of guards needed per shift
            - NearestShiftDate: The earliest unassigned shift in this group
            - FarthestShiftDate: The latest unassigned shift in this group

            Examples:
            GET /api/shifts/unassigned-groups?managerId={guid}
            GET /api/shifts/unassigned-groups?managerId={guid}&contractId={contractGuid}

            Example Response:
            If there are 10 morning shifts (template 'Ca Sáng') and 15 evening shifts (template 'Ca Chiều')
            not assigned for a contract, you will get 2 groups:
            1. One group for 'Ca Sáng' with UnassignedShiftCount=10
            2. One group for 'Ca Chiều' with UnassignedShiftCount=15
        ");
    }
}
