namespace Shifts.API.Consumers;

/// <summary>
/// Consumer for UserCreatedEvent
/// Creates manager/guard cache in Shifts database when new user is created
/// </summary>
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

            // Determine if user is manager or guard
            var isManager = @event.RoleName.ToLower() is "manager";
            var isGuard = @event.RoleName.ToLower() == "guard";

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

            // Log successful sync
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

            // Log failed sync
            using var connection = await _dbFactory.CreateConnectionAsync();
            await LogSyncAsync(connection, @event, syncStarted, "FAILED", ex.Message);

            throw; // Re-throw to trigger MassTransit retry
        }
    }

    private async Task CreateManagerCacheAsync(IDbConnection connection, UserCreatedEvent @event)
    {
        var manager = new Managers
        {
            Id = @event.UserId,
            EmployeeCode = @event.EmployeeCode ?? $"MGR-{@event.UserId.ToString()[..8]}",
            FullName = @event.FullName,
            Email = @event.Email,
            PhoneNumber = @event.Phone,

            Role = @event.RoleName.ToUpper(),
            Position = @event.Position,
            Department = @event.Department,

            ManagerLevel = @event.RoleName.ToLower() switch
            {
                "director" => 3,
                "supervisor" => 2,
                _ => 1
            },

            EmploymentStatus = "ACTIVE",

            // Default permissions
            CanCreateShifts = true,
            CanApproveShifts = true,
            CanAssignGuards = true,
            CanApproveOvertime = true,
            CanManageTeams = true,

            IsActive = true,

            // Sync metadata
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
            EmployeeCode = @event.EmployeeCode ?? $"GRD-{@event.UserId.ToString()[..8]}",
            FullName = @event.FullName,
            Email = @event.Email,
            PhoneNumber = @event.Phone ?? "N/A",

            DateOfBirth = @event.DateOfBirth,
            Gender = @event.Gender,
            NationalId = @event.NationalId,
            CurrentAddress = @event.Address,

            EmploymentStatus = "ACTIVE",
            HireDate = @event.HireDate ?? DateTime.Today,
            ContractType = @event.ContractType,

            // Default preferences
            MaxWeeklyHours = 48,
            CanWorkOvertime = true,
            CanWorkWeekends = true,
            CanWorkHolidays = true,

            CurrentAvailability = "AVAILABLE",

            IsActive = true,

            // Sync metadata
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
            UserType = @event.RoleName.ToLower() is "manager" or "director" or "supervisor" ? "MANAGER" : "GUARD",
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