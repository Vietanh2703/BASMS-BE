using Carter;
using MediatR;

namespace Shifts.API.GuardsHandler.GetAllGuardLevelIIAndIII;

/// <summary>
/// Endpoint để lấy tất cả guards có CertificationLevel II và III
/// </summary>
public class GetAllGuardLevelIIAndIIIEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/shifts/guards/level-ii-and-iii", async (
            ISender sender,
            ILogger<GetAllGuardLevelIIAndIIIEndpoint> logger,
            CancellationToken cancellationToken) =>
        {
            logger.LogInformation("GET /api/shifts/guards/level-ii-and-iii - Getting all Level II and III guards");

            var query = new GetAllGuardLevelIIAndIIIQuery();
            var result = await sender.Send(query, cancellationToken);

            logger.LogInformation(
                "✓ Found {TotalCount} guards: {LevelIICount} Level II, {LevelIIICount} Level III",
                result.TotalGuards,
                result.LevelIICount,
                result.LevelIIICount);

            return Results.Ok(new
            {
                success = true,
                totalGuards = result.TotalGuards,
                levelIICount = result.LevelIICount,
                levelIIICount = result.LevelIIICount,
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
        .WithName("GetAllGuardLevelIIAndIII")
        .WithTags("Guards - Certification")
        .Produces(200)
        .Produces(401)
        .Produces(500)
        .WithSummary("Lấy danh sách guards có CertificationLevel II và III")
        .WithDescription(@"
**Chức năng:**
Trả về danh sách tất cả guards có CertificationLevel = II hoặc III (Hạng II và III theo Nghị định 96/2016/NĐ-CP).

**Logic:**
- Filter guards có CertificationLevel = ""II"" hoặc ""III""
- Chỉ lấy guards active (IsDeleted = false, IsActive = true)
- Sắp xếp theo CertificationLevel (II trước, III sau), sau đó theo EmployeeCode
- Thống kê số lượng guards theo từng level

**Use Cases:**
- Quản lý xem danh sách bảo vệ hạng II và III
- Tìm bảo vệ để assign vào shifts yêu cầu trình độ trung cấp
- Thống kê phân bố bảo vệ theo hạng chứng chỉ
- So sánh số lượng bảo vệ Level II vs Level III

**Response Structure:**
```json
{
  ""success"": true,
  ""totalGuards"": 25,
  ""levelIICount"": 15,
  ""levelIIICount"": 10,
  ""guards"": [
    {
      ""id"": ""770e8400-e29b-41d4-a716-446655440000"",
      ""employeeCode"": ""GRD002"",
      ""fullName"": ""Tran Van B"",
      ""email"": ""tranvanb@example.com"",
      ""phoneNumber"": ""0902345678"",
      ""certificationLevel"": ""II"",
      ""employmentStatus"": ""ACTIVE"",
      ""currentAvailability"": ""AVAILABLE"",
      ""isActive"": true
    },
    {
      ""id"": ""880e8400-e29b-41d4-a716-446655440000"",
      ""employeeCode"": ""GRD003"",
      ""fullName"": ""Le Thi C"",
      ""email"": ""lethic@example.com"",
      ""phoneNumber"": ""0903456789"",
      ""certificationLevel"": ""III"",
      ""employmentStatus"": ""ACTIVE"",
      ""currentAvailability"": ""AVAILABLE"",
      ""isActive"": true
    }
  ]
}
```

**Notes:**
- Chỉ trả về guards active (IsDeleted = false, IsActive = true)
- Guards Level II được sắp xếp trước guards Level III
- Response bao gồm thống kê: totalGuards, levelIICount, levelIIICount
- Nếu không có guards, trả về mảng rỗng với count = 0

**Examples:**
```
GET /api/shifts/guards/level-ii-and-iii
```
        ");
    }
}
