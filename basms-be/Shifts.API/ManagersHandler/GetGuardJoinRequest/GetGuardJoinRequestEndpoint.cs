// Endpoint API để manager xem danh sách guards đã gửi join request
namespace Shifts.API.ManagersHandler.GetGuardJoinRequest;

public class GetGuardJoinRequestEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/shifts/managers/{managerId}/guard-join-requests", async (
            Guid managerId,
            ISender sender) =>
        {
            var query = new GetGuardJoinRequestQuery(managerId);
            var result = await sender.Send(query);

            return Results.Ok(new
            {
                managerId = result.ManagerId,
                managerName = result.ManagerName,
                totalRequests = result.TotalRequests,
                pendingRequests = result.PendingRequests.Select(g => new
                {
                    guardId = g.GuardId,
                    employeeCode = g.EmployeeCode,
                    fullName = g.FullName,
                    email = g.Email,
                    phoneNumber = g.PhoneNumber,
                    avatarUrl = g.AvatarUrl,
                    dateOfBirth = g.DateOfBirth,
                    gender = g.Gender,
                    employmentStatus = g.EmploymentStatus,
                    hireDate = g.HireDate,
                    contractType = g.ContractType,
                    preferredShiftType = g.PreferredShiftType,
                    canWorkOvertime = g.CanWorkOvertime,
                    canWorkWeekends = g.CanWorkWeekends,
                    canWorkHolidays = g.CanWorkHolidays,
                    currentAvailability = g.CurrentAvailability,
                    // Performance metrics
                    totalShiftsWorked = g.TotalShiftsWorked,
                    totalHoursWorked = g.TotalHoursWorked,
                    attendanceRate = g.AttendanceRate,
                    punctualityRate = g.PunctualityRate,
                    noShowCount = g.NoShowCount,
                    violationCount = g.ViolationCount,
                    commendationCount = g.CommendationCount,
                    // Timestamps
                    requestedAt = g.UpdatedAt,
                    createdAt = g.CreatedAt
                })
            });
        })
        .RequireAuthorization()
        .WithTags("Managers")
        .WithName("GetGuardJoinRequests")
        .Produces<GetGuardJoinRequestResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Get pending guard join requests")
        .WithDescription(@"
            Retrieves all guards who have sent join requests to this manager.

            **Filters:**
            - DirectManagerId = {managerId}
            - ContractType = 'join_in_request'
            - IsDeleted = false

            **Returns:**
            - List of guards with full profile information
            - Performance metrics (attendance rate, punctuality, violations, etc.)
            - Sorted by request date (newest first)

            **Example Response:**
            ```json
            {
              ""managerId"": ""660e8400-e29b-41d4-a716-446655440000"",
              ""managerName"": ""Nguyễn Văn A"",
              ""totalRequests"": 5,
              ""pendingRequests"": [
                {
                  ""guardId"": ""550e8400-e29b-41d4-a716-446655440000"",
                  ""employeeCode"": ""GRD001"",
                  ""fullName"": ""Trần Văn B"",
                  ""attendanceRate"": 95.5,
                  ""requestedAt"": ""2024-01-15T10:30:00Z""
                }
              ]
            }
            ```
        ");
    }
}
