namespace Shifts.API.ShiftsHandler.AssignTeamToShift;

/// <summary>
/// Request DTO từ client
/// </summary>
public record AssignTeamToShiftRequest(
    DateTime StartDate,
    DateTime EndDate,
    string ShiftTimeSlot,       // MORNING | AFTERNOON | EVENING
    Guid LocationId,
    Guid? ContractId,
    string AssignmentType,      // REGULAR | OVERTIME | MANDATORY
    string? AssignmentNotes
);

public class AssignTeamToShiftEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // Route: POST /api/shifts/teams/{teamId}/assign
        app.MapPost("/api/shifts/teams/{teamId}/assign", async (
            Guid teamId,
            AssignTeamToShiftRequest req,
            ISender sender,
            HttpContext context) =>
        {
            // Lấy userId từ claims (manager thực hiện assign)
            var userIdClaim = context.User.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                // Fallback for testing
                userId = Guid.NewGuid();
            }

            // Validate ShiftTimeSlot
            var validTimeSlots = new[] { "MORNING", "AFTERNOON", "EVENING" };
            if (!validTimeSlots.Contains(req.ShiftTimeSlot.ToUpper()))
            {
                return Results.BadRequest(new
                {
                    error = "Invalid ShiftTimeSlot",
                    message = $"ShiftTimeSlot phải là một trong: {string.Join(", ", validTimeSlots)}",
                    received = req.ShiftTimeSlot
                });
            }

            // Validate date range
            if (req.EndDate < req.StartDate)
            {
                return Results.BadRequest(new
                {
                    error = "Invalid date range",
                    message = "EndDate phải lớn hơn hoặc bằng StartDate"
                });
            }

            // Map request DTO sang command
            var command = new AssignTeamToShiftCommand(
                TeamId: teamId,
                StartDate: req.StartDate.Date,
                EndDate: req.EndDate.Date,
                ShiftTimeSlot: req.ShiftTimeSlot.ToUpper(),
                LocationId: req.LocationId,
                ContractId: req.ContractId,
                AssignmentType: req.AssignmentType.ToUpper(),
                AssignmentNotes: req.AssignmentNotes,
                AssignedBy: userId
            );

            // Gửi command đến handler
            var result = await sender.Send(command);

            if (!result.Success)
            {
                return Results.BadRequest(new
                {
                    success = false,
                    errors = result.Errors,
                    warnings = result.Warnings
                });
            }

            // Trả về 200 OK với kết quả
            return Results.Ok(result);
        })
        .RequireAuthorization()
        .WithTags("Shifts", "Teams")
        .WithName("AssignTeamToShift")
        .Produces<AssignTeamToShiftResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Assign team to shifts (multi-day)")
        .WithDescription(@"
Assign toàn bộ team vào các ca trực trong khoảng thời gian từ StartDate đến EndDate.

**WORKFLOW:**
1. Tìm shifts theo LocationId + ShiftDate + ShiftTimeSlot (tính toán từ ShiftStart)
2. Check consecutive shift conflict cho từng guard:
   - MORNING: Không được có ca EVENING hôm trước (vì ca đêm kết thúc 6h sáng)
   - AFTERNOON: Không được có ca MORNING cùng ngày (ca sáng kết thúc 14h)
   - EVENING: Không được có ca AFTERNOON cùng ngày (ca chiều kết thúc 22h)
3. Tạo ShiftAssignments cho guards hợp lệ (không có conflict)
4. Tự động tạo AttendanceRecords (qua RabbitMQ event)

**ShiftTimeSlot Options:**
- `MORNING`: Ca sáng (6h-14h)
- `AFTERNOON`: Ca chiều (14h-22h)
- `EVENING`: Ca tối/đêm (22h-6h)

**Consecutive Shift Rules:**
- ❌ Không được: MORNING + AFTERNOON (cùng ngày)
- ❌ Không được: AFTERNOON + EVENING (cùng ngày)
- ❌ Không được: EVENING (hôm trước) + MORNING (hôm sau)
- ✅ Cho phép: MORNING + EVENING (cùng ngày)

**Response:**
- `Success`: true nếu assign thành công
- `TotalDaysProcessed`: Tổng số ngày xử lý
- `TotalShiftsAssigned`: Số ca assign thành công
- `TotalGuardsAssigned`: Tổng số lượt guard được assign
- `DailySummaries`: Chi tiết từng ngày
- `Warnings`: Các cảnh báo (conflict, không tìm thấy shift)
- `Errors`: Lỗi (nếu có)

**Example Request:**
```json
{
  ""startDate"": ""2025-12-15"",
  ""endDate"": ""2025-12-30"",
  ""shiftTimeSlot"": ""MORNING"",
  ""locationId"": ""guid"",
  ""contractId"": ""guid"",
  ""assignmentType"": ""REGULAR"",
  ""assignmentNotes"": ""Assigned for December shifts""
}
```
");
    }
}
