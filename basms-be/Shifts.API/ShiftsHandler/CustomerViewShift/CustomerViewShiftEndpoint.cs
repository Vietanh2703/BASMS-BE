namespace Shifts.API.ShiftsHandler.CustomerViewShift;

/// <summary>
/// Endpoint để Customer xem các ca trực của contract
/// </summary>
public class CustomerViewShiftEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/shifts/customer/{contractId}/view", async (
            Guid contractId,
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            [FromQuery] string? status,
            [FromQuery] string? shiftType,
            ISender sender,
            ILogger<CustomerViewShiftEndpoint> logger,
            CancellationToken cancellationToken) =>
        {
            logger.LogInformation(
                "GET /api/shifts/customer/{ContractId}/view - Customer viewing contract shifts",
                contractId);

            var query = new CustomerViewShiftQuery(
                ContractId: contractId,
                FromDate: fromDate,
                ToDate: toDate,
                Status: status,
                ShiftType: shiftType
            );

            var result = await sender.Send(query, cancellationToken);

            if (!result.Success)
            {
                logger.LogWarning(
                    "Failed to get shifts for contract {ContractId}: {Error}",
                    contractId,
                    result.ErrorMessage);

                return Results.BadRequest(new
                {
                    success = false,
                    error = result.ErrorMessage
                });
            }

            logger.LogInformation(
                "✓ Retrieved {Count} shifts for contract {ContractId}",
                result.Shifts.Count,
                contractId);

            return Results.Ok(new
            {
                success = true,
                data = result.Shifts,
                totalCount = result.TotalCount,
                summary = result.Summary,
                message = "Danh sách ca trực được sắp xếp theo ngày và giờ",
                filters = new
                {
                    contractId = contractId.ToString(),
                    fromDate = fromDate?.ToString("yyyy-MM-dd") ?? "all",
                    toDate = toDate?.ToString("yyyy-MM-dd") ?? "all",
                    status = status ?? "all",
                    shiftType = shiftType ?? "all"
                }
            });
        })
        .RequireAuthorization()
        .WithName("CustomerViewShift")
        .WithTags("Shifts", "Customer")
        .Produces(200)
        .Produces(400)
        .WithSummary("Customer xem các ca trực của contract")
        .WithDescription(@"
            Endpoint này cho phép khách hàng xem tất cả các ca trực thuộc contract của mình.

            Thông tin trả về:
            - Danh sách ca trực (ngày, giờ, địa điểm, loại ca)
            - Tình trạng nhân sự (số lượng bảo vệ yêu cầu, đã phân công, đã xác nhận, đã check-in)
            - Trạng thái ca trực (SCHEDULED, IN_PROGRESS, COMPLETED, CANCELLED)
            - Phân loại ca (sáng/chiều/tối, cuối tuần, ngày lễ)
            - Tóm tắt tổng quan (tổng số ca, ca đã hoàn thành, ca thiếu người, tổng giờ làm)

            Query Parameters:
            - contractId (required - in path): ID của contract
            - fromDate (optional): Xem ca từ ngày này (yyyy-MM-dd)
            - toDate (optional): Xem ca đến ngày này (yyyy-MM-dd)
            - status (optional): Lọc theo trạng thái ca trực
              + DRAFT - Nháp
              + SCHEDULED - Đã lên lịch
              + IN_PROGRESS - Đang diễn ra
              + COMPLETED - Đã hoàn thành
              + CANCELLED - Đã hủy
              + PARTIAL - Hoàn thành một phần
            - shiftType (optional): Lọc theo loại ca
              + REGULAR - Ca thường
              + OVERTIME - Tăng ca
              + EMERGENCY - Ca khẩn cấp
              + REPLACEMENT - Ca thay thế
              + TRAINING - Ca huấn luyện

            Response Summary bao gồm:
            - Tổng số ca trực
            - Số ca theo trạng thái (scheduled, in-progress, completed, cancelled)
            - Số ca fully staffed / understaffed
            - Tổng giờ làm đã lên lịch / đã hoàn thành
            - Số ca đêm, ca cuối tuần, ca ngày lễ

            Sắp xếp:
            - Theo ngày tăng dần (ShiftDate ASC)
            - Theo giờ bắt đầu ca (ShiftStart ASC)

            Examples:
            GET /api/shifts/contracts/{contractId}/view
            GET /api/shifts/contracts/{contractId}/view?fromDate=2025-12-01&toDate=2025-12-31
            GET /api/shifts/contracts/{contractId}/view?status=SCHEDULED
            GET /api/shifts/contracts/{contractId}/view?status=COMPLETED&fromDate=2025-11-01&toDate=2025-11-30
            GET /api/shifts/contracts/{contractId}/view?shiftType=NIGHT

            Use Cases:
            - Customer theo dõi lịch ca trực của contract
            - Customer kiểm tra tình trạng nhân sự của các ca
            - Customer xem báo cáo ca đã hoàn thành trong tháng
            - Customer theo dõi ca thiếu người để yêu cầu bổ sung
            - Dashboard hiển thị tổng quan contract cho customer
        ");
    }
}
