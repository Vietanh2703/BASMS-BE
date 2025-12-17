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
        app.MapGet("/api/shifts/guards/manager/{managerId:guid}/level-i", async (
            Guid managerId,
            ISender sender,
            ILogger<GetAllGuardLevelIEndpoint> logger,
            CancellationToken cancellationToken) =>
        {
            logger.LogInformation(
                "GET /api/shifts/guards/manager/{ManagerId}/level-i - Getting all Level I guards",
                managerId);

            var query = new GetAllGuardLevelIQuery(managerId);
            var result = await sender.Send(query, cancellationToken);

            logger.LogInformation(
                "✓ Found {Count} Level I guards for Manager {ManagerId}",
                result.TotalGuards,
                managerId);

            return Results.Ok(new
            {
                success = true,
                managerId = result.ManagerId,
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
        .WithSummary("Lấy danh sách guards có CertificationLevel I theo ManagerId")
        .WithDescription(@"
**Chức năng:**
Trả về danh sách tất cả guards có CertificationLevel = I (Hạng I theo Nghị định 96/2016/NĐ-CP) thuộc quản lý của một Manager cụ thể.

**Logic:**
- Filter guards có CertificationLevel = ""I""
- Filter guards có DirectManagerId khớp với managerId
- Chỉ lấy guards active (IsDeleted = false, IsActive = true)
- Sắp xếp theo EmployeeCode

**Use Cases:**
- Manager xem danh sách bảo vệ hạng I thuộc quyền quản lý
- Tìm bảo vệ hạng I để assign vào shifts yêu cầu trình độ cao
- Thống kê số lượng bảo vệ hạng I theo từng manager

**Response Structure:**
```json
{
  ""success"": true,
  ""managerId"": ""660e8400-e29b-41d4-a716-446655440000"",
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
- Chỉ trả về guards có DirectManagerId khớp với managerId
- Chỉ trả về guards active (IsDeleted = false, IsActive = true)
- CertificationLevel I là hạng cao nhất theo quy định
- Nếu không có guards Level I, trả về mảng rỗng

**Examples:**
```
GET /api/shifts/guards/manager/660e8400-e29b-41d4-a716-446655440000/level-i
```
        ");
    }
}
