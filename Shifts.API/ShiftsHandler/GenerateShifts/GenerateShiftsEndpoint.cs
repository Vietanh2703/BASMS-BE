// Endpoint API để tự động tạo ca làm từ Shift Templates
// Generate shifts from multiple shift templates for a manager
namespace Shifts.API.ShiftsHandler.GenerateShifts;

public class GenerateShiftsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/shifts/generate", async (GenerateShiftsRequest request, ISender sender) =>
        {
            var command = new GenerateShiftsCommand(
                ManagerId: request.ManagerId,
                ShiftTemplateIds: request.ShiftTemplateIds,
                GenerateFromDate: request.GenerateFromDate,
                GenerateDays: request.GenerateDays ?? 30
            );

            var result = await sender.Send(command);

            return Results.Ok(result);
        })
        // .RequireAuthorization()
        .WithTags("Shifts - Auto Generation")
        .WithName("GenerateShifts")
        .Produces<GenerateShiftsResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Generate shifts from shift template")
        .WithDescription(@"
            Tự động tạo ca làm từ Shift Template cụ thể.

            WORKFLOW:
            1. Validate ManagerId tồn tại và có quyền CanCreateShifts
            2. Validate ShiftTemplateId tồn tại và đang active
            3. Lấy thông tin shift template từ database
            4. Với mỗi ngày trong khoảng thời gian:
               - Kiểm tra template có apply cho ngày đó không (Mon-Sun)
               - Kiểm tra ngày lễ (holiday) từ Contracts.API
               - Auto-detect ca đêm từ template IsNightShift flag
               - Auto-detect tăng ca nếu ca > 12h
               - Tạo shift nếu đủ điều kiện (không duplicate)
            5. Return kết quả: số ca tạo được, số ca bỏ qua, lý do

            AUTO-DETECTION:
            - Night Shift: Dựa vào template.IsNightShift
            - Overtime: Ca > 12 giờ → ShiftType = OVERTIME
            - Public Holiday: Call Contracts.API CheckPublicHolidayRequest
            - Manager: Query từ bảng managers, validate CanCreateShifts

            INPUT:
            - ManagerId: Guid (REQUIRED) - ID của manager tạo ca
            - ShiftTemplateIds: List<Guid> (REQUIRED) - Danh sách ID của shift templates
              VD: [ca-sang-id, ca-trua-id, ca-toi-id]
            - GenerateFromDate: DateTime? (OPTIONAL) - Default = hôm nay
            - GenerateDays: int? (OPTIONAL) - Default = 30 ngày

            EXAMPLES:
            {
              ""managerId"": ""guid-manager"",
              ""shiftTemplateIds"": [
                ""guid-ca-sang-8h-12h"",
                ""guid-ca-chieu-13h-17h"",
                ""guid-ca-toi-18h-22h""
              ],
              ""generateFromDate"": ""2025-01-20"",
              ""generateDays"": 30
            }

            NOTES:
            - Có thể truyền 1 hoặc nhiều ShiftTemplateIds
            - Mỗi template sẽ được process độc lập
            - Không tạo duplicate shifts (kiểm tra existing shifts)
            - ManagerId được lưu vào shifts.ManagerId và shifts.CreatedBy
            - Publish ShiftsGeneratedEvent khi hoàn tất
        ");
    }
}

/// <summary>
/// Request model cho generate shifts endpoint
/// </summary>
public record GenerateShiftsRequest
{
    /// <summary>
    /// ID của Manager tạo ca
    /// </summary>
    public Guid ManagerId { get; init; }

    /// <summary>
    /// Danh sách ID của ShiftTemplates để generate shifts
    /// VD: [ca-sang-id, ca-trua-id, ca-toi-id]
    /// </summary>
    public List<Guid> ShiftTemplateIds { get; init; } = new();

    /// <summary>
    /// Tạo ca từ ngày nào (null = hôm nay)
    /// </summary>
    public DateTime? GenerateFromDate { get; init; }

    /// <summary>
    /// Tạo trước bao nhiêu ngày (default: 30)
    /// </summary>
    public int? GenerateDays { get; init; }
}
