using Carter;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Shifts.API.ShiftsHandler.GetGuardGroupByShiftId;

/// <summary>
/// Endpoint để lấy danh sách guards được phân công vào một shift
/// </summary>
public class GetGuardGroupByShiftIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/shifts/{shiftId:guid}/guards", async (
            [FromRoute] Guid shiftId,
            ISender sender,
            ILogger<GetGuardGroupByShiftIdEndpoint> logger,
            CancellationToken cancellationToken) =>
        {
            logger.LogInformation(
                "GET /api/shifts/{ShiftId}/guards - Getting guard group for shift",
                shiftId);

            var query = new GetGuardGroupByShiftIdQuery(shiftId);

            var result = await sender.Send(query, cancellationToken);

            if (!result.Success)
            {
                logger.LogWarning(
                    "Failed to get guard group for Shift {ShiftId}: {Error}",
                    shiftId,
                    result.ErrorMessage);

                return Results.BadRequest(new
                {
                    success = false,
                    error = result.ErrorMessage
                });
            }

            logger.LogInformation(
                "✓ Found {Count} guards for Shift {ShiftId}",
                result.TotalGuards,
                shiftId);

            return Results.Ok(new
            {
                success = true,
                data = new
                {
                    shiftId = result.ShiftId,
                    teamId = result.TeamId,
                    teamName = result.TeamName,
                    guards = result.Guards,
                    totalGuards = result.TotalGuards
                }
            });
        })
        .RequireAuthorization()
        .WithName("GetGuardGroupByShiftId")
        .WithTags("Shifts - Guards")
        .Produces(200)
        .Produces(400)
        .Produces(401)
        .Produces(404)
        .WithSummary("Lấy danh sách guards được phân công vào shift")
        .WithDescription(@"
**Chức năng:**
Trả về danh sách guards được phân công vào một shift cụ thể, bao gồm thông tin role trong team.

**Logic:**
- Tìm tất cả shift_assignments cho ShiftId
- JOIN với guards table để lấy thông tin guard
- JOIN với team_members để lấy role (LEADER | DEPUTY | MEMBER)
- JOIN với teams để lấy team name
- Sắp xếp: LEADER trước, sau đó DEPUTY, cuối cùng là MEMBER

**Use Cases:**
- Manager xem danh sách guards đã được phân công vào một ca
- Biết ai là leader của nhóm
- Xem trạng thái assignment của từng guard (ASSIGNED, CONFIRMED, DECLINED, etc.)
- Xem thông tin liên hệ của guards trong ca

**Response Structure:**
```json
{
  ""success"": true,
  ""data"": {
    ""shiftId"": ""123e4567-e89b-12d3-a456-426614174000"",
    ""teamId"": ""987e4567-e89b-12d3-a456-426614174001"",
    ""teamName"": ""Team Alpha"",
    ""totalGuards"": 3,
    ""guards"": [
      {
        ""assignmentId"": ""aaa-111"",
        ""guardId"": ""guard-001"",
        ""employeeCode"": ""GRD001"",
        ""fullName"": ""Nguyễn Văn A"",
        ""avatarUrl"": ""https://example.com/avatar1.jpg"",
        ""email"": ""nguyenvana@example.com"",
        ""phoneNumber"": ""0901234567"",
        ""gender"": ""MALE"",
        ""employmentStatus"": ""ACTIVE"",
        ""role"": ""LEADER"",
        ""isLeader"": true,
        ""assignmentStatus"": ""CONFIRMED"",
        ""assignmentType"": ""REGULAR"",
        ""assignedAt"": ""2025-12-10T08:00:00Z"",
        ""confirmedAt"": ""2025-12-10T09:00:00Z"",
        ""certificationLevel"": ""SENIOR""
      },
      {
        ""assignmentId"": ""aaa-222"",
        ""guardId"": ""guard-002"",
        ""employeeCode"": ""GRD002"",
        ""fullName"": ""Trần Thị B"",
        ""avatarUrl"": ""https://example.com/avatar2.jpg"",
        ""email"": ""tranthib@example.com"",
        ""phoneNumber"": ""0907654321"",
        ""gender"": ""FEMALE"",
        ""employmentStatus"": ""ACTIVE"",
        ""role"": ""MEMBER"",
        ""isLeader"": false,
        ""assignmentStatus"": ""ASSIGNED"",
        ""assignmentType"": ""REGULAR"",
        ""assignedAt"": ""2025-12-10T08:00:00Z"",
        ""confirmedAt"": null,
        ""certificationLevel"": ""INTERMEDIATE""
      }
    ]
  }
}
```

**Notes:**
- Guards được sắp xếp theo thứ tự: LEADER → DEPUTY → MEMBER
- Nếu shift không có guards (chưa được phân công), trả về mảng rỗng
- Nếu shift không tồn tại, trả về 400 Bad Request
- Role mặc định là MEMBER nếu không có team assignment

**Examples:**
```
GET /api/shifts/123e4567-e89b-12d3-a456-426614174000/guards
```
        ");
    }
}
