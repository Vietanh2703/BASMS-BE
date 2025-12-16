// Endpoint API để lấy tất cả guards đã joined theo ManagerId
// Trả về danh sách guards có DirectManagerId trùng với ManagerId và ContractType = "joined_in"
namespace Shifts.API.GuardsHandler.GetAllGuardJoined;

public class GetAllGuardJoinedEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // Route: GET /api/shifts/guards/joined/{managerId}
        app.MapGet("/api/shifts/guards/joined/{managerId:guid}", async (Guid managerId, ISender sender) =>
        {
            var query = new GetAllGuardJoinedQuery(managerId);
            var result = await sender.Send(query);
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
        .WithTags("Guards")
        .WithName("GetAllGuardJoined")
        .Produces<GetAllGuardJoinedResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Get all joined guards by manager ID")
        .WithDescription(@"
            Retrieves all guards who have joined (ContractType = 'joined_in') and
            have the specified manager as their DirectManager.

            **Features:**
            - Filters guards by DirectManagerId
            - Filters guards by ContractType = 'joined_in'
            - Returns only active guards (IsDeleted = false)
            - Orders by EmployeeCode
            - Includes full guard details

            **Use Case:**
            This endpoint is used when a manager wants to see all guards who have
            officially joined their team (completed the join request process).
            It excludes guards with other contract types like pending, probation, etc.

            **Filters Applied:**
            1. DirectManagerId = {managerId}
            2. ContractType = 'joined_in'
            3. IsDeleted = false

            **Response Structure:**
            ```json
            {
              ""success"": true,
              ""managerId"": ""660e8400-e29b-41d4-a716-446655440000"",
              ""totalGuards"": 3,
              ""guards"": [
                {
                  ""id"": ""770e8400-e29b-41d4-a716-446655440000"",
                  ""employeeCode"": ""GRD001"",
                  ""fullName"": ""Nguyen Van A"",
                  ""email"": ""nguyenvana@example.com"",
                  ""phoneNumber"": ""0901234567"",
                  ""employmentStatus"": ""ACTIVE"",
                  ""contractType"": ""joined_in"",
                  ""currentAvailability"": ""AVAILABLE"",
                  ""isActive"": true
                }
              ]
            }
            ```

            **Difference from GetAllGuardsByManager:**
            - GetAllGuardsByManager: Returns ALL guards under a manager
            - GetAllGuardJoined: Returns ONLY guards with ContractType = 'joined_in'
        ");
    }
}
