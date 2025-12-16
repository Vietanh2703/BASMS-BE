namespace Shifts.API.GuardsHandler.GuardViewShiftAssigned;

/// <summary>
/// Endpoint để Guard xem lịch ca trực được phân công
/// </summary>
public class GuardViewShiftAssignedEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/shifts/guards/{guardId}/shifts/assigned", async (
            Guid guardId,
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            [FromQuery] string? status,
            ISender sender,
            ILogger<GuardViewShiftAssignedEndpoint> logger,
            CancellationToken cancellationToken) =>
        {
            logger.LogInformation(
                "GET /api/guards/{GuardId}/shifts/assigned - Guard viewing assigned shift schedule",
                guardId);

            var query = new GuardViewShiftAssignedQuery(
                GuardId: guardId,
                FromDate: fromDate,
                ToDate: toDate,
                Status: status
            );

            var result = await sender.Send(query, cancellationToken);

            if (!result.Success)
            {
                logger.LogWarning(
                    "Failed to get assigned shifts for guard {GuardId}: {Error}",
                    guardId,
                    result.ErrorMessage);

                return Results.BadRequest(new
                {
                    success = false,
                    error = result.ErrorMessage
                });
            }

            logger.LogInformation(
                "✓ Retrieved {Count} assigned shifts for guard {GuardId}",
                result.Shifts.Count,
                guardId);

            return Results.Ok(new
            {
                success = true,
                data = result.Shifts,
                totalCount = result.TotalCount,
                message = "Lịch ca trực được sắp xếp theo ngày và giờ",
                filters = new
                {
                    guardId = guardId.ToString(),
                    fromDate = fromDate?.ToString("yyyy-MM-dd") ?? "all",
                    toDate = toDate?.ToString("yyyy-MM-dd") ?? "all",
                    status = status ?? "all"
                }
            });
        })
        .RequireAuthorization()
        .WithName("GuardViewShiftAssigned")
        .WithTags("Guards", "Shifts")
        .Produces(200)
        .Produces(400)
        .WithSummary("Guard xem lịch ca trực được phân công")
        .WithDescription(@"
            Endpoint này cho phép bảo vệ xem lịch tất cả các ca trực mà mình đã được phân công.

            Thông tin trả về:
            - Thông tin ca trực (ngày, giờ, địa điểm, loại ca)
            - Trạng thái assignment (ASSIGNED, CONFIRMED, CHECKED_IN, COMPLETED, etc.)
            - Loại phân công (REGULAR, OVERTIME, EMERGENCY, REPLACEMENT)
            - Thông tin team (nếu được assign qua team)
            - Hướng dẫn đặc biệt, thiết bị cần thiết

            Query Parameters:
            - guardId (required - in path): ID của bảo vệ
            - fromDate (optional): Xem ca trực từ ngày này (yyyy-MM-dd)
            - toDate (optional): Xem ca trực đến ngày này (yyyy-MM-dd)
            - status (optional): Lọc theo trạng thái assignment
              + ASSIGNED - Đã phân công, chưa xác nhận
              + CONFIRMED - Đã xác nhận tham gia
              + DECLINED - Đã từ chối
              + CHECKED_IN - Đã check-in
              + CHECKED_OUT - Đã check-out
              + COMPLETED - Đã hoàn thành
              + NO_SHOW - Vắng mặt không phép
              + CANCELLED - Đã hủy

            Sắp xếp:
            - Theo ngày tăng dần (ShiftDate ASC)
            - Theo giờ bắt đầu ca (ShiftStart ASC)

            Examples:
            GET /api/guards/{guardId}/shifts/assigned
            GET /api/guards/{guardId}/shifts/assigned?fromDate=2025-12-15&toDate=2025-12-31
            GET /api/guards/{guardId}/shifts/assigned?status=ASSIGNED
            GET /api/guards/{guardId}/shifts/assigned?status=CONFIRMED&fromDate=2025-12-15

            Use Cases:
            - Bảo vệ xem lịch ca trực của mình trong tháng
            - Bảo vệ xem các ca sắp tới cần xác nhận
            - Bảo vệ kiểm tra lịch sử ca đã làm
            - App mobile hiển thị calendar view cho bảo vệ
        ");
    }
}
