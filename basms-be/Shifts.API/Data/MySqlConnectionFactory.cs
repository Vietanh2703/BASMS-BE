namespace Shifts.API.Data;

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
            
            await connection.ExecuteAsync(@"
                CREATE TABLE IF NOT EXISTS `managers` (
                    `Id` CHAR(36) PRIMARY KEY,
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
                    `Role` VARCHAR(50) NOT NULL DEFAULT 'MANAGER',
                    `ReportsToManagerId` CHAR(36) NULL,
                    `CertificationLevel` VARCHAR(10) NULL COMMENT 'Hạng chứng chỉ: I, II, III, IV, V, VI',
                    `StandardWage` DECIMAL(15,2) NULL COMMENT 'Mức lương cơ bản (VNĐ/tháng) import từ hợp đồng',
                    `CertificationFileUrl` TEXT NULL COMMENT 'URL file chứng chỉ (PDF/image)',
                    `IdentityCardFrontUrl` TEXT NULL COMMENT 'URL ảnh CCCD mặt trước',
                    `IdentityCardBackUrl` TEXT NULL COMMENT 'URL ảnh CCCD mặt sau',
                    `EmploymentStatus` VARCHAR(50) NOT NULL DEFAULT 'ACTIVE',
                    `CanCreateShifts` BOOLEAN NOT NULL DEFAULT TRUE,
                    `CanApproveShifts` BOOLEAN NOT NULL DEFAULT TRUE,
                    `CanAssignGuards` BOOLEAN NOT NULL DEFAULT TRUE,
                    `CanApproveOvertime` BOOLEAN NOT NULL DEFAULT TRUE,
                    `CanManageTeams` BOOLEAN NOT NULL DEFAULT TRUE,
                    `MaxTeamSize` INT NULL,
                    `TotalTeamsManaged` INT NOT NULL DEFAULT 0,
                    `TotalGuardsSupervised` INT NOT NULL DEFAULT 0,
                    `TotalShiftsCreated` INT NOT NULL DEFAULT 0,
                    `LastSyncedAt` DATETIME NULL,
                    `SyncStatus` VARCHAR(50) NOT NULL DEFAULT 'SYNCED',
                    `UserServiceVersion` INT NULL,
                    `IsActive` BOOLEAN NOT NULL DEFAULT TRUE,
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
            
            await connection.ExecuteAsync(@"
                CREATE TABLE IF NOT EXISTS `guards` (
                    `Id` CHAR(36) PRIMARY KEY,
                    `IdentityNumber` VARCHAR(12) NOT NULL,
                    `IdentityIssueDate` DATETIME NULL,
                    `IdentityIssuePlace` VARCHAR(255) NULL,
                    `EmployeeCode` VARCHAR(50) NOT NULL,
                    `FullName` VARCHAR(200) NOT NULL,
                    `AvatarUrl` TEXT NULL,
                    `Email` VARCHAR(255) NULL,
                    `PhoneNumber` VARCHAR(20) NOT NULL,
                    `DateOfBirth` DATETIME NULL,
                    `Gender` VARCHAR(10) NULL,
                    `CurrentAddress` TEXT NULL, 
                    `EmploymentStatus` VARCHAR(50) NOT NULL DEFAULT 'ACTIVE',
                    `HireDate` DATETIME NOT NULL,
                    `ProbationEndDate` DATETIME NULL,
                    `ContractType` VARCHAR(50) NULL,
                    `TerminationDate` DATETIME NULL,
                    `TerminationReason` TEXT NULL,
                    `DirectManagerId` CHAR(36) NULL,
                    `CertificationLevel` VARCHAR(10) NULL COMMENT 'Hạng chứng chỉ: I, II, III, IV, V, VI',
                    `StandardWage` DECIMAL(15,2) NULL COMMENT 'Mức lương cơ bản (VNĐ/tháng) import từ hợp đồng',
                    `CertificationFileUrl` TEXT NULL COMMENT 'URL file chứng chỉ (PDF/image)',
                    `IdentityCardFrontUrl` TEXT NULL COMMENT 'URL ảnh CCCD mặt trước',
                    `IdentityCardBackUrl` TEXT NULL COMMENT 'URL ảnh CCCD mặt sau',
                    `PreferredShiftType` VARCHAR(50) NULL,
                    `PreferredLocations` TEXT NULL,
                    `MaxWeeklyHours` INT NOT NULL DEFAULT 48,
                    `CanWorkOvertime` BOOLEAN NOT NULL DEFAULT TRUE,
                    `CanWorkWeekends` BOOLEAN NOT NULL DEFAULT TRUE,
                    `CanWorkHolidays` BOOLEAN NOT NULL DEFAULT TRUE,
                    `TotalShiftsWorked` INT NOT NULL DEFAULT 0,
                    `TotalHoursWorked` DECIMAL(10,2) NOT NULL DEFAULT 0,
                    `AttendanceRate` DECIMAL(5,2) NULL,
                    `PunctualityRate` DECIMAL(5,2) NULL,
                    `NoShowCount` INT NOT NULL DEFAULT 0,
                    `ViolationCount` INT NOT NULL DEFAULT 0,
                    `CommendationCount` INT NOT NULL DEFAULT 0,
                    `CurrentAvailability` VARCHAR(50) NOT NULL DEFAULT 'AVAILABLE',
                    `AvailabilityNotes` TEXT NULL,
                    `BiometricRegistered` BOOLEAN NOT NULL DEFAULT FALSE,
                    `FaceTemplateUrl` TEXT NULL,
                    `LastAppLogin` DATETIME NULL,
                    `DeviceTokens` TEXT NULL,
                    `LastSyncedAt` DATETIME NULL,
                    `SyncStatus` VARCHAR(50) NOT NULL DEFAULT 'SYNCED',
                    `UserServiceVersion` INT NULL,
                    `IsActive` BOOLEAN NOT NULL DEFAULT TRUE,
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
            
            await connection.ExecuteAsync(@"
                CREATE TABLE IF NOT EXISTS `shift_templates` (
                    `Id` CHAR(36) PRIMARY KEY,
                    `ManagerId` CHAR(36) NULL,
                    `ContractId` CHAR(36) NULL,
                    `TeamId` CHAR(36) NULL COMMENT 'Team to auto-assign when generating shifts from this template',
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
                    INDEX `idx_templates_team` (`TeamId`, `IsActive`),
                    INDEX `idx_templates_location` (`LocationId`, `IsActive`),
                    INDEX `idx_templates_active` (`IsActive`, `IsDeleted`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            ");

            await connection.ExecuteAsync(@"
                CREATE TABLE IF NOT EXISTS `shifts` (
                    `Id` CHAR(36) PRIMARY KEY,
                    `ShiftTemplateId` CHAR(36) NULL,
                    `LocationId` CHAR(36) NULL,
                    `LocationName` VARCHAR(200) NULL,
                    `LocationAddress` TEXT NULL,
                    `LocationLatitude` DECIMAL(10,8) NULL,
                    `LocationLongitude` DECIMAL(11,8) NULL,
                    `ContractId` CHAR(36) NULL,
                    `ManagerId` CHAR(36) NULL,
                    `ShiftDate` DATE NOT NULL,
                    `ShiftDay` INT NOT NULL,
                    `ShiftMonth` INT NOT NULL,
                    `ShiftYear` INT NOT NULL,
                    `ShiftQuarter` INT NOT NULL,
                    `ShiftWeek` INT NOT NULL,
                    `DayOfWeek` INT NOT NULL,
                    `ShiftEndDate` DATETIME NULL,
                    `ShiftStart` DATETIME NOT NULL,
                    `ShiftEnd` DATETIME NOT NULL,
                    `TotalDurationMinutes` INT NOT NULL DEFAULT 0,
                    `WorkDurationMinutes` INT NOT NULL DEFAULT 0,
                    `WorkDurationHours` DECIMAL(10,2) NOT NULL DEFAULT 0,
                    `BreakDurationMinutes` INT NOT NULL DEFAULT 60,
                    `PaidBreakMinutes` INT NOT NULL DEFAULT 0,
                    `UnpaidBreakMinutes` INT NOT NULL DEFAULT 60,
                    `RequiredGuards` INT NOT NULL DEFAULT 1,
                    `AssignedGuardsCount` INT NOT NULL DEFAULT 0,
                    `ConfirmedGuardsCount` INT NOT NULL DEFAULT 0,
                    `CheckedInGuardsCount` INT NOT NULL DEFAULT 0,
                    `CompletedGuardsCount` INT NOT NULL DEFAULT 0,
                    `IsFullyStaffed` BOOLEAN NOT NULL DEFAULT FALSE,
                    `IsUnderstaffed` BOOLEAN NOT NULL DEFAULT FALSE,
                    `IsOverstaffed` BOOLEAN NOT NULL DEFAULT FALSE,
                    `StaffingPercentage` DECIMAL(5,2) NULL,
                    `IsRegularWeekday` BOOLEAN NOT NULL DEFAULT TRUE,
                    `IsSaturday` BOOLEAN NOT NULL DEFAULT FALSE,
                    `IsSunday` BOOLEAN NOT NULL DEFAULT FALSE,
                    `IsPublicHoliday` BOOLEAN NOT NULL DEFAULT FALSE,
                    `IsTetHoliday` BOOLEAN NOT NULL DEFAULT FALSE,
                    `IsNightShift` BOOLEAN NOT NULL DEFAULT FALSE,
                    `NightHours` DECIMAL(10,2) NOT NULL DEFAULT 0,
                    `DayHours` DECIMAL(10,2) NOT NULL DEFAULT 0,
                    `ShiftType` VARCHAR(50) NOT NULL DEFAULT 'REGULAR',
                    `IsMandatory` BOOLEAN NOT NULL DEFAULT FALSE,
                    `IsCritical` BOOLEAN NOT NULL DEFAULT FALSE,
                    `IsTrainingShift` BOOLEAN NOT NULL DEFAULT FALSE,
                    `RequiresArmedGuard` BOOLEAN NOT NULL DEFAULT FALSE,
                    `RequiresApproval` BOOLEAN NOT NULL DEFAULT TRUE,
                    `ApprovedBy` CHAR(36) NULL,
                    `ApprovedAt` DATETIME NULL,
                    `ApprovalStatus` VARCHAR(50) NOT NULL DEFAULT 'PENDING',
                    `RejectionReason` TEXT NULL,
                    `Status` VARCHAR(50) NOT NULL DEFAULT 'DRAFT',
                    `ScheduledAt` DATETIME NULL,
                    `StartedAt` DATETIME NULL,
                    `CompletedAt` DATETIME NULL,
                    `CancelledAt` DATETIME NULL,
                    `CancellationReason` TEXT NULL,
                    `Description` TEXT NULL,
                    `SpecialInstructions` TEXT NULL,
                    `EquipmentNeeded` TEXT NULL,
                    `EmergencyContacts` TEXT NULL,
                    `SiteAccessInfo` TEXT NULL,
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
            
            await connection.ExecuteAsync(@"
                CREATE TABLE IF NOT EXISTS `shift_assignments` (
                    `Id` CHAR(36) PRIMARY KEY,
                    `ShiftId` CHAR(36) NOT NULL,
                    `TeamId` CHAR(36) NULL,
                    `GuardId` CHAR(36) NOT NULL,
                    `AssignmentType` VARCHAR(50) NOT NULL DEFAULT 'REGULAR',
                    `ReplacedGuardId` CHAR(36) NULL,
                    `ReplacementReason` TEXT NULL,
                    `IsReplacement` BOOLEAN NOT NULL DEFAULT FALSE,
                    `Status` VARCHAR(50) NOT NULL DEFAULT 'ASSIGNED',
                    `AssignedAt` DATETIME NOT NULL,
                    `ConfirmedAt` DATETIME NULL,
                    `DeclinedAt` DATETIME NULL,
                    `CheckedInAt` DATETIME NULL,
                    `CheckedOutAt` DATETIME NULL,
                    `CompletedAt` DATETIME NULL,
                    `CancelledAt` DATETIME NULL,
                    `DeclineReason` TEXT NULL,
                    `CancellationReason` TEXT NULL,
                    `AttendanceRecordId` CHAR(36) NULL,
                    `AttendanceSynced` BOOLEAN NOT NULL DEFAULT FALSE,
                    `NotificationSent` BOOLEAN NOT NULL DEFAULT FALSE,
                    `NotificationSentAt` DATETIME NULL,
                    `NotificationMethod` VARCHAR(50) NULL,
                    `Reminder24HSent` BOOLEAN NOT NULL DEFAULT FALSE,
                    `Reminder24HSentAt` DATETIME NULL,
                    `Reminder2HSent` BOOLEAN NOT NULL DEFAULT FALSE,
                    `Reminder2HSentAt` DATETIME NULL,
                    `PunctualityScore` DECIMAL(3,2) NULL,
                    `PerformanceNote` TEXT NULL,
                    `RatedBy` CHAR(36) NULL,
                    `RatedAt` DATETIME NULL,
                    `AssignmentNotes` TEXT NULL,
                    `GuardNotes` TEXT NULL,
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


            await connection.ExecuteAsync(@"
                CREATE TABLE IF NOT EXISTS `shift_issues` (
                    `Id` CHAR(36) PRIMARY KEY,
                    `ShiftId` CHAR(36) NULL COMMENT 'Shift bị ảnh hưởng (NULL nếu bulk cancel)',
                    `GuardId` CHAR(36) NULL COMMENT 'Guard liên quan đến issue',
                    `IssueType` VARCHAR(50) NOT NULL COMMENT 'CANCEL_SHIFT | BULK_CANCEL | SICK_LEAVE | MATERNITY_LEAVE | OTHER',
                    `Reason` TEXT NOT NULL COMMENT 'Lý do chi tiết (nghỉ ốm, thai sản, v.v.)',
                    `StartDate` DATE NULL COMMENT 'Ngày bắt đầu nghỉ (cho bulk cancel)',
                    `EndDate` DATE NULL COMMENT 'Ngày kết thúc nghỉ (cho bulk cancel)',
                    `IssueDate` DATETIME NOT NULL COMMENT 'Ngày phát sinh sự cố',
                    `EvidenceFileUrl` TEXT NULL COMMENT 'URL file chứng từ trên S3 (đơn xin nghỉ, giấy khám bệnh, v.v.)',
                    `TotalShiftsAffected` INT NOT NULL DEFAULT 0 COMMENT 'Tổng số ca bị ảnh hưởng',
                    `TotalGuardsAffected` INT NOT NULL DEFAULT 0 COMMENT 'Tổng số guard bị ảnh hưởng',
                    `CreatedAt` DATETIME NOT NULL COMMENT 'Thời gian tạo record (VN timezone)',
                    `CreatedBy` CHAR(36) NOT NULL COMMENT 'Manager tạo record',
                    `UpdatedAt` DATETIME NULL COMMENT 'Thời gian cập nhật cuối (VN timezone)',
                    `UpdatedBy` CHAR(36) NULL COMMENT 'Người cập nhật cuối',
                    `IsDeleted` BOOLEAN NOT NULL DEFAULT FALSE,
                    `DeletedAt` DATETIME NULL,
                    `DeletedBy` CHAR(36) NULL,
                    INDEX `idx_shift_issues_shift` (`ShiftId`, `IsDeleted`),
                    INDEX `idx_shift_issues_guard` (`GuardId`, `IsDeleted`),
                    INDEX `idx_shift_issues_type` (`IssueType`, `IsDeleted`),
                    INDEX `idx_shift_issues_date_range` (`StartDate`, `EndDate`),
                    INDEX `idx_shift_issues_created` (`CreatedAt`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
                COMMENT='Lưu thông tin sự cố ảnh hưởng tới ca trực và nhân sự';
            ");


            await connection.ExecuteAsync(@"
                CREATE TABLE IF NOT EXISTS `wage_rates` (
                    `Id` CHAR(36) PRIMARY KEY,
                    `CertificationLevel` VARCHAR(10) NOT NULL COMMENT 'Hạng chứng chỉ: I, II, III, IV, V, VI',
                    `MinWage` DECIMAL(15,2) NOT NULL COMMENT 'Mức tối thiểu',
                    `MaxWage` DECIMAL(15,2) NOT NULL COMMENT 'Mức tối đa',
                    `StandardWage` DECIMAL(15,2) NOT NULL COMMENT 'Mức chuẩn (gợi ý khi tạo HĐ)',
                    `StandardWageInWords` TEXT NULL COMMENT 'Số tiền chuẩn bằng chữ: Sáu triệu đồng chẵn',
                    `Currency` VARCHAR(10) NOT NULL DEFAULT 'VNĐ' COMMENT 'Đơn vị tính: VNĐ, USD...',
                    `Description` TEXT NULL COMMENT 'Mô tả chi tiết về cấp bậc và nhiệm vụ',
                    `EffectiveFrom` DATE NOT NULL COMMENT 'Ngày bắt đầu áp dụng',
                    `EffectiveTo` DATE NULL COMMENT 'Ngày kết thúc áp dụng (NULL = đang áp dụng)',
                    `Notes` TEXT NULL COMMENT 'Ghi chú về mức lương',
                    `IsActive` BOOLEAN NOT NULL DEFAULT TRUE,
                    `CreatedAt` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    `UpdatedAt` DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
                    `CreatedBy` CHAR(36) NULL,
                    `UpdatedBy` CHAR(36) NULL,

                    UNIQUE KEY `unique_level_date` (`CertificationLevel`, `EffectiveFrom`),
                    INDEX `idx_wage_rates_level_active` (`CertificationLevel`, `IsActive`, `EffectiveFrom`),
                    INDEX `idx_wage_rates_effective` (`EffectiveFrom`, `EffectiveTo`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
                COMMENT='Bảng mức tiền công chuẩn theo cấp bậc bảo vệ';
            ");
            
            await connection.ExecuteAsync(@"
                INSERT IGNORE INTO `wage_rates`
                (`Id`, `CertificationLevel`, `MinWage`, `MaxWage`, `StandardWage`, `StandardWageInWords`, `Currency`, `Description`, `EffectiveFrom`, `Notes`, `IsActive`, `CreatedAt`)
                VALUES
                ('010507b3-5eba-4c03-8269-f915f87e0651', 'I', 5500000.00, 6500000.00, 6000000.00,
                 'Sáu triệu đồng chẵn', 'VNĐ',
                 'Bảo vệ hạng I - Bảo vệ thường, canh gác đơn giản, không yêu cầu kinh nghiệm',
                 '2025-01-01', 'Mức cơ bản cho bảo vệ mới vào nghề', TRUE,
                 CONVERT_TZ('2025-01-01 00:00:00', '+00:00', '+07:00')),

                ('4c3bee0d-84a3-4125-ad11-0e8abc4f4ada', 'II', 7000000.00, 8500000.00, 7500000.00,
                 'Bảy triệu năm trăm nghìn đồng chẵn', 'VNĐ',
                 'Bảo vệ hạng II - Bảo vệ trọng điểm, trưởng ca, có ít nhất 1 năm kinh nghiệm',
                 '2025-01-01', 'Phù hợp cho trưởng ca, bảo vệ địa điểm quan trọng', TRUE,
                 CONVERT_TZ('2025-01-01 00:00:00', '+00:00', '+07:00')),

                ('3642fc11-968b-4004-8bfe-dd14a1f3429e', 'III', 9000000.00, 11000000.00, 10000000.00,
                 'Mười triệu đồng chẵn', 'VNĐ',
                 'Bảo vệ hạng III - Trưởng đội, giám sát địa điểm, có ít nhất 2 năm kinh nghiệm',
                 '2025-01-01', 'Quản lý team 5-10 người, giám sát 1 địa điểm', TRUE,
                 CONVERT_TZ('2025-01-01 00:00:00', '+00:00', '+07:00')),

                ('52195235-dae4-4e5a-8cfc-900e1eb099b9', 'IV', 12000000.00, 14000000.00, 13000000.00,
                 'Mười ba triệu đồng chẵn', 'VNĐ',
                 'Bảo vệ hạng IV - Quản lý nhiều địa điểm, đào tạo nâng cao',
                 '2025-01-01', 'Quản lý 2-3 địa điểm, có thể đào tạo bảo vệ mới', TRUE,
                 CONVERT_TZ('2025-01-01 00:00:00', '+00:00', '+07:00')),

                ('0ac4b24d-6c98-4e72-929d-da2180541e91', 'V', 15000000.00, 18000000.00, 16500000.00,
                 'Mười sáu triệu năm trăm nghìn đồng chẵn', 'VNĐ',
                 'Bảo vệ hạng V - Quản lý vùng, điều phối, có bằng cao đẳng an ninh + 5 năm',
                 '2025-01-01', 'Quản lý khu vực, điều phối nhiều team', TRUE,
                 CONVERT_TZ('2025-01-01 00:00:00', '+00:00', '+07:00')),

                ('1c778afc-a39b-4d9e-8d55-acd5dcf5cb08', 'VI', 20000000.00, 30000000.00, 25000000.00,
                 'Hai mươi lăm triệu đồng chẵn', 'VNĐ',
                 'Bảo vệ hạng VI - Giám đốc vận hành, có bằng đại học an ninh + 7 năm',
                 '2025-01-01', 'C-level, chiến lược, quản lý toàn bộ hoạt động', TRUE,
                 CONVERT_TZ('2025-01-01 00:00:00', '+00:00', '+07:00'));
            ");

            Console.WriteLine("✓ Wage rates table created with sample data (6 levels)");

            _tablesCreated = true;
            Console.WriteLine("✓ All tables created successfully");
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
