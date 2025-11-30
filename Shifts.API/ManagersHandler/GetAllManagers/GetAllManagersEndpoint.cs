// Endpoint API để lấy danh sách tất cả managers
// Trả về cache managers từ Shifts database
namespace Shifts.API.ManagersHandler.GetAllManagers;

public class GetAllManagersEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/managers", async (ISender sender) =>
        {
            var query = new GetAllManagersQuery();
            var result = await sender.Send(query);
            return Results.Ok(result);
        })
        .RequireAuthorization()
        .WithTags("Managers")
        .WithName("GetAllManagers")
        .Produces<GetAllManagersResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Get all managers")
        .WithDescription("Retrieves all active managers from the cache database");
    }
}
