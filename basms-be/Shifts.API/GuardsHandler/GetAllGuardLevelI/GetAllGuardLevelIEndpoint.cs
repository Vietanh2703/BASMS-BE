namespace Shifts.API.GuardsHandler.GetAllGuardLevelI;

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
                "Found {Count} Level I guards for Manager {ManagerId}",
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
        .WithSummary("Lấy danh sách guards có CertificationLevel I theo ManagerId");
    }
}
