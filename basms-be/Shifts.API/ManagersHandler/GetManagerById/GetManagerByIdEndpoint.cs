// Endpoint API để lấy thông tin chi tiết manager theo ID
// Trả về cache manager từ Shifts database
namespace Shifts.API.ManagersHandler.GetManagerById;

public class GetManagerByIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/managers/{id:guid}", async (Guid id, ISender sender) =>
        {
            var query = new GetManagerByIdQuery(id);
            var result = await sender.Send(query);
            return Results.Ok(result);
        })
        .AddStandardGetDocumentation<GetManagerByIdResult>(
            tag: "Managers",
            name: "GetManagerById",
            summary: "Get manager by ID",
            description: "Retrieves detailed information of a specific manager by their ID from cache");
    }
}