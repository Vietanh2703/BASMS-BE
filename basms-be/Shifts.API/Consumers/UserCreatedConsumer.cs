namespace Shifts.API.Consumers;

public class UserCreatedConsumer : IConsumer<UserCreatedEvent>
{
    private readonly IDbConnectionFactory _dbFactory;
    private readonly ILogger<UserCreatedConsumer> _logger;

    public UserCreatedConsumer(
        IDbConnectionFactory dbFactory,
        ILogger<UserCreatedConsumer> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<UserCreatedEvent> context)
    {
        var @event = context.Message;

        _logger.LogInformation(
            "Received UserCreatedEvent for User {UserId} with Role {RoleName}",
            @event.UserId,
            @event.RoleName);

        var syncStarted = DateTime.UtcNow;

        try
        {
            using var connection = await _dbFactory.CreateConnectionAsync();
            var isManager = @event.RoleName.ToLower() is "manager";
            var isGuard = @event.RoleName.ToLower() is "guard";

            if (isManager)
            {
                await CreateManagerCacheAsync(connection, @event);
            }
            else if (isGuard)
            {
                await CreateGuardCacheAsync(connection, @event);
            }
            else
            {
                _logger.LogInformation(
                    "User {UserId} has role {RoleName} - not creating cache (only manager/guard are cached)",
                    @event.UserId,
                    @event.RoleName);
                return;
            }

            await LogSyncAsync(connection, @event, syncStarted, "SUCCESS", null);

            _logger.LogInformation(
                "Successfully synced user {UserId} as {UserType}",
                @event.UserId,
                isManager ? "MANAGER" : "GUARD");
        }

        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to process UserCreatedEvent for User {UserId}",
                @event.UserId);
            
            using var connection = await _dbFactory.CreateConnectionAsync();
            await LogSyncAsync(connection, @event, syncStarted, "FAILED", ex.Message);

            throw; 
        }
    }
    
    private async Task CreateManagerCacheAsync(IDbConnection connection, UserCreatedEvent @event)
    {
        var manager = new Managers
        {
            Id = @event.UserId,
            IdentityNumber = @event.IdentityNumber,
            IdentityIssueDate = @event.IdentityIssueDate,
            IdentityIssuePlace = @event.IdentityIssuePlace,
            EmployeeCode = @event.EmployeeCode ?? $"MGR-{@event.UserId.ToString()[..8]}",
            FullName = @event.FullName,
            Email = @event.Email,
            Gender = @event.Gender,
            DateOfBirth = @event.DateOfBirth,
            AvatarUrl = @event.AvatarUrl,
            CurrentAddress = @event.Address,
            PhoneNumber = @event.Phone,

            Role = @event.RoleName.ToUpper(),
            CertificationLevel = @event.CertificationLevel,
            StandardWage = @event.StandardWage,

            EmploymentStatus = "ACTIVE",


            CanCreateShifts = true,
            CanApproveShifts = false,
            CanAssignGuards = true,
            CanApproveOvertime = true,
            CanManageTeams = true,

            IsActive = true,
            LastSyncedAt = DateTime.UtcNow,
            SyncStatus = "SYNCED",
            UserServiceVersion = @event.Version,

            CreatedAt = @event.CreatedAt
        };

        await connection.InsertAsync(manager);

        _logger.LogInformation(
            "Created manager cache for User {UserId}: {EmployeeCode}",
            @event.UserId,
            manager.EmployeeCode);
    }

    private async Task CreateGuardCacheAsync(IDbConnection connection, UserCreatedEvent @event)
    {
        var guard = new Guards
        {
            Id = @event.UserId,
            IdentityNumber = @event.IdentityNumber,
            IdentityIssueDate = @event.IdentityIssueDate,
            IdentityIssuePlace = @event.IdentityIssuePlace,
            EmployeeCode = @event.EmployeeCode ?? $"GRD-{@event.UserId.ToString()[..8]}",
            FullName = @event.FullName,
            Email = @event.Email,
            AvatarUrl = @event.AvatarUrl,
            PhoneNumber = @event.Phone ?? "N/A",
            DateOfBirth = @event.DateOfBirth,
            Gender = @event.Gender,
            CurrentAddress = @event.Address,
            EmploymentStatus = "ACTIVE",
            HireDate = @event.HireDate ?? DateTime.Today,
            ContractType = @event.ContractType,
            MaxWeeklyHours = 48,
            CanWorkOvertime = true,
            CanWorkWeekends = true,
            CanWorkHolidays = true,
            CurrentAvailability = "AVAILABLE",
            IsActive = true,
            LastSyncedAt = DateTime.UtcNow,
            SyncStatus = "SYNCED",
            UserServiceVersion = @event.Version,
            CreatedAt = @event.CreatedAt
        };

        await connection.InsertAsync(guard);

        _logger.LogInformation(
            "Created guard cache for User {UserId}: {EmployeeCode}",
            @event.UserId,
            guard.EmployeeCode);
    }

    private async Task LogSyncAsync(
        IDbConnection connection,
        UserCreatedEvent @event,
        DateTime syncStarted,
        string status,
        string? errorMessage)
    {
        var log = new UserSyncLog
        {
            Id = Guid.NewGuid(),
            UserId = @event.UserId,
            UserType = @event.RoleName.ToLower() == "manager" ? "MANAGER" : "GUARD",
            SyncType = "CREATE",
            SyncStatus = status,
            SyncInitiatedBy = "WEBHOOK",
            UserServiceVersionAfter = @event.Version,
            ErrorMessage = errorMessage,
            SyncStartedAt = syncStarted,
            SyncCompletedAt = DateTime.UtcNow,
            SyncDurationMs = (int)(DateTime.UtcNow - syncStarted).TotalMilliseconds,
            CreatedAt = DateTime.UtcNow
        };

        await connection.InsertAsync(log);
    }
}