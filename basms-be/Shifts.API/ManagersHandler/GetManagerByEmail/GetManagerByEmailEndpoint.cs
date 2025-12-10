// Endpoint API để lấy thông tin chi tiết manager theo Email
// Trả về cache manager từ Shifts database
namespace Shifts.API.ManagersHandler.GetManagerByEmail;

/// <summary>
/// Request body để lấy manager theo email
/// </summary>
public record GetManagerByEmailRequest(string Email);

public class GetManagerByEmailEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/shifts/managers/by-email", async (GetManagerByEmailRequest request, ISender sender) =>
        {
            // Bước 1: Validate request
            if (string.IsNullOrWhiteSpace(request.Email))
            {
                return Results.BadRequest(new { success = false, error = "Email is required" });
            }

            // Bước 2: Tạo query với Email manager cần lấy
            var query = new GetManagerByEmailQuery(request.Email);

            // Bước 3: Gửi query đến Handler
            var result = await sender.Send(query);

            // Bước 4: Trả về 200 OK với thông tin manager đầy đủ
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
        .WithSummary("Get manager by Email");
    }
}
