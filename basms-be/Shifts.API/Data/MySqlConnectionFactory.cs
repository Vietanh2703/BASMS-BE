using MySql.Data.MySqlClient;
using System.Data;
using Dapper;

namespace Shifts.API.Data;

/// <summary>
/// MySQL connection factory for Shifts service
/// Creates tables based on Models/*.cs files
/// </summary>
public class MySqlConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private bool _tablesCreated;

    public MySqlConnectionFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<IDbConnection> CreateConnectionAsync()
    {
        var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        return connection;
    }

    public async Task EnsureTablesCreatedAsync()
    {
        if (_tablesCreated) return;

        await _semaphore.WaitAsync();
        try
        {
            if (_tablesCreated) return;

            // Create database if not exists
            var connectionStringBuilder = new MySqlConnectionStringBuilder(_connectionString);
            var databaseName = connectionStringBuilder.Database;
            connectionStringBuilder.Database = null;

            using (var tempConnection = new MySqlConnection(connectionStringBuilder.ConnectionString))
            {
                await tempConnection.OpenAsync();
                await tempConnection.ExecuteAsync($@"
                    CREATE DATABASE IF NOT EXISTS `{databaseName}`
                    CHARACTER SET utf8mb4
                    COLLATE utf8mb4_unicode_ci;
                ");
                Console.WriteLine($"✓ Database '{databaseName}' ready");
            }

            using var connection = await CreateConnectionAsync();

            // ============================================================================
            // 1. MANAGERS TABLE
            // ============================================================================
            await connection.ExecuteAsync(@"
                CREATE TABLE IF NOT EXISTS `managers` (
                    `Id` CHAR(36) PRIMARY KEY,

                    -- Basic info
                    `IdentityNumber` VARCHAR(12) NOT NULL,
                    `IdentityIssueDate` DATETIME NULL,
                    `IdentityIssuePlace` VARCHAR(255) NULL,
                    `EmployeeCode` VARCHAR(50) NOT NULL,
                    `FullName` VARCHAR(200) NOT NULL,
                    `AvatarUrl` TEXT NULL,
                    `Email` VARCHAR(255) NOT NULL,
                    `PhoneNumber` VARCHAR(20) NULL,
                    `CurrentAddress` VARCHAR(200) NULL,
                    `Gender` VARCHAR(10) NULL,
                    `DateOfBirth` DATETIME NULL,

                    -- Role & Position
                    `Role` VARCHAR(50) NOT NULL DEFAULT 'MANAGER',
                    `Position` VARCHAR(100) NULL,
                    `Department` VARCHAR(100) NULL,

                    -- Management level
                    `ManagerLevel` INT NOT NULL DEFAULT 1,
                    `ReportsToManagerId` CHAR(36) NULL,

                    -- Employment status
                    `EmploymentStatus` VARCHAR(50) NOT NULL DEFAULT 'ACTIVE',

                    -- Permissions
                    `CanCreateShifts` BOOLEAN NOT NULL DEFAULT TRUE,
                    `CanApproveShifts` BOOLEAN NOT NULL DEFAULT TRUE,
                    `CanAssignGuards` BOOLEAN NOT NULL DEFAULT TRUE,
                    `CanApproveOvertime` BOOLEAN NOT NULL DEFAULT TRUE,
                    `CanManageTeams` BOOLEAN NOT NULL DEFAULT TRUE,
                    `MaxTeamSize` INT NULL,

                    -- Statistics
                    `TotalTeamsManaged` INT NOT NULL DEFAULT 0,
                    `TotalGuardsSupervised` INT NOT NULL DEFAULT 0,
                    `TotalShiftsCreated` INT NOT NULL DEFAULT 0,

                    -- Sync metadata
                    `LastSyncedAt` DATETIME NULL,
                    `SyncStatus` VARCHAR(50) NOT NULL DEFAULT 'SYNCED',
                    `UserServiceVersion` INT NULL,
                    `IsActive` BOOLEAN NOT NULL DEFAULT TRUE,

                    -- Audit
                    `CreatedAt` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    `UpdatedAt` DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
                    `IsDeleted` BOOLEAN NOT NULL DEFAULT FALSE,
                    `DeletedAt` DATETIME NULL,

                    INDEX `idx_managers_code` (`EmployeeCode`),
                    INDEX `idx_managers_identity` (`IdentityNumber`),
                    INDEX `idx_managers_email` (`Email`),
                    INDEX `idx_managers_active` (`IsActive`, `IsDeleted`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            ");

            // ============================================================================
            // 2. GUARDS TABLE
            // ============================================================================
            await connection.ExecuteAsync(@"
                CREATE TABLE IF NOT EXISTS `guards` (
                    `Id` CHAR(36) PRIMARY KEY,

                    -- Basic info
                    `IdentityNumber` VARCHAR(12) NOT NULL,
                    `IdentityIssueDate` DATETIME NULL,
                    `IdentityIssuePlace` VARCHAR(255) NULL,
                    `EmployeeCode` VARCHAR(50) NOT NULL,
                    `FullName` VARCHAR(200) NOT NULL,
                    `AvatarUrl` TEXT NULL,
                    `Email` VARCHAR(255) NULL,
                    `PhoneNumber` VARCHAR(20) NOT NULL,

                    -- Personal info
                    `DateOfBirth` DATETIME NULL,
                    `Gender` VARCHAR(10) NULL,
                    `CurrentAddress` TEXT NULL,

                    -- Employment
                    `EmploymentStatus` VARCHAR(50) NOT NULL DEFAULT 'ACTIVE',
                    `HireDate` DATETIME NOT NULL,
                    `ProbationEndDate` DATETIME NULL,
                    `ContractType` VARCHAR(50) NULL,
                    `TerminationDate` DATETIME NULL,
                    `TerminationReason` TEXT NULL,

                    -- Management
                    `DirectManagerId` CHAR(36) NULL,

                    -- Preferences
                    `PreferredShiftType` VARCHAR(50) NULL,
                    `PreferredLocations` TEXT NULL,
                    `MaxWeeklyHours` INT NOT NULL DEFAULT 48,
                    `CanWorkOvertime` BOOLEAN NOT NULL DEFAULT TRUE,
                    `CanWorkWeekends` BOOLEAN NOT NULL DEFAULT TRUE,
                    `CanWorkHolidays` BOOLEAN NOT NULL DEFAULT TRUE,

                    -- Performance metrics
                    `TotalShiftsWorked` INT NOT NULL DEFAULT 0,
                    `TotalHoursWorked` DECIMAL(10,2) NOT NULL DEFAULT 0,
                    `AttendanceRate` DECIMAL(5,2) NULL,
                    `PunctualityRate` DECIMAL(5,2) NULL,
                    `NoShowCount` INT NOT NULL DEFAULT 0,
                    `ViolationCount` INT NOT NULL DEFAULT 0,
                    `CommendationCount` INT NOT NULL DEFAULT 0,

                    -- Realtime status
                    `CurrentAvailability` VARCHAR(50) NOT NULL DEFAULT 'AVAILABLE',
                    `AvailabilityNotes` TEXT NULL,

                    -- App & Biometric
                    `BiometricRegistered` BOOLEAN NOT NULL DEFAULT FALSE,
                    `FaceTemplateUrl` TEXT NULL,
                    `LastAppLogin` DATETIME NULL,
                    `DeviceTokens` TEXT NULL,

                    -- Sync metadata
                    `LastSyncedAt` DATETIME NULL,
                    `SyncStatus` VARCHAR(50) NOT NULL DEFAULT 'SYNCED',
                    `UserServiceVersion` INT NULL,
                    `IsActive` BOOLEAN NOT NULL DEFAULT TRUE,

                    -- Audit
                    `CreatedAt` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    `UpdatedAt` DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
                    `IsDeleted` BOOLEAN NOT NULL DEFAULT FALSE,
                    `DeletedAt` DATETIME NULL,

                    INDEX `idx_guards_code` (`EmployeeCode`),
                    INDEX `idx_guards_identity` (`IdentityNumber`),
                    INDEX `idx_guards_phone` (`PhoneNumber`),
                    INDEX `idx_guards_manager` (`DirectManagerId`),
                    INDEX `idx_guards_availability` (`CurrentAvailability`, `IsActive`),
                    INDEX `idx_guards_active` (`IsActive`, `IsDeleted`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            ");

            // ============================================================================
            // 3. USER_SYNC_LOG TABLE
            // ============================================================================
            await connection.ExecuteAsync(@"
                CREATE TABLE IF NOT EXISTS `user_sync_log` (
                    `Id` CHAR(36) PRIMARY KEY,
                    `UserId` CHAR(36) NOT NULL,
                    `UserType` VARCHAR(50) NOT NULL,
                    `SyncType` VARCHAR(50) NOT NULL,
                    `SyncStatus` VARCHAR(50) NOT NULL,
                    `FieldsChanged` TEXT NULL,
                    `OldValues` TEXT NULL,
                    `NewValues` TEXT NULL,
                    `SyncInitiatedBy` VARCHAR(50) NULL,
                    `UserServiceVersionBefore` INT NULL,
                    `UserServiceVersionAfter` INT NULL,
                    `ErrorMessage` TEXT NULL,
                    `ErrorCode` VARCHAR(50) NULL,
                    `RetryCount` INT NOT NULL DEFAULT 0,
                    `SyncStartedAt` DATETIME NOT NULL,
                    `SyncCompletedAt` DATETIME NULL,
                    `SyncDurationMs` INT NULL,
                    `CreatedAt` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,

                    INDEX `idx_sync_user` (`UserId`, `SyncType`),
                    INDEX `idx_sync_status` (`SyncStatus`, `CreatedAt`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            ");

            // ============================================================================
            // 4. TEAMS TABLE
            // ============================================================================
            await connection.ExecuteAsync(@"
                CREATE TABLE IF NOT EXISTS `teams` (
                    `Id` CHAR(36) PRIMARY KEY,
                    `ManagerId` CHAR(36) NOT NULL,
                    `TeamCode` VARCHAR(50) NOT NULL,
                    `TeamName` VARCHAR(200) NOT NULL,
                    `Description` TEXT NULL,
                    `MinMembers` INT NOT NULL DEFAULT 1,
                    `MaxMembers` INT NULL,
                    `CurrentMemberCount` INT NOT NULL DEFAULT 0,
                    `Specialization` VARCHAR(100) NULL,
                    `IsActive` BOOLEAN NOT NULL DEFAULT TRUE,
                    `CreatedAt` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    `UpdatedAt` DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
                    `CreatedBy` CHAR(36) NULL,
                    `UpdatedBy` CHAR(36) NULL,
                    `IsDeleted` BOOLEAN NOT NULL DEFAULT FALSE,
                    `DeletedAt` DATETIME NULL,
                    `DeletedBy` CHAR(36) NULL,

                    INDEX `idx_teams_code` (`TeamCode`),
                    INDEX `idx_teams_manager` (`ManagerId`),
                    INDEX `idx_teams_active` (`IsActive`, `IsDeleted`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            ");

            // ============================================================================
            // 5. TEAM_MEMBERS TABLE
            // ============================================================================
            await connection.ExecuteAsync(@"
                CREATE TABLE IF NOT EXISTS `team_members` (
                    `Id` CHAR(36) PRIMARY KEY,
                    `TeamId` CHAR(36) NOT NULL,
                    `GuardId` CHAR(36) NOT NULL,
                    `Role` VARCHAR(50) NOT NULL DEFAULT 'MEMBER',
                    `IsActive` BOOLEAN NOT NULL DEFAULT TRUE,
                    `PerformanceRating` DECIMAL(3,2) NULL,
                    `TotalShiftsCompleted` INT NOT NULL DEFAULT 0,
                    `TotalShiftsAssigned` INT NOT NULL DEFAULT 0,
                    `AttendanceRate` DECIMAL(5,2) NULL,
                    `JoiningNotes` TEXT NULL,
                    `LeavingNotes` TEXT NULL,
                    `CreatedAt` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    `UpdatedAt` DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
                    `CreatedBy` CHAR(36) NULL,
                    `UpdatedBy` CHAR(36) NULL,
                    `IsDeleted` BOOLEAN NOT NULL DEFAULT FALSE,

                    INDEX `idx_team_members_team` (`TeamId`, `IsActive`),
                    INDEX `idx_team_members_guard` (`GuardId`, `IsActive`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            ");

            // ============================================================================
            // 6. SHIFT_TEMPLATES TABLE
            // ============================================================================
            await connection.ExecuteAsync(@"
                CREATE TABLE IF NOT EXISTS `shift_templates` (
                    `Id` CHAR(36) PRIMARY KEY,
                    `ManagerId` CHAR(36) NULL,
                    `ContractId` CHAR(36) NULL,
                    `TemplateCode` VARCHAR(50) NOT NULL,
                    `TemplateName` VARCHAR(200) NOT NULL,
                    `Description` TEXT NULL,
                    `StartTime` TIME NOT NULL,
                    `EndTime` TIME NOT NULL,
                    `DurationHours` DECIMAL(4,2) NOT NULL,
                    `BreakDurationMinutes` INT NOT NULL DEFAULT 60,
                    `PaidBreakMinutes` INT NOT NULL DEFAULT 0,
                    `UnpaidBreakMinutes` INT NOT NULL DEFAULT 60,
                    `IsNightShift` BOOLEAN NOT NULL DEFAULT FALSE,
                    `IsOvernight` BOOLEAN NOT NULL DEFAULT FALSE,
                    `CrossesMidnight` BOOLEAN NOT NULL DEFAULT FALSE,
                    `AppliesMonday` BOOLEAN NOT NULL DEFAULT FALSE,
                    `AppliesTuesday` BOOLEAN NOT NULL DEFAULT FALSE,
                    `AppliesWednesday` BOOLEAN NOT NULL DEFAULT FALSE,
                    `AppliesThursday` BOOLEAN NOT NULL DEFAULT FALSE,
                    `AppliesFriday` BOOLEAN NOT NULL DEFAULT FALSE,
                    `AppliesSaturday` BOOLEAN NOT NULL DEFAULT FALSE,
                    `AppliesSunday` BOOLEAN NOT NULL DEFAULT FALSE,
                    `MinGuardsRequired` INT NOT NULL DEFAULT 1,
                    `MaxGuardsAllowed` INT NULL,
                    `OptimalGuards` INT NULL,
                    `LocationId` CHAR(36) NULL,
                    `LocationName` VARCHAR(200) NULL,
                    `LocationAddress` TEXT NULL,
                    `LocationLatitude` DECIMAL(10,8) NULL,
                    `LocationLongitude` DECIMAL(11,8) NULL,
                    `Status` VARCHAR(50) NOT NULL DEFAULT 'await_create_shift',
                    `IsActive` BOOLEAN NOT NULL DEFAULT TRUE,
                    `EffectiveFrom` DATETIME NULL,
                    `EffectiveTo` DATETIME NULL,
                    `CreatedAt` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    `UpdatedAt` DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
                    `CreatedBy` CHAR(36) NULL,
                    `UpdatedBy` CHAR(36) NULL,
                    `IsDeleted` BOOLEAN NOT NULL DEFAULT FALSE,

                    INDEX `idx_templates_code` (`TemplateCode`),
                    INDEX `idx_templates_manager` (`ManagerId`, `IsActive`),
                    INDEX `idx_templates_contract` (`ContractId`, `IsActive`),
                    INDEX `idx_templates_location` (`LocationId`, `IsActive`),
                    INDEX `idx_templates_active` (`IsActive`, `IsDeleted`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            ");

            // ============================================================================
            // 7. SHIFTS TABLE
            // ============================================================================
            await connection.ExecuteAsync(@"
                CREATE TABLE IF NOT EXISTS `shifts` (
                    `Id` CHAR(36) PRIMARY KEY,

                    -- Foreign Keys
                    `ShiftTemplateId` CHAR(36) NULL,
                    `LocationId` CHAR(36) NULL,
                    `LocationName` VARCHAR(200) NULL,
                    `LocationAddress` TEXT NULL,
                    `LocationLatitude` DECIMAL(10,8) NULL,
                    `LocationLongitude` DECIMAL(11,8) NULL,
                    `ContractId` CHAR(36) NULL,
                    `ManagerId` CHAR(36) NULL,

                    -- Date splitting
                    `ShiftDate` DATE NOT NULL,
                    `ShiftDay` INT NOT NULL,
                    `ShiftMonth` INT NOT NULL,
                    `ShiftYear` INT NOT NULL,
                    `ShiftQuarter` INT NOT NULL,
                    `ShiftWeek` INT NOT NULL,
                    `DayOfWeek` INT NOT NULL,
                    `ShiftEndDate` DATETIME NULL,

                    -- Time (DATETIME not TIME)
                    `ShiftStart` DATETIME NOT NULL,
                    `ShiftEnd` DATETIME NOT NULL,

                    -- Duration
                    `TotalDurationMinutes` INT NOT NULL DEFAULT 0,
                    `WorkDurationMinutes` INT NOT NULL DEFAULT 0,
                    `WorkDurationHours` DECIMAL(10,2) NOT NULL DEFAULT 0,
                    `BreakDurationMinutes` INT NOT NULL DEFAULT 60,
                    `PaidBreakMinutes` INT NOT NULL DEFAULT 0,
                    `UnpaidBreakMinutes` INT NOT NULL DEFAULT 60,

                    -- Staffing
                    `RequiredGuards` INT NOT NULL DEFAULT 1,
                    `AssignedGuardsCount` INT NOT NULL DEFAULT 0,
                    `ConfirmedGuardsCount` INT NOT NULL DEFAULT 0,
                    `CheckedInGuardsCount` INT NOT NULL DEFAULT 0,
                    `CompletedGuardsCount` INT NOT NULL DEFAULT 0,
                    `IsFullyStaffed` BOOLEAN NOT NULL DEFAULT FALSE,
                    `IsUnderstaffed` BOOLEAN NOT NULL DEFAULT FALSE,
                    `IsOverstaffed` BOOLEAN NOT NULL DEFAULT FALSE,
                    `StaffingPercentage` DECIMAL(5,2) NULL,

                    -- Day classification
                    `IsRegularWeekday` BOOLEAN NOT NULL DEFAULT TRUE,
                    `IsSaturday` BOOLEAN NOT NULL DEFAULT FALSE,
                    `IsSunday` BOOLEAN NOT NULL DEFAULT FALSE,
                    `IsPublicHoliday` BOOLEAN NOT NULL DEFAULT FALSE,
                    `IsTetHoliday` BOOLEAN NOT NULL DEFAULT FALSE,

                    -- Shift type classification
                    `IsNightShift` BOOLEAN NOT NULL DEFAULT FALSE,
                    `NightHours` DECIMAL(10,2) NOT NULL DEFAULT 0,
                    `DayHours` DECIMAL(10,2) NOT NULL DEFAULT 0,
                    `ShiftType` VARCHAR(50) NOT NULL DEFAULT 'REGULAR',

                    -- Flags
                    `IsMandatory` BOOLEAN NOT NULL DEFAULT FALSE,
                    `IsCritical` BOOLEAN NOT NULL DEFAULT FALSE,
                    `IsTrainingShift` BOOLEAN NOT NULL DEFAULT FALSE,
                    `RequiresArmedGuard` BOOLEAN NOT NULL DEFAULT FALSE,

                    -- Approval
                    `RequiresApproval` BOOLEAN NOT NULL DEFAULT TRUE,
                    `ApprovedBy` CHAR(36) NULL,
                    `ApprovedAt` DATETIME NULL,
                    `ApprovalStatus` VARCHAR(50) NOT NULL DEFAULT 'PENDING',
                    `RejectionReason` TEXT NULL,

                    -- Status & Lifecycle
                    `Status` VARCHAR(50) NOT NULL DEFAULT 'DRAFT',
                    `ScheduledAt` DATETIME NULL,
                    `StartedAt` DATETIME NULL,
                    `CompletedAt` DATETIME NULL,
                    `CancelledAt` DATETIME NULL,
                    `CancellationReason` TEXT NULL,

                    -- Description & Instructions
                    `Description` TEXT NULL,
                    `SpecialInstructions` TEXT NULL,
                    `EquipmentNeeded` TEXT NULL,
                    `EmergencyContacts` TEXT NULL,
                    `SiteAccessInfo` TEXT NULL,

                    -- Audit
                    `CreatedAt` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    `UpdatedAt` DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
                    `CreatedBy` CHAR(36) NULL,
                    `UpdatedBy` CHAR(36) NULL,
                    `IsDeleted` BOOLEAN NOT NULL DEFAULT FALSE,
                    `DeletedAt` DATETIME NULL,
                    `DeletedBy` CHAR(36) NULL,
                    `Version` INT NOT NULL DEFAULT 1,

                    INDEX `idx_shifts_date` (`ShiftDate`, `Status`, `IsDeleted`),
                    INDEX `idx_shifts_location` (`LocationId`, `ShiftDate`),
                    INDEX `idx_shifts_manager` (`ManagerId`),
                    INDEX `idx_shifts_template` (`ShiftTemplateId`),
                    INDEX `idx_shifts_contract` (`ContractId`),
                    INDEX `idx_shifts_status` (`Status`),
                    INDEX `idx_shifts_year_month` (`ShiftYear`, `ShiftMonth`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            ");

            // ============================================================================
            // 8. SHIFT_ASSIGNMENTS TABLE
            // ============================================================================
            await connection.ExecuteAsync(@"
                CREATE TABLE IF NOT EXISTS `shift_assignments` (
                    `Id` CHAR(36) PRIMARY KEY,
                    `ShiftId` CHAR(36) NOT NULL,
                    `TeamId` CHAR(36) NULL,
                    `GuardId` CHAR(36) NOT NULL,
                    `AssignmentType` VARCHAR(50) NOT NULL DEFAULT 'REGULAR',

                    -- Replacement info
                    `ReplacedGuardId` CHAR(36) NULL,
                    `ReplacementReason` TEXT NULL,
                    `IsReplacement` BOOLEAN NOT NULL DEFAULT FALSE,

                    -- Status lifecycle
                    `Status` VARCHAR(50) NOT NULL DEFAULT 'ASSIGNED',
                    `AssignedAt` DATETIME NOT NULL,
                    `ConfirmedAt` DATETIME NULL,
                    `DeclinedAt` DATETIME NULL,
                    `CheckedInAt` DATETIME NULL,
                    `CheckedOutAt` DATETIME NULL,
                    `CompletedAt` DATETIME NULL,
                    `CancelledAt` DATETIME NULL,

                    -- Reasons
                    `DeclineReason` TEXT NULL,
                    `CancellationReason` TEXT NULL,

                    -- Attendance link
                    `AttendanceRecordId` CHAR(36) NULL,
                    `AttendanceSynced` BOOLEAN NOT NULL DEFAULT FALSE,

                    -- Notifications
                    `NotificationSent` BOOLEAN NOT NULL DEFAULT FALSE,
                    `NotificationSentAt` DATETIME NULL,
                    `NotificationMethod` VARCHAR(50) NULL,

                    -- Reminders
                    `Reminder24HSent` BOOLEAN NOT NULL DEFAULT FALSE,
                    `Reminder24HSentAt` DATETIME NULL,
                    `Reminder2HSent` BOOLEAN NOT NULL DEFAULT FALSE,
                    `Reminder2HSentAt` DATETIME NULL,

                    -- Performance tracking
                    `PunctualityScore` DECIMAL(3,2) NULL,
                    `PerformanceNote` TEXT NULL,
                    `RatedBy` CHAR(36) NULL,
                    `RatedAt` DATETIME NULL,

                    -- Notes
                    `AssignmentNotes` TEXT NULL,
                    `GuardNotes` TEXT NULL,

                    -- Audit
                    `AssignedBy` CHAR(36) NOT NULL,
                    `UpdatedAt` DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
                    `IsDeleted` BOOLEAN NOT NULL DEFAULT FALSE,
                    `DeletedAt` DATETIME NULL,
                    `DeletedBy` CHAR(36) NULL,

                    INDEX `idx_assignments_shift` (`ShiftId`, `Status`),
                    INDEX `idx_assignments_guard` (`GuardId`, `Status`),
                    INDEX `idx_assignments_team` (`TeamId`),
                    INDEX `idx_assignments_status` (`Status`),
                    INDEX `idx_assignments_assigned_by` (`AssignedBy`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            ");

            // ============================================================================
            // 9. SHIFT_CONFLICTS TABLE
            // ============================================================================
            await connection.ExecuteAsync(@"
                CREATE TABLE IF NOT EXISTS `shift_conflicts` (
                    `Id` CHAR(36) PRIMARY KEY,
                    `ConflictType` VARCHAR(100) NOT NULL,
                    `Severity` VARCHAR(50) NOT NULL,
                    `GuardId` CHAR(36) NOT NULL,
                    `ShiftId1` CHAR(36) NOT NULL,
                    `ShiftId2` CHAR(36) NULL,
                    `ShiftAssignmentId` CHAR(36) NULL,
                    `Description` TEXT NOT NULL,
                    `DetectedAt` DATETIME NOT NULL,
                    `Status` VARCHAR(50) NOT NULL DEFAULT 'OPEN',
                    `ResolvedAt` DATETIME NULL,
                    `ResolvedBy` CHAR(36) NULL,
                    `ResolutionNotes` TEXT NULL,
                    `AutoResolvable` BOOLEAN NOT NULL DEFAULT FALSE,
                    `SuggestedAction` TEXT NULL,
                    `CreatedAt` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    `UpdatedAt` DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
                    `IsDeleted` BOOLEAN NOT NULL DEFAULT FALSE,

                    INDEX `idx_conflicts_guard` (`GuardId`, `Status`),
                    INDEX `idx_conflicts_shift1` (`ShiftId1`),
                    INDEX `idx_conflicts_status` (`Status`),
                    INDEX `idx_conflicts_severity` (`Severity`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            ");

            _tablesCreated = true;
            Console.WriteLine("✓ All tables created successfully");
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
