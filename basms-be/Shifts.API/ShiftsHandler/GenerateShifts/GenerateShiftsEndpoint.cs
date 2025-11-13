namespace Shifts.API.ShiftsHandler.GenerateShifts;

/// <summary>
/// Endpoint để manager tự tạo ca làm từ contract
/// Manual shift generation from contract schedules
/// </summary>
public class GenerateShiftsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/shifts/generate",
            async (GenerateShiftsRequest request, ISender sender, HttpContext httpContext) =>
            {
                // Lấy userId từ JWT token
                var userId = httpContext.User.FindFirst("userId")?.Value;
                var createdBy = string.IsNullOrEmpty(userId) ? (Guid?)null : Guid.Parse(userId);

                var command = new GenerateShiftsCommand(
                    ContractId: request.ContractId,
                    GenerateFromDate: request.GenerateFromDate,
                    GenerateDays: request.GenerateDays ?? 30,
                    CreatedBy: createdBy
                );

                var result = await sender.Send(command);

                return Results.Ok(new
                {
                    success = result.ShiftsCreatedCount > 0,
                    message = $"Tạo thành công {result.ShiftsCreatedCount} ca làm, bỏ qua {result.ShiftsSkippedCount} ca",
                    data = result
                });
            })
            .RequireAuthorization()
            .WithName("GenerateShifts")
            .WithTags("Shifts - Auto Generation")
            .WithDescription(@"
                Tự động tạo ca làm từ mẫu lịch trong hợp đồng.

                WORKFLOW:
                1. Lấy tất cả shift schedules từ contract
                2. Với mỗi ngày trong khoảng thời gian:
                   - Kiểm tra ngày lễ, ngày đóng cửa
                   - Kiểm tra exception (skip/modify)
                   - Tạo shift nếu đủ điều kiện
                3. Return kết quả: số ca tạo được, số ca bỏ qua, lý do

                USE CASES:
                - Manager muốn tạo ca cho 1 tháng tới
                - Sau khi sửa schedule, cần regenerate shifts
                - Contract mới được activate (auto-trigger)

                NOTES:
                - Không tạo duplicate shifts (kiểm tra existing)
                - Publish ShiftsGeneratedEvent khi xong
                - Log chi tiết vào shift_generation_log
            ")
            .Produces<GenerateShiftsResult>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest);
    }
}

/// <summary>
/// Request model cho generate shifts endpoint
/// </summary>
public record GenerateShiftsRequest
{
    /// <summary>
    /// ID hợp đồng cần tạo ca
    /// </summary>
    public Guid ContractId { get; init; }

    /// <summary>
    /// Tạo ca từ ngày nào (null = hôm nay)
    /// </summary>
    public DateTime? GenerateFromDate { get; init; }

    /// <summary>
    /// Tạo trước bao nhiêu ngày (default: 30)
    /// </summary>
    public int? GenerateDays { get; init; }
}
