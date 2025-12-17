using Carter;
using MediatR;

namespace Shifts.API.GuardsHandler.GetAllGuardLevelI;

/// <summary>
/// Endpoint để lấy tất cả guards có CertificationLevel I
/// </summary>
public class GetAllGuardLevelIEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/shifts/guards/level-i", async (
            ISender sender,
            ILogger<GetAllGuardLevelIEndpoint> logger,
            CancellationToken cancellationToken) =>
        {
            logger.LogInformation("GET /api/shifts/guards/level-i - Getting all Level I guards");

            var query = new GetAllGuardLevelIQuery();
            var result = await sender.Send(query, cancellationToken);

            logger.LogInformation(
                "✓ Found {Count} Level I guards",
                result.TotalGuards);

            return Results.Ok(new
            {
                success = true,
                totalGuards = result.TotalGuards,
                guards = result.Guards.Select(g => new
                {
                    id = g.Id,
                    identityNumber = g.IdentityNumber,
                    employeeCode = g.EmployeeCode,
                    fullName = g.FullName,
                    email = g.Email,
                    avatarUrl = g.AvatarUrl,
                    phoneNumber = g.PhoneNumber,
                    dateOfBirth = g.DateOfBirth,
                    gender = g.Gender,
                    currentAddress = g.CurrentAddress,
                    employmentStatus = g.EmploymentStatus,
                    hireDate = g.HireDate,
                    contractType = g.ContractType,
                    certificationLevel = g.CertificationLevel,
                    terminationDate = g.TerminationDate,
                    terminationReason = g.TerminationReason,
                    maxWeeklyHours = g.MaxWeeklyHours,
                    canWorkOvertime = g.CanWorkOvertime,
                    canWorkWeekends = g.CanWorkWeekends,
                    canWorkHolidays = g.CanWorkHolidays,
                    currentAvailability = g.CurrentAvailability,
                    isActive = g.IsActive,
                    lastSyncedAt = g.LastSyncedAt,
                    syncStatus = g.SyncStatus,
                    userServiceVersion = g.UserServiceVersion,
                    createdAt = g.CreatedAt,
                    updatedAt = g.UpdatedAt
                })
            });
        })
        .RequireAuthorization()
        .WithName("GetAllGuardLevelI")
        .WithTags("Guards - Certification")
        .Produces(200)
        .Produces(401)
        .Produces(500)
        .WithSummary("Lấy danh sách guards có CertificationLevel I")
        .WithDescription(@"
**Chức năng:**
Trả về danh sách tất cả guards có CertificationLevel = I (Hạng I theo Nghị định 96/2016/NĐ-CP).

**Logic:**
- Filter guards có CertificationLevel = ""I""
- Chỉ lấy guards active (IsDeleted = false, IsActive = true)
- Sắp xếp theo EmployeeCode

**Use Cases:**
- Quản lý xem danh sách bảo vệ hạng I
- Tìm bảo vệ hạng I để assign vào shifts yêu cầu trình độ cao
- Thống kê số lượng bảo vệ theo hạng chứng chỉ

**Response Structure:**
```json
{
  ""success"": true,
  ""totalGuards"": 10,
  ""guards"": [
    {
      ""id"": ""770e8400-e29b-41d4-a716-446655440000"",
      ""employeeCode"": ""GRD001"",
      ""fullName"": ""Nguyen Van A"",
      ""email"": ""nguyenvana@example.com"",
      ""phoneNumber"": ""0901234567"",
      ""certificationLevel"": ""I"",
      ""employmentStatus"": ""ACTIVE"",
      ""currentAvailability"": ""AVAILABLE"",
      ""isActive"": true
    }
  ]
}
```

**Notes:**
- Chỉ trả về guards active (IsDeleted = false, IsActive = true)
- CertificationLevel I là hạng cao nhất theo quy định
- Nếu không có guards Level I, trả về mảng rỗng

**Examples:**
```
GET /api/shifts/guards/level-i
```
        ");
    }
}
