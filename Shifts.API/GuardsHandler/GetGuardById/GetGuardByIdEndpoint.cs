// Endpoint API để lấy thông tin chi tiết guard theo ID
// Trả về cache guard từ Shifts database
namespace Shifts.API.GuardsHandler.GetGuardById;

public class GetGuardByIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // Route: GET /api/guards/{id}
        app.MapGet("/api/guards/{id:guid}", async (Guid id, ISender sender) =>
        {
            // Bước 1: Tạo query với ID guard cần lấy
            var query = new GetGuardByIdQuery(id);

            // Bước 2: Gửi query đến Handler
            var result = await sender.Send(query);

            // Bước 3: Trả về 200 OK với thông tin guard đầy đủ
            return Results.Ok(result);
        })
        .RequireAuthorization()
        .WithTags("Guards")
        .WithName("GetGuardById")
        .Produces<GetGuardByIdResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Get guard by ID")
        .WithDescription("Retrieves detailed information of a specific guard by their ID from cache");
    }
}