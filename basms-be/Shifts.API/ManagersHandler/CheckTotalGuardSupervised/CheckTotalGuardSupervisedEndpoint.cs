namespace Shifts.API.ManagersHandler.CheckTotalGuardSupervised;

public class CheckTotalGuardSupervisedEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/shifts/managers/{managerId:guid}/check-guard-count",
                async (Guid managerId, ISender sender) =>
                {
                    var query = new CheckTotalGuardSupervisedQuery(managerId);
                    var result = await sender.Send(query);

                    if (!result.Success)
                    {
                        return Results.NotFound(new
                        {
                            success = false,
                            error = result.ErrorMessage
                        });
                    }

                    return Results.Ok(new
                    {
                        success = true,
                        data = new
                        {
                            managerId = result.ManagerId,
                            managerName = result.ManagerName,
                            actualGuardsCount = result.ActualGuardsCount,
                            totalGuardsSupervised = result.TotalGuardsSupervised,
                            availableSlots = result.AvailableSlots,
                            isOverLimit = result.IsOverLimit,
                            message = result.Message
                        }
                    });
                })
            .RequireAuthorization()
            .WithTags("Managers")
            .WithName("CheckTotalGuardSupervised")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .WithSummary("Kiểm tra số guards thực tế so với TotalGuardsSupervised của manager");
    }
}
