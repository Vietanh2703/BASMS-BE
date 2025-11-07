using MySql.Data.MySqlClient;

namespace Shifts.API.Data;

/// <summary>
///     MySQL connection factory for Shifts service
///     Creates all 12 tables based on ERD: managers, guards, teams, shifts, etc.
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

            // ====================================================================
            // STEP 1: TẠO DATABASE NẾU CHƯA TỒN TẠI
            // ====================================================================
            // Kết nối không chỉ định database để tạo database mới
            var connectionStringBuilder = new MySqlConnectionStringBuilder(_connectionString);
            var databaseName = connectionStringBuilder.Database;
            connectionStringBuilder.Database = null; // Kết nối không chỉ định DB

            using (var tempConnection = new MySqlConnection(connectionStringBuilder.ConnectionString))
            {
                await tempConnection.OpenAsync();

                // Tạo database nếu chưa tồn tại
                await tempConnection.ExecuteAsync($@"
                    CREATE DATABASE IF NOT EXISTS `{databaseName}`
                    CHARACTER SET utf8mb4
                    COLLATE utf8mb4_unicode_ci;
                ");

                Console.WriteLine($"✓ Database '{databaseName}' ready");
            }

            // ====================================================================
            // STEP 2: KẾT NỐI ĐẾN DATABASE VỪA TẠO VÀ CHECK/CREATE TABLES
            // ====================================================================
            using var connection = await CreateConnectionAsync();

            // ========================================================================
            // 1. MANAGERS TABLE - Cache managers từ User Service
            // ========================================================================
            var managersTableExists = await connection.ExecuteScalarAsync<bool>(
                "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'managers'");
        
            if (!managersTableExists)
            {
                await connection.ExecuteAsync(@"
                    CREATE TABLE IF NOT EXISTS `managers` (
                        `Id` CHAR(36) PRIMARY KEY COMMENT 'Trùng với User Service user_id',

                        -- Thông tin cơ bản
                        `EmployeeCode` VARCHAR(50) UNIQUE NOT NULL COMMENT 'Mã NV: MGR001',
                        `FullName` VARCHAR(200) NOT NULL COMMENT 'Họ tên đầy đủ',
                        `Email` VARCHAR(255) UNIQUE NOT NULL COMMENT 'Email đăng nhập',
                        `PhoneNumber` VARCHAR(20) NULL COMMENT 'SĐT liên hệ',

                        -- Vai trò & chức vụ
                        `Role` VARCHAR(50) NOT NULL DEFAULT 'MANAGER' COMMENT 'MANAGER | DIRECTOR | SUPERVISOR',
                        `Position` VARCHAR(100) NULL COMMENT 'Chức danh',
                        `Department` VARCHAR(100) NULL COMMENT 'Phòng ban',

                        -- Cấp bậc quản lý
                        `ManagerLevel` INT NOT NULL DEFAULT 1 COMMENT '1=Line | 2=Senior | 3=Director',
                        `ReportsToManagerId` CHAR(36) NULL COMMENT 'Manager cấp trên',

                        -- Tình trạng
                        `EmploymentStatus` VARCHAR(50) NOT NULL DEFAULT 'ACTIVE' COMMENT 'ACTIVE | ON_LEAVE | SUSPENDED | TERMINATED',

                        -- Phân quyền
                        `CanCreateShifts` BOOLEAN NOT NULL DEFAULT TRUE,
                        `CanApproveShifts` BOOLEAN NOT NULL DEFAULT TRUE,
                        `CanAssignGuards` BOOLEAN NOT NULL DEFAULT TRUE,
                        `CanApproveOvertime` BOOLEAN NOT NULL DEFAULT TRUE,
                        `CanManageTeams` BOOLEAN NOT NULL DEFAULT TRUE,
                        `MaxTeamSize` INT NULL,

                        -- Thống kê
                        `TotalTeamsManaged` INT NOT NULL DEFAULT 0,
                        `TotalGuardsSupervised` INT NOT NULL DEFAULT 0,
                        `TotalShiftsCreated` INT NOT NULL DEFAULT 0,

                        `AvatarUrl` TEXT NULL,

                        -- Sync metadata
                        `LastSyncedAt` DATETIME NULL,
                        `SyncStatus` VARCHAR(50) NOT NULL DEFAULT 'SYNCED' COMMENT 'SYNCED | PENDING | FAILED',
                        `UserServiceVersion` INT NULL,

                        `IsActive` BOOLEAN NOT NULL DEFAULT TRUE,

                        -- Audit
                        `CreatedAt` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        `UpdatedAt` DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
                        `IsDeleted` BOOLEAN NOT NULL DEFAULT FALSE,
                        `DeletedAt` DATETIME NULL,

                        -- Indexes
                        INDEX `idx_managers_active` (`IsActive`),
                        INDEX `idx_managers_code` (`EmployeeCode`),
                        INDEX `idx_managers_email` (`Email`),
                        INDEX `idx_managers_status` (`EmploymentStatus`),
                        INDEX `idx_managers_hierarchy` (`ReportsToManagerId`)
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
                    COMMENT='Cache managers từ User Service';
                ");
            }

            // ========================================================================
            // 2. GUARDS TABLE - Cache guards từ User Service
            // ========================================================================
            var guardsTableExists = await connection.ExecuteScalarAsync<bool>(
                "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'guards'");
            
            if (!guardsTableExists)
            {
                await connection.ExecuteAsync(@"
                    CREATE TABLE IF NOT EXISTS `guards` (
                        `Id` CHAR(36) PRIMARY KEY COMMENT 'Trùng với User Service user_id',

                        -- Thông tin cơ bản
                        `EmployeeCode` VARCHAR(50) UNIQUE NOT NULL COMMENT 'Mã NV: GRD001',
                        `FullName` VARCHAR(200) NOT NULL,
                        `Email` VARCHAR(255) NULL,
                        `PhoneNumber` VARCHAR(20) NOT NULL COMMENT 'SĐT bắt buộc',

                        -- Thông tin cá nhân
                        `DateOfBirth` DATE NULL,
                        `Gender` VARCHAR(10) NULL COMMENT 'MALE | FEMALE',
                        `NationalId` VARCHAR(50) UNIQUE NULL COMMENT 'CCCD/CMND',
                        `CurrentAddress` TEXT NULL,

                        -- Tuyển dụng
                        `EmploymentStatus` VARCHAR(50) NOT NULL DEFAULT 'ACTIVE' COMMENT 'ACTIVE | PROBATION | ON_LEAVE | SUSPENDED | TERMINATED',
                        `HireDate` DATE NOT NULL,
                        `ProbationEndDate` DATE NULL,
                        `ContractType` VARCHAR(50) NULL COMMENT 'FULL_TIME | PART_TIME | CONTRACT | SEASONAL',
                        `TerminationDate` DATE NULL,
                        `TerminationReason` TEXT NULL,

                        -- Quản lý
                        `DirectManagerId` CHAR(36) NULL,

                        -- Sở thích làm việc
                        `PreferredShiftType` VARCHAR(50) NULL COMMENT 'DAY | NIGHT | ROTATING | FLEXIBLE',
                        `PreferredLocations` TEXT NULL COMMENT 'JSON array',
                        `MaxWeeklyHours` INT NOT NULL DEFAULT 48,
                        `CanWorkOvertime` BOOLEAN NOT NULL DEFAULT TRUE,
                        `CanWorkWeekends` BOOLEAN NOT NULL DEFAULT TRUE,
                        `CanWorkHolidays` BOOLEAN NOT NULL DEFAULT TRUE,

                        -- Performance metrics
                        `TotalShiftsWorked` INT NOT NULL DEFAULT 0,
                        `TotalHoursWorked` DECIMAL(10,2) NOT NULL DEFAULT 0,
                        `AttendanceRate` DECIMAL(5,2) NULL COMMENT 'Tỷ lệ đi làm %',
                        `PunctualityRate` DECIMAL(5,2) NULL COMMENT 'Tỷ lệ đúng giờ %',
                        `NoShowCount` INT NOT NULL DEFAULT 0,
                        `ViolationCount` INT NOT NULL DEFAULT 0,
                        `CommendationCount` INT NOT NULL DEFAULT 0,

                        -- Trạng thái realtime
                        `CurrentAvailability` VARCHAR(50) NOT NULL DEFAULT 'AVAILABLE' COMMENT 'AVAILABLE | ON_SHIFT | ON_LEAVE | UNAVAILABLE',
                        `AvailabilityNotes` TEXT NULL,

                        -- App & Biometric
                        `BiometricRegistered` BOOLEAN NOT NULL DEFAULT FALSE,
                        `FaceTemplateUrl` TEXT NULL,
                        `LastAppLogin` DATETIME NULL,
                        `DeviceTokens` TEXT NULL COMMENT 'JSON array',

                        `AvatarUrl` TEXT NULL,

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

                        -- Indexes
                        INDEX `idx_guards_active` (`IsActive`),
                        INDEX `idx_guards_code` (`EmployeeCode`),
                        INDEX `idx_guards_phone` (`PhoneNumber`),
                        INDEX `idx_guards_national_id` (`NationalId`),
                        INDEX `idx_guards_status` (`EmploymentStatus`),
                        INDEX `idx_guards_manager` (`DirectManagerId`),
                        INDEX `idx_guards_availability` (`CurrentAvailability`, `IsActive`),
                        INDEX `idx_guards_email` (`Email`),

                        FOREIGN KEY (`DirectManagerId`) REFERENCES `managers`(`Id`) ON DELETE SET NULL
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
                    COMMENT='Cache guards từ User Service';
                ");
            }

            // ========================================================================
            // 3. USER_SYNC_LOG TABLE - Audit trail sync
            // ========================================================================
            var userSyncLogTableExists = await connection.ExecuteScalarAsync<bool>(
                "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'user_sync_log'");
            
            if (!userSyncLogTableExists)
            {
                await connection.ExecuteAsync(@"
                    CREATE TABLE IF NOT EXISTS `user_sync_log` (
                         `Id` CHAR(36) PRIMARY KEY,
                         `UserId` CHAR(36) NOT NULL COMMENT 'Manager/Guard ID',
                         `UserType` VARCHAR(50) NOT NULL COMMENT 'MANAGER | GUARD',
                         `SyncType` VARCHAR(50) NOT NULL COMMENT 'CREATE | UPDATE | DELETE | FULL_SYNC',
                         `SyncStatus` VARCHAR(50) NOT NULL COMMENT 'SUCCESS | FAILED | PARTIAL',
                         `FieldsChanged` TEXT NULL COMMENT 'JSON array',
                         `OldValues` TEXT NULL COMMENT 'JSON object',
                         `NewValues` TEXT NULL COMMENT 'JSON object',
                         `SyncInitiatedBy` VARCHAR(50) NULL COMMENT 'WEBHOOK | SCHEDULED_JOB | MANUAL | API_CALL',
                         `UserServiceVersionBefore` INT NULL,
                         `UserServiceVersionAfter` INT NULL,
                         `ErrorMessage` TEXT NULL,
                         `ErrorCode` VARCHAR(50) NULL COMMENT 'USER_NOT_FOUND | NETWORK_TIMEOUT | VALIDATION_ERROR',
                         `RetryCount` INT NOT NULL DEFAULT 0,
                         `SyncStartedAt` DATETIME NOT NULL,
                         `SyncCompletedAt` DATETIME NULL,
                         `SyncDurationMs` INT NULL COMMENT 'Thời gian sync (ms)',
                         `CreatedAt` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    INDEX `idx_sync_log_user` (`UserId`, `SyncType`),
                    INDEX `idx_sync_log_status` (`SyncStatus`, `CreatedAt`),
                    INDEX `idx_sync_log_date` (`CreatedAt`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
            COMMENT='Audit trail sync - debug, monitor, compliance';
            ");
            }

            // ============================================================================
            // 4. TEAMS TABLE - Đội nhóm bảo vệ
            // ============================================================================
            var teamsTableExists = await connection.ExecuteScalarAsync<bool>(
                "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'teams'");
            
            if (!teamsTableExists)
            {
                await connection.ExecuteAsync(@"
                    CREATE TABLE IF NOT EXISTS `teams` (
                        `Id` CHAR(36) PRIMARY KEY,
                        `ManagerId` CHAR(36) NOT NULL,
                        `TeamCode` VARCHAR(50) UNIQUE NOT NULL COMMENT 'TEAM-A, COMMERCIAL-DAY-01',
                        `TeamName` VARCHAR(200) NOT NULL,
                        `Description` TEXT NULL,
                        `MinMembers` INT NOT NULL DEFAULT 1,
                        `MaxMembers` INT NULL,
                        `CurrentMemberCount` INT NOT NULL DEFAULT 0,
                        `Specialization` VARCHAR(100) NULL COMMENT 'RESIDENTIAL | COMMERCIAL | EVENT | VIP | INDUSTRIAL',
                        `IsActive` BOOLEAN NOT NULL DEFAULT TRUE,
                        `CreatedAt` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        `UpdatedAt` DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
                        `CreatedBy` CHAR(36) NULL,
                        `UpdatedBy` CHAR(36) NULL,
                        `IsDeleted` BOOLEAN NOT NULL DEFAULT FALSE,
                        `DeletedAt` DATETIME NULL,
                        `DeletedBy` CHAR(36) NULL,
                        INDEX `idx_teams_active` (`IsActive`),
                        INDEX `idx_teams_manager` (`ManagerId`),
                        INDEX `idx_teams_code` (`TeamCode`),
                        FOREIGN KEY (`ManagerId`) REFERENCES `managers`(`Id`) ON DELETE RESTRICT
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
                    COMMENT='Đội nhóm bảo vệ - quản lý team và phân công';
                ");
            }

            // ============================================================================
            // 5. TEAM_MEMBERS TABLE - Thành viên team (Many-to-Many)
            // ============================================================================
            var teamMembersTableExists = await connection.ExecuteScalarAsync<bool>(
                "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'team_members'");
            
            if (!teamMembersTableExists)
            {
                await connection.ExecuteAsync(@"
                    CREATE TABLE IF NOT EXISTS `team_members` (
                        `Id` CHAR(36) PRIMARY KEY,
                        `TeamId` CHAR(36) NOT NULL,
                        `GuardId` CHAR(36) NOT NULL,
                        `Role` VARCHAR(50) NOT NULL DEFAULT 'MEMBER' COMMENT 'LEADER | DEPUTY | MEMBER',
                        `IsActive` BOOLEAN NOT NULL DEFAULT TRUE,
                        `PerformanceRating` DECIMAL(3,2) NULL COMMENT '1.00-5.00',
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
                        INDEX `idx_team_members_guard` (`GuardId`, `IsActive`),
                        UNIQUE INDEX `uk_team_guard_active` (`TeamId`, `GuardId`, `IsActive`),
                        FOREIGN KEY (`TeamId`) REFERENCES `teams`(`Id`) ON DELETE CASCADE,
                        FOREIGN KEY (`GuardId`) REFERENCES `guards`(`Id`) ON DELETE CASCADE
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
                    COMMENT='Thành viên team - quan hệ Many-to-Many giữa teams và guards';
                ");
            }

            // ============================================================================
            // 6. SHIFT_TEMPLATES TABLE - Mẫu ca trực
            // ============================================================================
            var shiftTemplatesTableExists = await connection.ExecuteScalarAsync<bool>(
                "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'shift_templates'");
            
            if (!shiftTemplatesTableExists)
            {
                await connection.ExecuteAsync(@"
                    CREATE TABLE IF NOT EXISTS `shift_templates` (
                        `Id` CHAR(36) PRIMARY KEY,
                        `TemplateCode` VARCHAR(50) UNIQUE NOT NULL COMMENT 'MORNING-8H, NIGHT-12H',
                        `TemplateName` VARCHAR(200) NOT NULL,
                        `Description` TEXT NULL,
                        `StartTime` TIME NOT NULL COMMENT '08:00:00',
                        `EndTime` TIME NOT NULL COMMENT '17:00:00',
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
                        `IsActive` BOOLEAN NOT NULL DEFAULT TRUE,
                        `EffectiveFrom` DATE NULL,
                        `EffectiveTo` DATE NULL,
                        `CreatedAt` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        `UpdatedAt` DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
                        `CreatedBy` CHAR(36) NULL,
                        `UpdatedBy` CHAR(36) NULL,
                        `IsDeleted` BOOLEAN NOT NULL DEFAULT FALSE,
                        INDEX `idx_shift_templates_active` (`IsActive`),
                        INDEX `idx_shift_templates_code` (`TemplateCode`)
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
                    COMMENT='Mẫu ca trực - định nghĩa các loại ca làm việc';
                ");
            }

            // ============================================================================
            // 7. SHIFTS TABLE - Ca trực thực tế
            // ============================================================================
            var shiftsTableExists = await connection.ExecuteScalarAsync<bool>(
                "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'shifts'");
            
            if (!shiftsTableExists)
            {
                await connection.ExecuteAsync(@"
                    CREATE TABLE IF NOT EXISTS `shifts` (
                        `Id` CHAR(36) PRIMARY KEY,
                        
                        -- Liên kết
                        `TemplateId` CHAR(36) NULL COMMENT 'Tham chiếu shift template',
                        `TeamId` CHAR(36) NULL COMMENT 'Team được giao ca này',
                        `ManagerId` CHAR(36) NOT NULL COMMENT 'Manager tạo/quản lý ca',
                        
                        -- Thông tin ca
                        `ShiftCode` VARCHAR(50) UNIQUE NOT NULL COMMENT 'SH-2024-001',
                        `ShiftName` VARCHAR(200) NOT NULL,
                        `Description` TEXT NULL,
                        
                        -- Thời gian
                        `ShiftDate` DATE NOT NULL COMMENT 'Ngày làm việc',
                        `StartTime` TIME NOT NULL,
                        `EndTime` TIME NOT NULL,
                        `PlannedDurationHours` DECIMAL(4,2) NOT NULL,
                        `ActualStartTime` DATETIME NULL COMMENT 'Thời gian check-in thực tế',
                        `ActualEndTime` DATETIME NULL COMMENT 'Thời gian check-out thực tế',
                        `ActualDurationHours` DECIMAL(4,2) NULL,
                        
                        -- Phân loại
                        `ShiftType` VARCHAR(50) NOT NULL DEFAULT 'REGULAR' COMMENT 'REGULAR | OVERTIME | EMERGENCY | REPLACEMENT | TRAINING',
                        `IsNightShift` BOOLEAN NOT NULL DEFAULT FALSE,
                        `IsWeekend` BOOLEAN NOT NULL DEFAULT FALSE,
                        `IsHoliday` BOOLEAN NOT NULL DEFAULT FALSE,
                        `HolidayName` VARCHAR(200) NULL,
                        
                        -- Yêu cầu nhân sự
                        `RequiredGuards` INT NOT NULL DEFAULT 1,
                        `AssignedGuards` INT NOT NULL DEFAULT 0,
                        `ConfirmedGuards` INT NOT NULL DEFAULT 0,
                        `CheckedInGuards` INT NOT NULL DEFAULT 0,
                        
                        -- Trạng thái
                        `Status` VARCHAR(50) NOT NULL DEFAULT 'DRAFT' COMMENT 'DRAFT | PUBLISHED | ASSIGNED | IN_PROGRESS | COMPLETED | CANCELLED',
                        `PublishedAt` DATETIME NULL,
                        `PublishedBy` CHAR(36) NULL,
                        
                        -- Phê duyệt
                        `RequiresApproval` BOOLEAN NOT NULL DEFAULT FALSE,
                        `ApprovalStatus` VARCHAR(50) NULL COMMENT 'PENDING | APPROVED | REJECTED',
                        `ApprovedBy` CHAR(36) NULL,
                        `ApprovedAt` DATETIME NULL,
                        `RejectionReason` TEXT NULL,
                        
                        -- Ghi chú & ưu tiên
                        `Priority` VARCHAR(50) NOT NULL DEFAULT 'NORMAL' COMMENT 'LOW | NORMAL | HIGH | URGENT',
                        `SpecialInstructions` TEXT NULL,
                        `ManagerNotes` TEXT NULL,
                        
                        -- Audit
                        `CreatedAt` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        `UpdatedAt` DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
                        `CreatedBy` CHAR(36) NULL,
                        `UpdatedBy` CHAR(36) NULL,
                        `IsDeleted` BOOLEAN NOT NULL DEFAULT FALSE,
                        `DeletedAt` DATETIME NULL,
                        `DeletedBy` CHAR(36) NULL,
                        
                        -- Indexes
                        INDEX `idx_shifts_date` (`ShiftDate`, `Status`),
                        INDEX `idx_shifts_status` (`Status`),
                        INDEX `idx_shifts_manager` (`ManagerId`),
                        INDEX `idx_shifts_team` (`TeamId`),
                        INDEX `idx_shifts_template` (`TemplateId`),
                        INDEX `idx_shifts_code` (`ShiftCode`),
                        INDEX `idx_shifts_type` (`ShiftType`, `ShiftDate`),
                        
                        FOREIGN KEY (`TemplateId`) REFERENCES `shift_templates`(`Id`) ON DELETE SET NULL,
                        FOREIGN KEY (`TeamId`) REFERENCES `teams`(`Id`) ON DELETE SET NULL,
                        FOREIGN KEY (`ManagerId`) REFERENCES `managers`(`Id`) ON DELETE RESTRICT
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
                    COMMENT='Ca trực thực tế - lịch làm việc cụ thể cho từng ngày';
                ");
            }

            // ============================================================================
            // 8. SHIFT_ASSIGNMENTS TABLE - Phân công bảo vệ vào ca
            // ============================================================================
            var shiftAssignmentsTableExists = await connection.ExecuteScalarAsync<bool>(
                "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'shift_assignments'");
            
            if (!shiftAssignmentsTableExists)
            {
                await connection.ExecuteAsync(@"
                    CREATE TABLE IF NOT EXISTS `shift_assignments` (
                        `Id` CHAR(36) PRIMARY KEY,
                        
                        -- Liên kết
                        `ShiftId` CHAR(36) NOT NULL,
                        `GuardId` CHAR(36) NOT NULL,
                        `AssignedBy` CHAR(36) NOT NULL COMMENT 'Manager phân công',
                        
                        -- Vai trò trong ca
                        `Role` VARCHAR(50) NOT NULL DEFAULT 'GUARD' COMMENT 'LEADER | SUPERVISOR | GUARD | BACKUP',
                        `Position` VARCHAR(100) NULL COMMENT 'Vị trí cụ thể: Gate 1, Floor 3',
                        
                        -- Trạng thái
                        `Status` VARCHAR(50) NOT NULL DEFAULT 'PENDING' COMMENT 'PENDING | CONFIRMED | REJECTED | CHECKED_IN | CHECKED_OUT | COMPLETED | NO_SHOW | CANCELLED',
                        
                        -- Xác nhận
                        `ConfirmedAt` DATETIME NULL COMMENT 'Guard xác nhận nhận ca',
                        `RejectedAt` DATETIME NULL,
                        `RejectionReason` TEXT NULL,
                        
                        -- Check-in/out
                        `CheckInTime` DATETIME NULL,
                        `CheckInLocation` VARCHAR(200) NULL COMMENT 'GPS coordinates',
                        `CheckInMethod` VARCHAR(50) NULL COMMENT 'QR_CODE | BIOMETRIC | MANUAL | GPS',
                        `CheckInPhotoUrl` TEXT NULL,
                        `CheckInNotes` TEXT NULL,
                        
                        `CheckOutTime` DATETIME NULL,
                        `CheckOutLocation` VARCHAR(200) NULL,
                        `CheckOutMethod` VARCHAR(50) NULL,
                        `CheckOutPhotoUrl` TEXT NULL,
                        `CheckOutNotes` TEXT NULL,
                        
                        -- Thời gian làm việc
                        `PlannedStartTime` DATETIME NOT NULL,
                        `PlannedEndTime` DATETIME NOT NULL,
                        `ActualStartTime` DATETIME NULL,
                        `ActualEndTime` DATETIME NULL,
                        `TotalHoursWorked` DECIMAL(5,2) NULL,
                        
                        -- Đánh giá
                        `PerformanceRating` DECIMAL(3,2) NULL COMMENT '1.00-5.00',
                        `ManagerFeedback` TEXT NULL,
                        `GuardFeedback` TEXT NULL,
                        
                        -- Vấn đề
                        `IsLate` BOOLEAN NOT NULL DEFAULT FALSE,
                        `LateMinutes` INT NULL,
                        `IsEarlyLeave` BOOLEAN NOT NULL DEFAULT FALSE,
                        `EarlyLeaveMinutes` INT NULL,
                        `HasViolations` BOOLEAN NOT NULL DEFAULT FALSE,
                        `ViolationCount` INT NOT NULL DEFAULT 0,
                        
                        -- Thông báo
                        `NotificationSent` BOOLEAN NOT NULL DEFAULT FALSE,
                        `NotificationSentAt` DATETIME NULL,
                        `ReminderSent` BOOLEAN NOT NULL DEFAULT FALSE,
                        `ReminderSentAt` DATETIME NULL,
                        
                        -- Audit
                        `CreatedAt` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        `UpdatedAt` DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
                        `IsDeleted` BOOLEAN NOT NULL DEFAULT FALSE,
                        `DeletedAt` DATETIME NULL,
                        
                        -- Indexes
                        INDEX `idx_assignments_shift` (`ShiftId`, `Status`),
                        INDEX `idx_assignments_guard` (`GuardId`, `Status`),
                        INDEX `idx_assignments_status` (`Status`),
                        INDEX `idx_assignments_date` (`PlannedStartTime`),
                        UNIQUE INDEX `uk_shift_guard` (`ShiftId`, `GuardId`, `IsDeleted`),
                        
                        FOREIGN KEY (`ShiftId`) REFERENCES `shifts`(`Id`) ON DELETE CASCADE,
                        FOREIGN KEY (`GuardId`) REFERENCES `guards`(`Id`) ON DELETE CASCADE,
                        FOREIGN KEY (`AssignedBy`) REFERENCES `managers`(`Id`) ON DELETE RESTRICT
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
                    COMMENT='Phân công bảo vệ vào ca - quan hệ Many-to-Many giữa shifts và guards';
                ");
            }

            // ============================================================================
            // 9. SHIFT_SWAPS TABLE - Đổi ca giữa các bảo vệ
            // ============================================================================
            var shiftSwapsTableExists = await connection.ExecuteScalarAsync<bool>(
                "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'shift_swaps'");
            
            if (!shiftSwapsTableExists)
            {
                await connection.ExecuteAsync(@"
                    CREATE TABLE IF NOT EXISTS `shift_swaps` (
                        `Id` CHAR(36) PRIMARY KEY,
                        
                        -- Yêu cầu đổi ca
                        `RequestingGuardId` CHAR(36) NOT NULL COMMENT 'Guard muốn đổi ca',
                        `RequestingAssignmentId` CHAR(36) NOT NULL COMMENT 'Ca muốn đổi đi',
                        
                        `TargetGuardId` CHAR(36) NULL COMMENT 'Guard được đề nghị đổi',
                        `TargetAssignmentId` CHAR(36) NULL COMMENT 'Ca muốn nhận',
                        
                        -- Loại đổi ca
                        `SwapType` VARCHAR(50) NOT NULL DEFAULT 'ONE_TO_ONE' COMMENT 'ONE_TO_ONE | GIVE_AWAY | FIND_REPLACEMENT',
                        
                        -- Lý do
                        `Reason` TEXT NOT NULL COMMENT 'Lý do đổi ca',
                        `RequestNotes` TEXT NULL,
                        
                        -- Trạng thái
                        `Status` VARCHAR(50) NOT NULL DEFAULT 'PENDING' COMMENT 'PENDING | TARGET_ACCEPTED | TARGET_REJECTED | MANAGER_APPROVED | MANAGER_REJECTED | COMPLETED | CANCELLED',
                        
                        -- Phê duyệt từ target guard
                        `TargetAcceptedAt` DATETIME NULL,
                        `TargetRejectedAt` DATETIME NULL,
                        `TargetRejectionReason` TEXT NULL,
                        
                        -- Phê duyệt từ manager
                        `ReviewedBy` CHAR(36) NULL COMMENT 'Manager phê duyệt',
                        `ReviewedAt` DATETIME NULL,
                        `ApprovalStatus` VARCHAR(50) NULL COMMENT 'APPROVED | REJECTED',
                        `RejectionReason` TEXT NULL,
                        `ManagerNotes` TEXT NULL,
                        
                        -- Thực hiện
                        `CompletedAt` DATETIME NULL,
                        `CancelledAt` DATETIME NULL,
                        `CancelledBy` CHAR(36) NULL,
                        `CancellationReason` TEXT NULL,
                        
                        -- Thời hạn
                        `ExpiresAt` DATETIME NULL COMMENT 'Hết hạn nếu không được chấp nhận',
                        
                        -- Audit
                        `CreatedAt` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        `UpdatedAt` DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
                        `IsDeleted` BOOLEAN NOT NULL DEFAULT FALSE,
                        
                        -- Indexes
                        INDEX `idx_swaps_requesting_guard` (`RequestingGuardId`, `Status`),
                        INDEX `idx_swaps_target_guard` (`TargetGuardId`, `Status`),
                        INDEX `idx_swaps_status` (`Status`, `CreatedAt`),
                        INDEX `idx_swaps_assignment` (`RequestingAssignmentId`),
                        
                        FOREIGN KEY (`RequestingGuardId`) REFERENCES `guards`(`Id`) ON DELETE CASCADE,
                        FOREIGN KEY (`TargetGuardId`) REFERENCES `guards`(`Id`) ON DELETE CASCADE,
                        FOREIGN KEY (`RequestingAssignmentId`) REFERENCES `shift_assignments`(`Id`) ON DELETE CASCADE,
                        FOREIGN KEY (`TargetAssignmentId`) REFERENCES `shift_assignments`(`Id`) ON DELETE SET NULL,
                        FOREIGN KEY (`ReviewedBy`) REFERENCES `managers`(`Id`) ON DELETE SET NULL
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
                    COMMENT='Đổi ca giữa các bảo vệ - quản lý yêu cầu swap shifts';
                ");
            }

            // ============================================================================
            // 10. ATTENDANCE_RECORDS TABLE - Bản ghi chấm công chi tiết
            // ============================================================================
            var attendanceRecordsTableExists = await connection.ExecuteScalarAsync<bool>(
                "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'attendance_records'");
            
            if (!attendanceRecordsTableExists)
            {
                await connection.ExecuteAsync(@"
                    CREATE TABLE IF NOT EXISTS `attendance_records` (
                        `Id` CHAR(36) PRIMARY KEY,
                        
                        -- Liên kết
                        `AssignmentId` CHAR(36) NOT NULL,
                        `GuardId` CHAR(36) NOT NULL,
                        `ShiftId` CHAR(36) NOT NULL,
                        
                        -- Thông tin ca
                        `AttendanceDate` DATE NOT NULL,
                        `PlannedCheckIn` DATETIME NOT NULL,
                        `PlannedCheckOut` DATETIME NOT NULL,
                        
                        -- Check-in
                        `ActualCheckIn` DATETIME NULL,
                        `CheckInStatus` VARCHAR(50) NULL COMMENT 'ON_TIME | LATE | VERY_LATE | ABSENT',
                        `CheckInMethod` VARCHAR(50) NULL COMMENT 'BIOMETRIC | QR_CODE | MANUAL | GPS',
                        `CheckInLocation` VARCHAR(200) NULL,
                        `CheckInLatitude` DECIMAL(10,8) NULL,
                        `CheckInLongitude` DECIMAL(11,8) NULL,
                        `CheckInPhotoUrl` TEXT NULL,
                        `CheckInDeviceId` VARCHAR(100) NULL,
                        `CheckInIpAddress` VARCHAR(50) NULL,
                        `LateMinutes` INT NULL,
                        
                        -- Check-out
                        `ActualCheckOut` DATETIME NULL,
                        `CheckOutStatus` VARCHAR(50) NULL COMMENT 'ON_TIME | EARLY | VERY_EARLY | NO_CHECKOUT',
                        `CheckOutMethod` VARCHAR(50) NULL,
                        `CheckOutLocation` VARCHAR(200) NULL,
                        `CheckOutLatitude` DECIMAL(10,8) NULL,
                        `CheckOutLongitude` DECIMAL(11,8) NULL,
                        `CheckOutPhotoUrl` TEXT NULL,
                        `CheckOutDeviceId` VARCHAR(100) NULL,
                        `CheckOutIpAddress` VARCHAR(50) NULL,
                        `EarlyLeaveMinutes` INT NULL,
                        
                        -- Thời gian làm việc
                        `TotalHoursWorked` DECIMAL(5,2) NULL,
                        `RegularHours` DECIMAL(5,2) NULL,
                        `OvertimeHours` DECIMAL(5,2) NULL,
                        `BreakDurationMinutes` INT NULL,
                        `EffectiveWorkHours` DECIMAL(5,2) NULL,
                        
                        -- Trạng thái tổng quan
                        `AttendanceStatus` VARCHAR(50) NOT NULL DEFAULT 'PENDING' COMMENT 'PRESENT | LATE | ABSENT | HALF_DAY | ON_LEAVE | NO_SHOW',
                        
                        -- Vấn đề
                        `HasIssues` BOOLEAN NOT NULL DEFAULT FALSE,
                        `IssueType` VARCHAR(100) NULL COMMENT 'LATE | EARLY_LEAVE | NO_CHECKOUT | LOCATION_MISMATCH | SUSPICIOUS',
                        `IssueDescription` TEXT NULL,
                        
                        -- Giải trình & phê duyệt
                        `RequiresJustification` BOOLEAN NOT NULL DEFAULT FALSE,
                        `GuardJustification` TEXT NULL,
                        `JustificationProofUrl` TEXT NULL COMMENT 'Ảnh bằng chứng',
                        `JustificationApproved` BOOLEAN NULL,
                        `ApprovedBy` CHAR(36) NULL,
                        `ApprovedAt` DATETIME NULL,
                        `ManagerComment` TEXT NULL,
                        
                        -- Điều chỉnh thủ công
                        `IsManuallyAdjusted` BOOLEAN NOT NULL DEFAULT FALSE,
                        `AdjustedBy` CHAR(36) NULL,
                        `AdjustedAt` DATETIME NULL,
                        `AdjustmentReason` TEXT NULL,
                        `OriginalCheckIn` DATETIME NULL,
                        `OriginalCheckOut` DATETIME NULL,
                        
                        -- Tính lương
                        `IsPaid` BOOLEAN NOT NULL DEFAULT FALSE,
                        `PayrollProcessedAt` DATETIME NULL,
                        `PayrollBatchId` VARCHAR(100) NULL,
                        
                        -- Audit
                        `CreatedAt` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        `UpdatedAt` DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
                        `IsDeleted` BOOLEAN NOT NULL DEFAULT FALSE,
                        
                        -- Indexes
                        INDEX `idx_attendance_guard_date` (`GuardId`, `AttendanceDate`),
                        INDEX `idx_attendance_shift` (`ShiftId`),
                        INDEX `idx_attendance_assignment` (`AssignmentId`),
                        INDEX `idx_attendance_status` (`AttendanceStatus`, `AttendanceDate`),
                        INDEX `idx_attendance_issues` (`HasIssues`, `AttendanceDate`),
                        INDEX `idx_attendance_payroll` (`IsPaid`, `AttendanceDate`),
                        UNIQUE INDEX `uk_assignment_date` (`AssignmentId`, `AttendanceDate`),
                        
                        FOREIGN KEY (`AssignmentId`) REFERENCES `shift_assignments`(`Id`) ON DELETE CASCADE,
                        FOREIGN KEY (`GuardId`) REFERENCES `guards`(`Id`) ON DELETE CASCADE,
                        FOREIGN KEY (`ShiftId`) REFERENCES `shifts`(`Id`) ON DELETE CASCADE
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
                    COMMENT='Bản ghi chấm công chi tiết - tracking check-in/out và thời gian làm việc';
                ");
            }

            // ============================================================================
            // 11. OVERTIME_RECORDS TABLE - Bản ghi làm thêm giờ
            // ============================================================================
            var overtimeRecordsTableExists = await connection.ExecuteScalarAsync<bool>(
                "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'overtime_records'");
            
            if (!overtimeRecordsTableExists)
            {
                await connection.ExecuteAsync(@"
                    CREATE TABLE IF NOT EXISTS `overtime_records` (
                        `Id` CHAR(36) PRIMARY KEY,
                        
                        -- Liên kết
                        `GuardId` CHAR(36) NOT NULL,
                        `ShiftId` CHAR(36) NULL COMMENT 'Null nếu OT không liên quan ca cụ thể',
                        `AssignmentId` CHAR(36) NULL,
                        `AttendanceId` CHAR(36) NULL,
                        
                        -- Thông tin OT
                        `OvertimeDate` DATE NOT NULL,
                        `OvertimeType` VARCHAR(50) NOT NULL COMMENT 'EXTENDED_SHIFT | EXTRA_SHIFT | WEEKEND | HOLIDAY | EMERGENCY',
                        
                        -- Thời gian
                        `StartTime` DATETIME NOT NULL,
                        `EndTime` DATETIME NOT NULL,
                        `PlannedHours` DECIMAL(5,2) NOT NULL,
                        `ActualStartTime` DATETIME NULL,
                        `ActualEndTime` DATETIME NULL,
                        `ActualHours` DECIMAL(5,2) NULL,
                        
                        -- Phân loại OT
                        `IsPreApproved` BOOLEAN NOT NULL DEFAULT FALSE COMMENT 'OT được duyệt trước',
                        `IsEmergency` BOOLEAN NOT NULL DEFAULT FALSE,
                        `IsWeekend` BOOLEAN NOT NULL DEFAULT FALSE,
                        `IsHoliday` BOOLEAN NOT NULL DEFAULT FALSE,
                        `IsNightShift` BOOLEAN NOT NULL DEFAULT FALSE,
                        
                        -- Yêu cầu OT
                        `RequestedBy` CHAR(36) NULL COMMENT 'Guard yêu cầu hoặc Manager chỉ định',
                        `RequestReason` TEXT NULL,
                        `RequestedAt` DATETIME NULL,
                        
                        -- Phê duyệt
                        `ApprovalStatus` VARCHAR(50) NOT NULL DEFAULT 'PENDING' COMMENT 'PENDING | APPROVED | REJECTED | AUTO_APPROVED',
                        `ApprovedBy` CHAR(36) NULL,
                        `ApprovedAt` DATETIME NULL,
                        `RejectionReason` TEXT NULL,
                        
                        -- Tính lương (multiplier)
                        `OvertimeRate` DECIMAL(4,2) NOT NULL DEFAULT 1.5 COMMENT '1.5x, 2.0x, 3.0x',
                        `BaseSalaryPerHour` DECIMAL(10,2) NULL,
                        `OvertimePay` DECIMAL(10,2) NULL COMMENT 'Số tiền OT',
                        
                        -- Trạng thái
                        `Status` VARCHAR(50) NOT NULL DEFAULT 'PENDING' COMMENT 'PENDING | IN_PROGRESS | COMPLETED | CANCELLED | NO_SHOW',
                        `CompletedAt` DATETIME NULL,
                        `CancelledAt` DATETIME NULL,
                        `CancellationReason` TEXT NULL,
                        
                        -- Ghi chú
                        `WorkDescription` TEXT NULL COMMENT 'Mô tả công việc OT',
                        `ManagerNotes` TEXT NULL,
                        `GuardNotes` TEXT NULL,
                        
                        -- Tính lương
                        `IsPaid` BOOLEAN NOT NULL DEFAULT FALSE,
                        `PayrollProcessedAt` DATETIME NULL,
                        `PayrollBatchId` VARCHAR(100) NULL,
                        
                        -- Audit
                        `CreatedAt` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        `UpdatedAt` DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
                        `CreatedBy` CHAR(36) NULL,
                        `IsDeleted` BOOLEAN NOT NULL DEFAULT FALSE,
                        
                        -- Indexes
                        INDEX `idx_overtime_guard_date` (`GuardId`, `OvertimeDate`),
                        INDEX `idx_overtime_shift` (`ShiftId`),
                        INDEX `idx_overtime_status` (`ApprovalStatus`, `Status`),
                        INDEX `idx_overtime_type` (`OvertimeType`, `OvertimeDate`),
                        INDEX `idx_overtime_payroll` (`IsPaid`, `OvertimeDate`),
                        
                        FOREIGN KEY (`GuardId`) REFERENCES `guards`(`Id`) ON DELETE CASCADE,
                        FOREIGN KEY (`ShiftId`) REFERENCES `shifts`(`Id`) ON DELETE SET NULL,
                        FOREIGN KEY (`AssignmentId`) REFERENCES `shift_assignments`(`Id`) ON DELETE SET NULL,
                        FOREIGN KEY (`AttendanceId`) REFERENCES `attendance_records`(`Id`) ON DELETE SET NULL,
                        FOREIGN KEY (`ApprovedBy`) REFERENCES `managers`(`Id`) ON DELETE SET NULL
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
                    COMMENT='Bản ghi làm thêm giờ - tracking và tính lương OT';
                ");
            }

            // ============================================================================
            // 12. VIOLATION_RECORDS TABLE - Bản ghi vi phạm
            // ============================================================================
            var violationRecordsTableExists = await connection.ExecuteScalarAsync<bool>(
                "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'violation_records'");
            
            if (!violationRecordsTableExists)
            {
                await connection.ExecuteAsync(@"
                    CREATE TABLE IF NOT EXISTS `violation_records` (
                        `Id` CHAR(36) PRIMARY KEY,
                        
                        -- Liên kết
                        `GuardId` CHAR(36) NOT NULL,
                        `ShiftId` CHAR(36) NULL,
                        `AssignmentId` CHAR(36) NULL,
                        `AttendanceId` CHAR(36) NULL,
                        
                        -- Thông tin vi phạm
                        `ViolationDate` DATE NOT NULL,
                        `ViolationTime` DATETIME NOT NULL,
                        
                        -- Loại vi phạm
                        `ViolationType` VARCHAR(100) NOT NULL COMMENT 'LATE | ABSENT | EARLY_LEAVE | NO_SHOW | UNIFORM | CONDUCT | SLEEPING | ALCOHOL | INSUBORDINATION | POLICY_VIOLATION | OTHER',
                        `ViolationCategory` VARCHAR(50) NOT NULL COMMENT 'ATTENDANCE | DISCIPLINE | SAFETY | CONDUCT | PERFORMANCE',
                        `Severity` VARCHAR(50) NOT NULL DEFAULT 'MINOR' COMMENT 'MINOR | MODERATE | MAJOR | CRITICAL',
                        
                        -- Mô tả
                        `Description` TEXT NOT NULL,
                        `Location` VARCHAR(200) NULL,
                        `Witnesses` TEXT NULL COMMENT 'JSON array of witness names/IDs',
                        
                        -- Bằng chứng
                        `EvidencePhotoUrls` TEXT NULL COMMENT 'JSON array of URLs',
                        `EvidenceVideoUrls` TEXT NULL COMMENT 'JSON array of URLs',
                        `EvidenceDocumentUrls` TEXT NULL COMMENT 'JSON array of URLs',
                        
                        -- Người báo cáo
                        `ReportedBy` CHAR(36) NOT NULL COMMENT 'Manager phát hiện',
                        `ReportedAt` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        
                        -- Giải trình của guard
                        `GuardExplanation` TEXT NULL,
                        `GuardExplanationAt` DATETIME NULL,
                        `GuardAcknowledged` BOOLEAN NOT NULL DEFAULT FALSE,
                        `GuardAcknowledgedAt` DATETIME NULL,
                        
                        -- Xử lý kỷ luật
                        `ActionTaken` VARCHAR(100) NULL COMMENT 'WARNING | WRITTEN_WARNING | SUSPENSION | FINE | TERMINATION | TRAINING | NO_ACTION',
                        `ActionDescription` TEXT NULL,
                        `ActionEffectiveDate` DATE NULL,
                        `ActionExpiryDate` DATE NULL COMMENT 'Hết hạn cảnh cáo',
                        
                        -- Phạt tiền (nếu có)
                        `FineAmount` DECIMAL(10,2) NULL,
                        `FineCurrency` VARCHAR(10) NULL DEFAULT 'VND',
                        `FinePaid` BOOLEAN NOT NULL DEFAULT FALSE,
                        `FinePaidAt` DATETIME NULL,
                        
                        -- Đình chỉ (nếu có)
                        `SuspensionDays` INT NULL,
                        `SuspensionStartDate` DATE NULL,
                        `SuspensionEndDate` DATE NULL,
                        
                        -- Trạng thái
                        `Status` VARCHAR(50) NOT NULL DEFAULT 'REPORTED' COMMENT 'REPORTED | UNDER_REVIEW | RESOLVED | APPEALED | DISMISSED',
                        `ResolvedBy` CHAR(36) NULL,
                        `ResolvedAt` DATETIME NULL,
                        `ResolutionNotes` TEXT NULL,
                        
                        -- Kháng cáo
                        `IsAppealed` BOOLEAN NOT NULL DEFAULT FALSE,
                        `AppealReason` TEXT NULL,
                        `AppealedAt` DATETIME NULL,
                        `AppealReviewedBy` CHAR(36) NULL,
                        `AppealReviewedAt` DATETIME NULL,
                        `AppealStatus` VARCHAR(50) NULL COMMENT 'PENDING | ACCEPTED | REJECTED',
                        `AppealOutcome` TEXT NULL,
                        
                        -- Ảnh hưởng
                        `ImpactsPerformanceReview` BOOLEAN NOT NULL DEFAULT TRUE,
                        `ImpactsPromotion` BOOLEAN NOT NULL DEFAULT FALSE,
                        `ImpactsRenewal` BOOLEAN NOT NULL DEFAULT FALSE,
                        
                        -- Lặp lại
                        `IsRepeatOffense` BOOLEAN NOT NULL DEFAULT FALSE,
                        `PreviousViolationIds` TEXT NULL COMMENT 'JSON array',
                        `RepeatOffenseCount` INT NOT NULL DEFAULT 0,
                        
                        -- Audit
                        `CreatedAt` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        `UpdatedAt` DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
                        `IsDeleted` BOOLEAN NOT NULL DEFAULT FALSE,
                        `DeletedAt` DATETIME NULL,
                        `DeletedBy` CHAR(36) NULL,
                        
                        -- Indexes
                        INDEX `idx_violations_guard_date` (`GuardId`, `ViolationDate`),
                        INDEX `idx_violations_type` (`ViolationType`, `ViolationDate`),
                        INDEX `idx_violations_severity` (`Severity`, `Status`),
                        INDEX `idx_violations_status` (`Status`, `ViolationDate`),
                        INDEX `idx_violations_shift` (`ShiftId`),
                        
                        FOREIGN KEY (`GuardId`) REFERENCES `guards`(`Id`) ON DELETE CASCADE,
                        FOREIGN KEY (`ShiftId`) REFERENCES `shifts`(`Id`) ON DELETE SET NULL,
                        FOREIGN KEY (`AssignmentId`) REFERENCES `shift_assignments`(`Id`) ON DELETE SET NULL,
                        FOREIGN KEY (`AttendanceId`) REFERENCES `attendance_records`(`Id`) ON DELETE SET NULL,
                        FOREIGN KEY (`ReportedBy`) REFERENCES `managers`(`Id`) ON DELETE RESTRICT
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
                    COMMENT='Bản ghi vi phạm - quản lý kỷ luật và xử phạt';
                ");
            }

            _tablesCreated = true;
        }
        finally
        {
            _semaphore.Release();
        }
    }
}