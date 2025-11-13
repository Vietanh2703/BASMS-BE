// Endpoint API để lấy thông tin chi tiết manager theo ID
// Trả về cache manager từ Shifts database
namespace Shifts.API.ManagersHandler.GetManagerById;

public class GetManagerByIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // Route: GET /api/managers/{id}
        app.MapGet("/api/managers/{id:guid}", async (Guid id, ISender sender) =>
        {
            // Bước 1: Tạo query với ID manager cần lấy
            var query = new GetManagerByIdQuery(id);

            // Bước 2: Gửi query đến Handler
            var result = await sender.Send(query);

            // Bước 3: Trả về 200 OK với thông tin manager đầy đủ
            return Results.Ok(result);
        })
        .RequireAuthorization()
        .WithTags("Managers")
        .WithName("GetManagerById")
        .Produces<GetManagerByIdResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Get manager by ID")
        .WithDescription("Retrieves detailed information of a specific manager by their ID from cache");
    }
}