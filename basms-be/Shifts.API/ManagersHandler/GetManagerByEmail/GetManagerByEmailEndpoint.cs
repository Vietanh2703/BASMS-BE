// Endpoint API để lấy thông tin chi tiết manager theo Email
// Trả về cache manager từ Shifts database
namespace Shifts.API.ManagersHandler.GetManagerByEmail;

public class GetManagerByEmailEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/shifts/managers/by-email", async (string email, ISender sender) =>
        {
            // Bước 1: Tạo query với Email manager cần lấy
            var query = new GetManagerByEmailQuery(email);

            // Bước 2: Gửi query đến Handler
            var result = await sender.Send(query);

            // Bước 3: Trả về 200 OK với thông tin manager đầy đủ
            return Results.Ok(result);
        })
        .RequireAuthorization()
        .WithTags("Managers")
        .WithName("GetManagerByEmail")
        .Produces<GetManagerByEmailResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Get manager by Email")
        .WithDescription("Retrieves detailed information of a specific manager by their email address from cache");
    }
}
