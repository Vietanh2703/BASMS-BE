namespace Shifts.API.GuardsHandler.GetAllGuardLevelIIAndIII;

public class GetAllGuardLevelIIAndIIIEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/shifts/guards/manager/{managerId:guid}/level-ii-and-iii", async (
            Guid managerId,
            ISender sender,
            ILogger<GetAllGuardLevelIIAndIIIEndpoint> logger,
            CancellationToken cancellationToken) =>
        {
            logger.LogInformation(
                "GET /api/shifts/guards/manager/{ManagerId}/level-ii-and-iii - Getting all Level II and III guards",
                managerId);

            var query = new GetAllGuardLevelIIAndIIIQuery(managerId);
            var result = await sender.Send(query, cancellationToken);

            logger.LogInformation(
                "✓ Found {TotalCount} guards for Manager {ManagerId}: {LevelIICount} Level II, {LevelIIICount} Level III",
                result.TotalGuards,
                managerId,
                result.LevelIICount,
                result.LevelIIICount);

            return Results.Ok(new
            {
                success = true,
                managerId = result.ManagerId,
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
        .WithSummary("Lấy danh sách guards có CertificationLevel II và III");
    }
}
