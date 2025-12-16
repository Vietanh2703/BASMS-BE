// Endpoint API để lấy thông tin chi tiết guard theo Email
// Trả về cache guard từ Shifts database
namespace Shifts.API.GuardsHandler.GetGuardByEmail;

/// <summary>
/// Request body để lấy guard theo email
/// </summary>
public record GetGuardByEmailRequest(string Email);

public class GetGuardByEmailEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/shifts/guards/by-email", async (GetGuardByEmailRequest request, ISender sender) =>
        {
            // Bước 1: Validate request
            if (string.IsNullOrWhiteSpace(request.Email))
            {
                return Results.BadRequest(new { success = false, error = "Email is required" });
            }

            // Bước 2: Tạo query với Email guard cần lấy
            var query = new GetGuardByEmailQuery(request.Email);

            // Bước 3: Gửi query đến Handler
            var result = await sender.Send(query);

            // Bước 4: Trả về 200 OK với thông tin guard đầy đủ
            return Results.Ok(result);
        })
        .RequireAuthorization()
        .WithTags("Guards")
        .WithName("GetGuardByEmail")
        .Produces<GetGuardByEmailResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Get guard by Email")
        .WithDescription(@"
            Lấy thông tin chi tiết của bảo vệ theo Email.

            Endpoint này query từ cache guards trong Shifts database.

            Request Body:
            {
              ""email"": ""guard@example.com""
            }

            Response:
            - 200 OK: Trả về thông tin đầy đủ của guard
            - 400 Bad Request: Email không hợp lệ hoặc thiếu
            - 404 Not Found: Không tìm thấy guard với email này
            - 500 Internal Server Error: Lỗi server

            Use cases:
            - Tìm guard theo email để assign vào shift
            - Kiểm tra thông tin guard trước khi thêm vào team
            - Lookup guard profile từ email login
        ");
    }
}
