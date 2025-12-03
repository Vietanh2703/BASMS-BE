// Endpoint API để lấy tất cả guards theo ManagerId
// Trả về danh sách guards có DirectManagerId trùng với ManagerId
namespace Shifts.API.GuardsHandler.GetAllGuardsByManager;

public class GetAllGuardsByManagerEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // Route: GET /api/shifts/guards/manager/{managerId}
        app.MapGet("/api/shifts/guards/manager/{managerId:guid}", async (Guid managerId, ISender sender) =>
        {
            // Bước 1: Tạo query với ManagerId
            var query = new GetAllGuardsByManagerQuery(managerId);

            // Bước 2: Gửi query đến Handler
            var result = await sender.Send(query);

            // Bước 3: Trả về 200 OK với danh sách guards
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
        .WithTags("Guards")
        .WithName("GetAllGuardsByManager")
        .Produces<GetAllGuardsByManagerResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Get all guards by manager ID")
        .WithDescription(@"
            Retrieves all guards who have the specified manager as their DirectManager.

            **Features:**
            - Filters guards by DirectManagerId
            - Returns only active guards (IsDeleted = false)
            - Orders by EmployeeCode
            - Includes full guard details

            **Use Case:**
            This endpoint is used when a manager wants to see all guards under their supervision.
            The response includes detailed information about each guard including:
            - Personal information (name, contact, DOB, etc.)
            - Employment status and contract details
            - Work preferences (overtime, weekends, holidays)
            - Current availability status
            - Performance and sync metadata

            **Response Structure:**
            ```json
            {
              ""success"": true,
              ""managerId"": ""660e8400-e29b-41d4-a716-446655440000"",
              ""totalGuards"": 5,
              ""guards"": [
                {
                  ""id"": ""770e8400-e29b-41d4-a716-446655440000"",
                  ""employeeCode"": ""GRD001"",
                  ""fullName"": ""Nguyen Van A"",
                  ""email"": ""nguyenvana@example.com"",
                  ""phoneNumber"": ""0901234567"",
                  ""employmentStatus"": ""ACTIVE"",
                  ""currentAvailability"": ""AVAILABLE"",
                  ""isActive"": true
                }
              ]
            }
            ```
        ");
    }
}
