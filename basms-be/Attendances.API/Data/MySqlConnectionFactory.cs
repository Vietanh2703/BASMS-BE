namespace Attendances.API.Data;

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

    public string GetConnectionString() => _connectionString;

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
                CREATE TABLE IF NOT EXISTS `attendance_records` (
                    `Id` CHAR(36) PRIMARY KEY,
                    `ShiftAssignmentId` CHAR(36) NOT NULL,
                    `GuardId` CHAR(36) NOT NULL,
                    `ShiftId` CHAR(36) NOT NULL,
                    `CheckInTime` DATETIME NULL COMMENT 'NULL = chưa check-in (PENDING)',
                    `CheckInLatitude` DECIMAL(10,8) NULL,
                    `CheckInLongitude` DECIMAL(11,8) NULL,
                    `CheckInLocationAccuracy` DECIMAL(10,2) NULL,
                    `CheckInDistanceFromSite` DECIMAL(10,2) NULL,
                    `CheckInDeviceId` VARCHAR(255) NULL,
                    `CheckInFaceImageUrl` TEXT NULL COMMENT 'S3 URL ảnh khuôn mặt check-in',
                    `CheckInFaceMatchScore` DECIMAL(5,2) NULL COMMENT 'Điểm match 0-100%',
                    `CheckOutTime` DATETIME NULL,
                    `CheckOutLatitude` DECIMAL(10,8) NULL,
                    `CheckOutLongitude` DECIMAL(11,8) NULL,
                    `CheckOutLocationAccuracy` DECIMAL(10,2) NULL,
                    `CheckOutDistanceFromSite` DECIMAL(10,2) NULL,
                    `CheckOutDeviceId` VARCHAR(255) NULL,
                    `CheckOutFaceImageUrl` TEXT NULL COMMENT 'S3 URL ảnh khuôn mặt check-out',
                    `CheckOutFaceMatchScore` DECIMAL(5,2) NULL COMMENT 'Điểm match 0-100%',
                    `ScheduledStartTime` DATETIME NULL,
                    `ScheduledEndTime` DATETIME NULL,
                    `ActualWorkDurationMinutes` INT NULL,
                    `BreakDurationMinutes` INT NOT NULL DEFAULT 60,
                    `TotalHours` DECIMAL(10,2) NULL,
                    `Status` VARCHAR(50) NOT NULL DEFAULT 'CHECKED_IN',
                    `IsLate` BOOLEAN NOT NULL DEFAULT FALSE,
                    `IsEarlyLeave` BOOLEAN NOT NULL DEFAULT FALSE,
                    `HasOvertime` BOOLEAN NOT NULL DEFAULT FALSE,
                    `IsIncomplete` BOOLEAN NOT NULL DEFAULT FALSE,
                    `IsVerified` BOOLEAN NOT NULL DEFAULT FALSE,
                    `LateMinutes` INT NULL,
                    `EarlyLeaveMinutes` INT NULL,
                    `OvertimeMinutes` INT NULL,
                    `VerifiedBy` CHAR(36) NULL,
                    `VerifiedAt` DATETIME NULL,
                    `VerificationStatus` VARCHAR(50) NOT NULL DEFAULT 'PENDING',
                    `Notes` TEXT NULL,
                    `ManagerNotes` TEXT NULL,
                    `AutoDetected` BOOLEAN NOT NULL DEFAULT FALSE,
                    `FlagsForReview` BOOLEAN NOT NULL DEFAULT FALSE,
                    `FlagReason` TEXT NULL,
                    `CreatedAt` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    `UpdatedAt` DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
                    `IsDeleted` BOOLEAN NOT NULL DEFAULT FALSE,
                    `DeletedAt` DATETIME NULL,

                    INDEX `idx_attendance_shift_assignment` (`ShiftAssignmentId`),
                    INDEX `idx_attendance_guard` (`GuardId`, `CheckInTime`),
                    INDEX `idx_attendance_shift` (`ShiftId`),
                    INDEX `idx_attendance_status` (`Status`, `IsDeleted`),
                    INDEX `idx_attendance_verification` (`VerificationStatus`, `FlagsForReview`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            ");
            
            
            await connection.ExecuteAsync(@"
                CREATE TABLE IF NOT EXISTS `biometric_logs` (
                    `Id` CHAR(36) PRIMARY KEY,
                    `DeviceId` VARCHAR(255) NOT NULL,
                    `DeviceName` VARCHAR(255) NULL,
                    `DeviceType` VARCHAR(50) NOT NULL DEFAULT 'FACE_RECOGNITION',
                    `DeviceLocation` VARCHAR(255) NULL,
                    `DeviceIpAddress` VARCHAR(50) NULL,
                    `GuardId` CHAR(36) NULL,
                    `BiometricUserId` VARCHAR(255) NOT NULL,
                    `BiometricTemplateId` VARCHAR(255) NULL,
                    `MatchScore` DECIMAL(5,2) NULL,
                    `AuthenticationMethod` VARCHAR(50) NOT NULL DEFAULT 'FACE',
                    `FaceImageUrl` TEXT NULL COMMENT 'S3 URL ảnh chụp',
                    `RegisteredFaceTemplateUrl` TEXT NULL COMMENT 'S3 URL template đăng ký',
                    `FaceMatchConfidence` DECIMAL(5,2) NULL COMMENT 'Điểm AI 0-100%',
                    `AiModelVersion` VARCHAR(50) NULL,
                    `AiResponseMetadata` TEXT NULL COMMENT 'JSON metadata từ AI',
                    `LivenessScore` DECIMAL(5,2) NULL COMMENT 'Điểm liveness detection',
                    `FaceQualityScore` DECIMAL(5,2) NULL,
                    `DeviceTimestamp` DATETIME NOT NULL,
                    `ReceivedAt` DATETIME NOT NULL,
                    `EventType` VARCHAR(50) NOT NULL DEFAULT 'CHECK_IN',
                    `IsVerified` BOOLEAN NOT NULL DEFAULT TRUE,
                    `VerificationStatus` VARCHAR(50) NOT NULL DEFAULT 'SUCCESS',
                    `FailureReason` TEXT NULL,
                    `IsProcessed` BOOLEAN NOT NULL DEFAULT FALSE,
                    `ProcessedAt` DATETIME NULL,
                    `AttendanceRecordId` CHAR(36) NULL,
                    `ProcessingStatus` VARCHAR(50) NOT NULL DEFAULT 'PENDING',
                    `ProcessingNotes` TEXT NULL,
                    `RawData` TEXT NULL,
                    `PhotoUrl` TEXT NULL,
                    `BodyTemperature` DECIMAL(4,2) NULL,
                    `IsSynced` BOOLEAN NOT NULL DEFAULT TRUE,
                    `SyncBatchId` CHAR(36) NULL,
                    `RetryCount` INT NOT NULL DEFAULT 0,
                    `LastRetryAt` DATETIME NULL,
                    `IsDuplicate` BOOLEAN NOT NULL DEFAULT FALSE,
                    `IsAnomaly` BOOLEAN NOT NULL DEFAULT FALSE,
                    `AnomalyReason` TEXT NULL,
                    `CreatedAt` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    `UpdatedAt` DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
                    `IsDeleted` BOOLEAN NOT NULL DEFAULT FALSE,
                    `DeletedAt` DATETIME NULL,

                    INDEX `idx_biometric_guard` (`GuardId`, `EventType`),
                    INDEX `idx_biometric_device` (`DeviceId`, `DeviceTimestamp`),
                    INDEX `idx_biometric_processing` (`ProcessingStatus`, `IsProcessed`),
                    INDEX `idx_biometric_attendance` (`AttendanceRecordId`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            ");


            await connection.ExecuteAsync(@"
                CREATE TABLE IF NOT EXISTS `attendance_exceptions` (
                    `Id` CHAR(36) PRIMARY KEY,
                    `AttendanceRecordId` CHAR(36) NULL,
                    `ShiftAssignmentId` CHAR(36) NOT NULL,
                    `GuardId` CHAR(36) NOT NULL,
                    `ShiftId` CHAR(36) NOT NULL,
                    `ExceptionType` VARCHAR(100) NOT NULL,
                    `Severity` VARCHAR(50) NOT NULL DEFAULT 'MEDIUM',
                    `Description` TEXT NOT NULL,
                    `AutoDetected` BOOLEAN NOT NULL DEFAULT TRUE,
                    `DetectedAt` DATETIME NOT NULL,
                    `Status` VARCHAR(50) NOT NULL DEFAULT 'OPEN',
                    `ResolvedAt` DATETIME NULL,
                    `ResolvedBy` CHAR(36) NULL,
                    `ResolutionNotes` TEXT NULL,
                    `ResolutionAction` VARCHAR(100) NULL,
                    `AutoResolvable` BOOLEAN NOT NULL DEFAULT FALSE,
                    `SuggestedAction` TEXT NULL,
                    `NotificationTemplate` TEXT NULL,
                    `PenaltyAmount` DECIMAL(15,2) NULL,
                    `PerformanceImpact` DECIMAL(5,2) NULL,
                    `ImpactNotes` TEXT NULL,
                    `RequiresApproval` BOOLEAN NOT NULL DEFAULT TRUE,
                    `ApprovedBy` CHAR(36) NULL,
                    `ApprovedAt` DATETIME NULL,
                    `ApprovalStatus` VARCHAR(50) NOT NULL DEFAULT 'PENDING',
                    `RejectionReason` TEXT NULL,
                    `CreatedAt` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    `UpdatedAt` DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
                    `CreatedBy` CHAR(36) NOT NULL,
                    `UpdatedBy` CHAR(36) NULL,
                    `IsDeleted` BOOLEAN NOT NULL DEFAULT FALSE,
                    `DeletedAt` DATETIME NULL,

                    INDEX `idx_exceptions_guard` (`GuardId`, `Status`),
                    INDEX `idx_exceptions_shift` (`ShiftId`),
                    INDEX `idx_exceptions_attendance` (`AttendanceRecordId`),
                    INDEX `idx_exceptions_status` (`Status`, `Severity`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            ");


            await connection.ExecuteAsync(@"
                CREATE TABLE IF NOT EXISTS `overtime_records` (
                    `Id` CHAR(36) PRIMARY KEY,
                    `AttendanceRecordId` CHAR(36) NOT NULL,
                    `GuardId` CHAR(36) NOT NULL,
                    `ShiftId` CHAR(36) NOT NULL,
                    `OvertimeType` VARCHAR(50) NOT NULL DEFAULT 'REGULAR_OT',
                    `PlannedOvertimeStart` DATETIME NOT NULL,
                    `PlannedOvertimeEnd` DATETIME NOT NULL,
                    `ActualOvertimeStart` DATETIME NULL,
                    `ActualOvertimeEnd` DATETIME NULL,
                    `PlannedOvertimeMinutes` INT NOT NULL,
                    `ActualOvertimeMinutes` INT NULL,
                    `ActualOvertimeHours` DECIMAL(10,2) NULL,
                    `OvertimeRate` DECIMAL(4,2) NOT NULL DEFAULT 1.5,
                    `BaseHourlyRate` DECIMAL(15,2) NULL,
                    `Status` VARCHAR(50) NOT NULL DEFAULT 'PENDING',
                    `RequestedBy` CHAR(36) NOT NULL,
                    `RequestedAt` DATETIME NOT NULL,
                    `ApprovedBy` CHAR(36) NULL,
                    `ApprovedAt` DATETIME NULL,
                    `RejectionReason` TEXT NULL,
                    `Reason` TEXT NOT NULL,
                    `Notes` TEXT NULL,
                    `ManagerNotes` TEXT NULL,
                    `IsMandatory` BOOLEAN NOT NULL DEFAULT FALSE,
                    `IsEmergency` BOOLEAN NOT NULL DEFAULT FALSE,
                    `IsPaid` BOOLEAN NOT NULL DEFAULT FALSE,
                    `PaidAt` DATETIME NULL,
                    `CreatedAt` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    `UpdatedAt` DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
                    `UpdatedBy` CHAR(36) NULL,
                    `IsDeleted` BOOLEAN NOT NULL DEFAULT FALSE,
                    `DeletedAt` DATETIME NULL,

                    INDEX `idx_overtime_attendance` (`AttendanceRecordId`),
                    INDEX `idx_overtime_guard` (`GuardId`, `Status`),
                    INDEX `idx_overtime_shift` (`ShiftId`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            ");


            await connection.ExecuteAsync(@"
                CREATE TABLE IF NOT EXISTS `leave_requests` (
                    `Id` CHAR(36) PRIMARY KEY,
                    `GuardId` CHAR(36) NOT NULL,
                    `TeamId` CHAR(36) NULL,
                    `HandoverToGuardId` CHAR(36) NULL,
                    `LeaveType` VARCHAR(100) NOT NULL,
                    `LeaveScale` VARCHAR(50) NOT NULL DEFAULT 'FULL_DAY',
                    `StartDate` DATETIME NOT NULL,
                    `EndDate` DATETIME NOT NULL,
                    `StartTime` DATETIME NULL,
                    `EndTime` DATETIME NULL,
                    `TotalDays` INT NOT NULL,
                    `TotalWorkDays` DECIMAL(10,2) NOT NULL,
                    `TotalHours` DECIMAL(10,2) NULL,
                    `Reason` TEXT NOT NULL,
                    `SupportingDocumentUrl` TEXT NULL COMMENT 'S3 URL tài liệu đính kèm',
                    `Notes` TEXT NULL,
                    `ManagerNotes` TEXT NULL,
                    `HasHandover` BOOLEAN NOT NULL DEFAULT FALSE,
                    `ReplacementGuardId` CHAR(36) NULL,
                    `HandoverNotes` TEXT NULL,
                    `ContactDuringLeave` VARCHAR(50) NULL,
                    `EmergencyContact` VARCHAR(100) NULL,
                    `Status` VARCHAR(50) NOT NULL DEFAULT 'PENDING',
                    `SubmittedAt` DATETIME NOT NULL,
                    `ApprovedBy` CHAR(36) NULL,
                    `ApprovedAt` DATETIME NULL,
                    `RejectionReason` TEXT NULL,
                    `CancelledAt` DATETIME NULL,
                    `CancellationReason` TEXT NULL,
                    `IsPaid` BOOLEAN NOT NULL DEFAULT TRUE,
                    `PaymentPercentage` DECIMAL(5,2) NOT NULL DEFAULT 100.00,
                    `DeductsFromAnnualLeave` BOOLEAN NOT NULL DEFAULT FALSE,
                    `AnnualLeaveDaysDeducted` DECIMAL(10,2) NULL,
                    `CreatedAt` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    `UpdatedAt` DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
                    `CreatedBy` CHAR(36) NOT NULL,
                    `UpdatedBy` CHAR(36) NULL,
                    `IsDeleted` BOOLEAN NOT NULL DEFAULT FALSE,
                    `DeletedAt` DATETIME NULL,
                    `Version` INT NOT NULL DEFAULT 1,

                    INDEX `idx_leave_guard` (`GuardId`, `Status`),
                    INDEX `idx_leave_dates` (`StartDate`, `EndDate`),
                    INDEX `idx_leave_status` (`Status`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            ");


            await connection.ExecuteAsync(@"
                CREATE TABLE IF NOT EXISTS `attendance_summary` (
                    `Id` CHAR(36) PRIMARY KEY,
                    `GuardId` CHAR(36) NOT NULL,
                    `TeamId` CHAR(36) NULL,
                    `PeriodStartDate` DATETIME NOT NULL,
                    `PeriodEndDate` DATETIME NOT NULL,
                    `PeriodType` VARCHAR(50) NOT NULL DEFAULT 'MONTHLY',
                    `SummaryMonth` INT NULL,
                    `SummaryYear` INT NULL,
                    `SummaryQuarter` INT NULL,
                    `SummaryWeek` INT NULL,
                    `TotalShiftsAssigned` INT NOT NULL DEFAULT 0,
                    `TotalShiftsAttended` INT NOT NULL DEFAULT 0,
                    `TotalShiftsCompleted` INT NOT NULL DEFAULT 0,
                    `TotalAbsences` INT NOT NULL DEFAULT 0,
                    `TotalLateCount` INT NOT NULL DEFAULT 0,
                    `TotalEarlyLeaveCount` INT NOT NULL DEFAULT 0,
                    `TotalScheduledHours` DECIMAL(10,2) NOT NULL DEFAULT 0,
                    `TotalActualHours` DECIMAL(10,2) NOT NULL DEFAULT 0,
                    `TotalOvertimeHours` DECIMAL(10,2) NOT NULL DEFAULT 0,
                    `TotalDayHours` DECIMAL(10,2) NOT NULL DEFAULT 0,
                    `TotalNightHours` DECIMAL(10,2) NOT NULL DEFAULT 0,
                    `TotalApprovedLeaves` INT NOT NULL DEFAULT 0,
                    `TotalPaidLeaveDays` DECIMAL(10,2) NOT NULL DEFAULT 0,
                    `TotalUnpaidLeaveDays` DECIMAL(10,2) NOT NULL DEFAULT 0,
                    `TotalSickLeaveDays` DECIMAL(10,2) NOT NULL DEFAULT 0,
                    `TotalLateMinutes` INT NOT NULL DEFAULT 0,
                    `TotalEarlyLeaveMinutes` INT NOT NULL DEFAULT 0,
                    `PunctualityScore` DECIMAL(5,2) NULL,
                    `CompletionRate` DECIMAL(5,2) NULL,
                    `TotalExceptions` INT NOT NULL DEFAULT 0,
                    `ResolvedExceptions` INT NOT NULL DEFAULT 0,
                    `PendingExceptions` INT NOT NULL DEFAULT 0,
                    `PerformanceScore` DECIMAL(5,2) NULL,
                    `PerformanceNotes` TEXT NULL,
                    `IsReviewed` BOOLEAN NOT NULL DEFAULT FALSE,
                    `ReviewedBy` CHAR(36) NULL,
                    `ReviewedAt` DATETIME NULL,
                    `CalculatedAt` DATETIME NOT NULL,
                    `CalculationVersion` INT NOT NULL DEFAULT 1,
                    `IsAutoCalculated` BOOLEAN NOT NULL DEFAULT TRUE,
                    `IsFinalized` BOOLEAN NOT NULL DEFAULT FALSE,
                    `FinalizedAt` DATETIME NULL,
                    `FinalizedBy` CHAR(36) NULL,
                    `CreatedAt` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    `UpdatedAt` DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
                    `CreatedBy` CHAR(36) NULL,
                    `UpdatedBy` CHAR(36) NULL,
                    `IsDeleted` BOOLEAN NOT NULL DEFAULT FALSE,
                    `DeletedAt` DATETIME NULL,

                    INDEX `idx_summary_guard` (`GuardId`, `PeriodType`),
                    INDEX `idx_summary_period` (`SummaryYear`, `SummaryMonth`, `PeriodType`),
                    INDEX `idx_summary_finalized` (`IsFinalized`, `PeriodType`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            ");

            _tablesCreated = true;
            Console.WriteLine("All Attendance tables created successfully");
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
