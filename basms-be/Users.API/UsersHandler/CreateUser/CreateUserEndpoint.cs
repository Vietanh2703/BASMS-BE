namespace Users.API.UsersHandler.CreateUser;

// DTO (Data Transfer Object) nhận dữ liệu từ client
// Chứa tất cả thông tin cần thiết để tạo tài khoản người dùng mới
public record CreateUserRequest(
    string IdentityNumber,
    DateTime IdentityIssueDate,
    string IdentityIssuePlace,
    string Email,              // Email đăng nhập (bắt buộc)
    string Password,           // Mật khẩu (bắt buộc)
    string FullName,           // Họ và tên đầy đủ (bắt buộc)
    string? Phone,             // Số điện thoại (tùy chọn)
    string Gender,
    string? Address,           // Địa chỉ (tùy chọn)
    int? BirthDay,            // Ngày sinh (1-31)
    int? BirthMonth,          // Tháng sinh (1-12)
    int? BirthYear,           // Năm sinh
    Guid? RoleId,             // ID vai trò - nếu null sẽ dùng role mặc định "guard"
    string? AvatarUrl,        // URL ảnh đại diện (tùy chọn)
    string AuthProvider = "email"  // Phương thức đăng ký (mặc định: email)
);

// DTO trả về cho client sau khi tạo user thành công
// Chỉ chứa thông tin cơ bản và ID để bảo mật
public record CreateUserResponse(
    Guid Id,                  // ID user trong database
    string FirebaseUid,       // UID từ Firebase Authentication
    string Email              // Email đã đăng ký
);

public class CreateUserEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/users", async (CreateUserRequest request, ISender sender, ILogger<CreateUserEndpoint> logger, HttpContext httpContext) =>
        {
            // Log để debug routing issue
            logger.LogInformation("CreateUserEndpoint HIT - Method: {Method}, Path: {Path}",
                httpContext.Request.Method, httpContext.Request.Path);

            // Bước 1: Chuyển đổi request từ client thành command để xử lý
            // Sử dụng Mapster để tự động map các property giống nhau
            var command = request.Adapt<CreateUserCommand>();

            // Bước 2: Gửi command đến Handler thông qua MediatR
            // Handler sẽ thực hiện logic tạo user (validate, lưu DB, tạo Firebase account)
            var result = await sender.Send(command);

            // Bước 3: Chuyển đổi kết quả từ Handler thành response cho client
            var response = result.Adapt<CreateUserResponse>();

            // Bước 4: Trả về HTTP 201 Created với location header và response body
            // Location header chứa URL để lấy thông tin user vừa tạo
            return Results.Created($"/users/{response.Id}", response);
        })
        .AllowAnonymous()  // Explicitly allow anonymous access for user registration
        .WithTags("Users")  // Nhóm endpoint trong Swagger UI
        .WithName("CreateUser")  // Tên endpoint để reference
        // Định nghĩa các response codes có thể trả về
        .Produces<CreateUserResponse>(StatusCodes.Status201Created)  // Thành công
        .ProducesProblem(StatusCodes.Status400BadRequest)  // Dữ liệu không hợp lệ
        .ProducesProblem(StatusCodes.Status401Unauthorized)  // Chưa đăng nhập
        .ProducesProblem(StatusCodes.Status403Forbidden)  // Không có quyền (wrong roleId)
        .ProducesProblem(StatusCodes.Status409Conflict)  // Email đã tồn tại
        .ProducesProblem(StatusCodes.Status500InternalServerError)  // Lỗi server
        .WithSummary("Creates a new user")  // Mô tả ngắn
        .WithDescription("Creates a new user account with Firebase authentication and stores in MySQL database");  // Mô tả chi tiết
    }
}