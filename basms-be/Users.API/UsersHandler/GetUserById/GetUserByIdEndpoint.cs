// Endpoint API để lấy thông tin chi tiết user theo ID
// Tất cả role đã login đều có thể truy cập
namespace Users.API.UsersHandler.GetUserById;

public class GetUserByIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // Route: GET /users/{id}
        app.MapGet("/users/{id:guid}", async (Guid id, ISender sender) =>
        {
            // Bước 1: Tạo query với ID user cần lấy
            var query = new GetUserByIdQuery(id);
            
            // Bước 2: Gửi query đến Handler
            // Handler sẽ query database và join với bảng Roles
            var result = await sender.Send(query);
            
            // Bước 3: Trả về 200 OK với thông tin user đầy đủ
            return Results.Ok(result);
        })
        .RequireAuthorization()  // Chỉ cần đăng nhập, không giới hạn roleId
        .WithTags("Users")
        .WithName("GetUserById")
        .Produces<GetUserByIdResult>(StatusCodes.Status200OK)  // Thành công
        .ProducesProblem(StatusCodes.Status401Unauthorized)     // Chưa đăng nhập
        .ProducesProblem(StatusCodes.Status404NotFound)         // User không tồn tại
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Get user by ID")
        .WithDescription("Retrieves detailed information of a specific user by their ID");
    }
}