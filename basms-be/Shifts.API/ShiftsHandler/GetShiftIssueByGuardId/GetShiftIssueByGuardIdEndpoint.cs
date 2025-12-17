using Carter;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Shifts.API.ShiftsHandler.GetShiftIssueByGuardId;

/// <summary>
/// Endpoint để lấy danh sách shift issues của một guard
/// </summary>
public class GetShiftIssueByGuardIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/shifts/guards/{guardId:guid}/shift-issues", async (
            [FromRoute] Guid guardId,
            ISender sender,
            ILogger<GetShiftIssueByGuardIdEndpoint> logger,
            CancellationToken cancellationToken) =>
        {
            logger.LogInformation(
                "GET /api/guards/{GuardId}/shift-issues - Getting shift issues for guard",
                guardId);

            var query = new GetShiftIssueByGuardIdQuery(guardId);

            var result = await sender.Send(query, cancellationToken);

            if (!result.Success)
            {
                logger.LogWarning(
                    "Failed to get shift issues for Guard {GuardId}: {Error}",
                    guardId,
                    result.ErrorMessage);

                return Results.BadRequest(new
                {
                    success = false,
                    error = result.ErrorMessage
                });
            }

            logger.LogInformation(
                "✓ Found {Count} shift issues for Guard {GuardId}",
                result.TotalIssues,
                guardId);

            return Results.Ok(new
            {
                success = true,
                data = new
                {
                    guardId = result.GuardId,
                    issues = result.Issues,
                    totalIssues = result.TotalIssues
                }
            });
        })
        .RequireAuthorization()
        .WithName("GetShiftIssueByGuardId")
        .WithTags("Guards - Issues")
        .Produces(200)
        .Produces(400)
        .Produces(401)
        .Produces(404)
        .WithSummary("Lấy danh sách shift issues của guard")
        .WithDescription(@"
**Chức năng:**
Trả về danh sách tất cả shift issues liên quan đến một guard cụ thể.

**Logic:**
- Tìm tất cả shift_issues có GuardId khớp
- Sắp xếp theo IssueDate giảm dần (mới nhất trước)
- Bao gồm các loại issue: CANCEL_SHIFT, BULK_CANCEL, SICK_LEAVE, MATERNITY_LEAVE, LONG_TERM_LEAVE, OTHER

**Use Cases:**
- Manager xem lịch sử nghỉ phép/nghỉ ốm của guard
- Kiểm tra các ca đã bị cancel
- Xem lý do và thời gian nghỉ của guard
- Theo dõi evidence files (giấy khám bệnh, đơn xin nghỉ, v.v.)

**Issue Types:**
- `CANCEL_SHIFT`: Hủy ca đơn lẻ
- `BULK_CANCEL`: Hủy nhiều ca (generic)
- `SICK_LEAVE`: Nghỉ ốm dài ngày
- `MATERNITY_LEAVE`: Nghỉ thai sản
- `LONG_TERM_LEAVE`: Nghỉ phép dài hạn
- `OTHER`: Loại khác

**Response Structure:**
```json
{
  ""success"": true,
  ""data"": {
    ""guardId"": ""123e4567-e89b-12d3-a456-426614174000"",
    ""totalIssues"": 2,
    ""issues"": [
      {
        ""id"": ""issue-001"",
        ""shiftId"": null,
        ""guardId"": ""123e4567-e89b-12d3-a456-426614174000"",
        ""issueType"": ""SICK_LEAVE"",
        ""reason"": ""Nghỉ ốm do cảm cúm"",
        ""startDate"": ""2025-12-01"",
        ""endDate"": ""2025-12-05"",
        ""issueDate"": ""2025-12-01T08:00:00Z"",
        ""evidenceFileUrl"": ""https://s3.aws.com/evidence/sick-leave-001.pdf"",
        ""totalShiftsAffected"": 10,
        ""totalGuardsAffected"": 1,
        ""createdAt"": ""2025-12-01T08:30:00Z"",
        ""createdBy"": ""manager-001""
      },
      {
        ""id"": ""issue-002"",
        ""shiftId"": ""shift-123"",
        ""guardId"": ""123e4567-e89b-12d3-a456-426614174000"",
        ""issueType"": ""CANCEL_SHIFT"",
        ""reason"": ""Có việc gia đình đột xuất"",
        ""startDate"": null,
        ""endDate"": null,
        ""issueDate"": ""2025-11-20T10:00:00Z"",
        ""evidenceFileUrl"": null,
        ""totalShiftsAffected"": 1,
        ""totalGuardsAffected"": 1,
        ""createdAt"": ""2025-11-20T10:15:00Z"",
        ""createdBy"": ""manager-002""
      }
    ]
  }
}
```

**Notes:**
- Issues được sắp xếp theo IssueDate giảm dần (mới nhất trước)
- ShiftId có thể null (nếu là bulk cancel nhiều shifts)
- StartDate/EndDate có thể null (nếu là cancel shift đơn lẻ)
- EvidenceFileUrl có thể null (nếu không có file chứng từ)
- Nếu guard không có issues, trả về mảng rỗng
- Nếu guard không tồn tại, trả về 400 Bad Request

**Examples:**
```
GET /api/guards/123e4567-e89b-12d3-a456-426614174000/shift-issues
```
        ");
    }
}
