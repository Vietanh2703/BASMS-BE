using MySql.Data.MySqlClient;

namespace Contracts.API.Data;

/// <summary>
/// MySQL connection factory cho Contracts service
/// Tạo 16 tables theo ERD: company_profile, customers, contracts, locations...
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

            // Check if tables already exist
            var tablesExist = await connection.ExecuteScalarAsync<bool>(@"
                SELECT COUNT(*) FROM information_schema.tables
                WHERE table_schema = DATABASE()
                AND table_name = 'company_profile'
            ");

            if (tablesExist)
            {
                Console.WriteLine("✓ Contracts tables already exist, skipping creation");
                _tablesCreated = true;
                return;
            }

            Console.WriteLine("Creating Contracts database tables...");

            // ====================================================================
            // BƯỚC 1: TẠO TẤT CẢ TABLES KHÔNG CÓ FOREIGN KEY TRƯỚC
            // ====================================================================

            try
            {
            // ====================================================================
            // 1. CUSTOMERS - Khách hàng
            // ====================================================================
            Console.WriteLine("Creating table: customers");
            await connection.ExecuteAsync(@"
                CREATE TABLE IF NOT EXISTS `customers` (
                    `Id` CHAR(36) PRIMARY KEY,
                    `UserId` CHAR(36) UNIQUE NOT NULL COMMENT 'Link to Users Service',
                    `CustomerCode` VARCHAR(50) UNIQUE NOT NULL COMMENT 'CUST-001',
                    `CompanyName` VARCHAR(255) NOT NULL,
                    `ContactPersonName` VARCHAR(255) NOT NULL,
                    `ContactPersonTitle` VARCHAR(100) NULL,
                    `IdentityNumber` VARCHAR(12) NOT NULL UNIQUE,
                    `IdentityIssueDate` DATETIME NULL COMMENT 'Ngày cấp CCCD',
                    `IdentityIssuePlace` VARCHAR(255) NULL COMMENT 'Nơi cấp CCCD',
                    `Email` VARCHAR(255) NOT NULL,
                    `Phone` VARCHAR(20) NOT NULL,
                    `AvatarUrl` VARCHAR(500) NULL,
                    `Gender` VARCHAR(6) NULL COMMENT 'male, female, other...',
                    `DateOfBirth` DATE NOT NULL,
                    `Address` TEXT NOT NULL,
                    `City` VARCHAR(100) NULL,
                    `District` VARCHAR(100) NULL,
                    `Industry` VARCHAR(100) NULL COMMENT 'retail, office, hospital...',
                    `CompanySize` VARCHAR(50) NULL COMMENT 'small, medium, large...',
                    `CustomerSince` DATE NOT NULL DEFAULT (CURRENT_DATE),
                    `Status` VARCHAR(20) NOT NULL DEFAULT 'active',
                    `FollowsNationalHolidays` BOOLEAN NOT NULL DEFAULT TRUE,
                    `Notes` TEXT NULL,
                    `IsDeleted` BOOLEAN NOT NULL DEFAULT FALSE,
                    `CreatedAt` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    `UpdatedAt` DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
                    `CreatedBy` CHAR(36) NULL,
                    `UpdatedBy` CHAR(36) NULL,
                    INDEX `idx_customer_user` (`UserId`),
                    INDEX `idx_customer_code` (`CustomerCode`),
                    INDEX `idx_customer_identity_number` (`IdentityNumber`),
                    INDEX `idx_customer_status` (`Status`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
                COMMENT='Khách hàng - công ty thuê dịch vụ bảo vệ';
            ");

            // ====================================================================
            // 2. CUSTOMER_LOCATIONS - Địa điểm khách hàng
            // ====================================================================
            await connection.ExecuteAsync(@"
                CREATE TABLE IF NOT EXISTS `customer_locations` (
                    `Id` CHAR(36) PRIMARY KEY,
                    `CustomerId` CHAR(36) NOT NULL,
                    `LocationCode` VARCHAR(50) NOT NULL COMMENT 'LOC-001',
                    `LocationName` VARCHAR(255) NOT NULL,
                    `LocationType` VARCHAR(50) NOT NULL COMMENT 'office, warehouse, factory...',
                    `Address` TEXT NOT NULL,
                    `City` VARCHAR(100) NULL,
                    `District` VARCHAR(100) NULL,
                    `Ward` VARCHAR(100) NULL,
                    `Latitude` DECIMAL(10,8) NULL COMMENT 'GPS latitude',
                    `Longitude` DECIMAL(11,8) NULL COMMENT 'GPS longitude',
                    `GeofenceRadiusMeters` INT NOT NULL DEFAULT 100,
                    `AltitudeMeters` DECIMAL(8,2) NULL,
                    `SiteManagerName` VARCHAR(255) NULL,
                    `SiteManagerPhone` VARCHAR(20) NULL,
                    `EmergencyContactName` VARCHAR(255) NULL,
                    `EmergencyContactPhone` VARCHAR(20) NULL,
                    `OperatingHoursType` VARCHAR(50) NOT NULL DEFAULT '24/7',
                    `TotalAreaSqm` DECIMAL(10,2) NULL,
                    `BuildingFloors` INT NULL,
                    `FollowsStandardWorkweek` BOOLEAN NOT NULL DEFAULT TRUE,
                    `CustomWeekendDays` VARCHAR(20) NULL,
                    `Requires24x7Coverage` BOOLEAN NOT NULL DEFAULT FALSE,
                    `AllowsSingleGuard` BOOLEAN NOT NULL DEFAULT TRUE,
                    `MinimumGuardsRequired` INT NOT NULL DEFAULT 1,
                    `AccessInstructions` TEXT NULL,
                    `SpecialRequirements` TEXT NULL,
                    `IsActive` BOOLEAN NOT NULL DEFAULT TRUE,
                    `IsDeleted` BOOLEAN NOT NULL DEFAULT FALSE,
                    `CreatedAt` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    `UpdatedAt` DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
                    `CreatedBy` CHAR(36) NULL,
                    `UpdatedBy` CHAR(36) NULL,
                    INDEX `idx_location_customer` (`CustomerId`, `IsActive`),
                    UNIQUE INDEX `uk_location_code` (`LocationCode`, `CustomerId`),
                    INDEX `idx_location_geo` (`Latitude`, `Longitude`),
                    FOREIGN KEY (`CustomerId`) REFERENCES `customers`(`Id`) ON DELETE CASCADE
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
                COMMENT='Địa điểm khách hàng - nơi triển khai bảo vệ';
            ");

            // ====================================================================
            // 3. CONTRACT_DOCUMENTS - Tài liệu hợp đồng (MOVED BEFORE CONTRACTS)
            // ====================================================================
            await connection.ExecuteAsync(@"
                CREATE TABLE IF NOT EXISTS `contract_documents` (
                    `Id` CHAR(36) PRIMARY KEY,
                    `DocumentType` VARCHAR(50) NOT NULL,
                    `DocumentName` VARCHAR(255) NOT NULL,
                    `FileUrl` VARCHAR(500) NOT NULL,
                    `FileSize` BIGINT NULL,
                    `Version` VARCHAR(20) NOT NULL DEFAULT '1.0',
                    `Tokens` VARCHAR(100) NULL,
                    `TokenExpiredDay` DATETIME NULL,
                    `DocumentDate` DATE NULL,
                    `UploadedBy` CHAR(36) NULL,
                    `IsDeleted` BOOLEAN NOT NULL DEFAULT FALSE,
                    `CreatedAt` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    INDEX `idx_doc_type` (`DocumentType`),
                    INDEX `idx_doc_token` (`Tokens`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
                COMMENT='Tài liệu hợp đồng';
             ");

            // ====================================================================
            // 6. CONTRACTS - Hợp đồng dịch vụ
            // ====================================================================
            await connection.ExecuteAsync(@"
                CREATE TABLE IF NOT EXISTS `contracts` (
                    `Id` CHAR(36) PRIMARY KEY,
                    `CustomerId` CHAR(36) NULL COMMENT 'NULL for working_contract, required for service contracts',
                    `DocumentId` CHAR(36) NULL COMMENT 'Link to contract_documents.Id',
                    `ContractNumber` VARCHAR(50) UNIQUE NOT NULL COMMENT 'CTR-2025-001',
                    `ContractTitle` VARCHAR(255) NOT NULL,
                    `ContractType` VARCHAR(50) NOT NULL COMMENT 'long_term, short_term...',
                    `ServiceScope` VARCHAR(100) NOT NULL COMMENT 'continuous_24x7, shift_based...',
                    `StartDate` DATE NOT NULL,
                    `EndDate` DATE NOT NULL,
                    `DurationMonths` INT NOT NULL,
                    `IsRenewable` BOOLEAN NOT NULL DEFAULT TRUE,
                    `AutoRenewal` BOOLEAN NOT NULL DEFAULT FALSE,
                    `RenewalNoticeDays` INT NOT NULL DEFAULT 30,
                    `RenewalCount` INT NOT NULL DEFAULT 0,
                    `CoverageModel` VARCHAR(50) NOT NULL COMMENT 'fixed_schedule, rotating...',
                    `FollowsCustomerCalendar` BOOLEAN NOT NULL DEFAULT TRUE,
                    `WorkOnPublicHolidays` BOOLEAN NOT NULL DEFAULT TRUE,
                    `WorkOnCustomerClosedDays` BOOLEAN NOT NULL DEFAULT TRUE,
                    `AutoGenerateShifts` BOOLEAN NOT NULL DEFAULT TRUE,
                    `GenerateShiftsAdvanceDays` INT NOT NULL DEFAULT 30,
                    `Status` VARCHAR(20) NOT NULL DEFAULT 'draft',
                    `ApprovedBy` CHAR(36) NULL,
                    `ApprovedAt` DATETIME NULL,
                    `ActivatedAt` DATETIME NULL,
                    `TerminationDate` DATE NULL,
                    `TerminationType` VARCHAR(50) NULL,
                    `TerminationReason` TEXT NULL,
                    `TerminatedBy` CHAR(36) NULL,
                    `ContractFileUrl` VARCHAR(500) NULL,
                    `SignedDate` DATE NULL,
                    `Notes` TEXT NULL,
                    `IsDeleted` BOOLEAN NOT NULL DEFAULT FALSE,
                    `CreatedAt` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    `UpdatedAt` DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
                    `CreatedBy` CHAR(36) NULL,
                    `UpdatedBy` CHAR(36) NULL,
                    INDEX `idx_contract_number` (`ContractNumber`),
                    INDEX `idx_contract_customer_status` (`CustomerId`, `Status`),
                    INDEX `idx_contract_type_status` (`ContractType`, `Status`),
                    INDEX `idx_contract_dates` (`StartDate`, `EndDate`),
                    INDEX `idx_contract_active` (`Status`, `EndDate`),
                    INDEX `idx_contract_document` (`DocumentId`),
                    FOREIGN KEY (`CustomerId`) REFERENCES `customers`(`Id`) ON DELETE RESTRICT,
                    FOREIGN KEY (`DocumentId`) REFERENCES `contract_documents`(`Id`) ON DELETE SET NULL
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
                COMMENT='Hợp đồng dịch vụ bảo vệ';
            ");

            // ====================================================================
            // 5. PUBLIC_HOLIDAYS - Ngày lễ quốc gia (MOVED AFTER CONTRACTS)
            // ====================================================================
            await connection.ExecuteAsync(@"
                CREATE TABLE IF NOT EXISTS `public_holidays` (
                    `Id` CHAR(36) PRIMARY KEY,
                    `ContractId` CHAR(36) NULL COMMENT 'Hợp đồng quy định ngày lễ này (nếu có)',
                    `HolidayDate` DATE UNIQUE NOT NULL,
                    `HolidayName` VARCHAR(200) NOT NULL,
                    `HolidayNameEn` VARCHAR(200) NULL,
                    `HolidayCategory` VARCHAR(50) NOT NULL COMMENT 'national, tet, regional, substitute',
                    `IsTetPeriod` BOOLEAN NOT NULL DEFAULT FALSE COMMENT 'Có phải ngày Tết không?',
                    `IsTetHoliday` BOOLEAN NOT NULL DEFAULT FALSE COMMENT 'Có phải kỳ nghỉ Tết chính (Tết Nguyên Đán) không?',
                    `TetDayNumber` INT NULL COMMENT '1=Mùng 1, 2=Mùng 2... (thường 1-5)',
                    `HolidayStartDate` DATE NULL COMMENT 'Ngày bắt đầu nghỉ (cho kỳ nghỉ dài ngày như Tết)',
                    `HolidayEndDate` DATE NULL COMMENT 'Ngày kết thúc nghỉ (cho kỳ nghỉ dài ngày như Tết)',
                    `TotalHolidayDays` INT NULL COMMENT 'Tổng số ngày nghỉ (bao gồm cả ngày nghỉ bù)',
                    `IsOfficialHoliday` BOOLEAN NOT NULL DEFAULT TRUE COMMENT 'Ngày nghỉ chính thức theo luật',
                    `IsObserved` BOOLEAN NOT NULL DEFAULT TRUE COMMENT 'Có được thực tế nghỉ không?',
                    `OriginalDate` DATE NULL COMMENT 'Ngày gốc (nếu bị dời do trùng cuối tuần)',
                    `ObservedDate` DATE NULL COMMENT 'Ngày thực tế nghỉ (sau khi dời)',
                    `AppliesNationwide` BOOLEAN NOT NULL DEFAULT TRUE,
                    `AppliesToRegions` VARCHAR(255) NULL COMMENT 'JSON array: [""TP.HCM"", ""Hà Nội""]',
                    `StandardWorkplacesClosed` BOOLEAN NOT NULL DEFAULT TRUE COMMENT 'Các công sở thường đóng cửa không?',
                    `EssentialServicesOperating` BOOLEAN NOT NULL DEFAULT TRUE COMMENT 'Dịch vụ thiết yếu vẫn hoạt động?',
                    `Description` TEXT NULL,
                    `Year` INT NOT NULL,
                    `CreatedAt` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    `UpdatedAt` DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
                    INDEX `idx_holiday_contract` (`ContractId`),
                    INDEX `idx_holiday_date` (`HolidayDate`),
                    INDEX `idx_holiday_year_cat` (`Year`, `HolidayCategory`),
                    INDEX `idx_holiday_tet` (`IsTetPeriod`, `Year`),
                    INDEX `idx_holiday_tet_main` (`IsTetHoliday`, `Year`),
                    INDEX `idx_holiday_date_range` (`HolidayStartDate`, `HolidayEndDate`),
                    FOREIGN KEY (`ContractId`) REFERENCES `contracts`(`Id`) ON DELETE CASCADE
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
                COMMENT='Ngày lễ quốc gia Việt Nam - bao gồm Tết và các ngày lễ khác';
            ");

            // ====================================================================
            // 6. HOLIDAY_SUBSTITUTE_WORK_DAYS - Ngày làm bù
            // ====================================================================
            await connection.ExecuteAsync(@"
                CREATE TABLE IF NOT EXISTS `holiday_substitute_work_days` (
                    `Id` CHAR(36) PRIMARY KEY,
                    `HolidayId` CHAR(36) NOT NULL,
                    `SubstituteDate` DATE NOT NULL,
                    `Reason` TEXT NULL,
                    `Year` INT NOT NULL,
                    `CreatedAt` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    INDEX `idx_substitute_holiday` (`HolidayId`),
                    INDEX `idx_substitute_date` (`SubstituteDate`),
                    FOREIGN KEY (`HolidayId`) REFERENCES `public_holidays`(`Id`) ON DELETE CASCADE
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
                COMMENT='Ngày làm bù cho ngày lễ';
            ");

            // ====================================================================
            // 7. CONTRACT_LOCATIONS - Địa điểm trong hợp đồng
            // ====================================================================
            await connection.ExecuteAsync(@"
                CREATE TABLE IF NOT EXISTS `contract_locations` (
                    `Id` CHAR(36) PRIMARY KEY,
                    `ContractId` CHAR(36) NOT NULL,
                    `LocationId` CHAR(36) NOT NULL,
                    `GuardsRequired` INT NOT NULL,
                    `CoverageType` VARCHAR(50) NOT NULL COMMENT '24x7, day_only...',
                    `ServiceStartDate` DATE NOT NULL,
                    `ServiceEndDate` DATE NULL,
                    `IsPrimaryLocation` BOOLEAN NOT NULL DEFAULT FALSE,
                    `PriorityLevel` INT NOT NULL DEFAULT 1,
                    `AutoGenerateShifts` BOOLEAN NOT NULL DEFAULT TRUE,
                    `IsActive` BOOLEAN NOT NULL DEFAULT TRUE,
                    `Notes` TEXT NULL,
                    `IsDeleted` BOOLEAN NOT NULL DEFAULT FALSE,
                    `CreatedAt` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    `UpdatedAt` DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
                    UNIQUE INDEX `uk_contract_location` (`ContractId`, `LocationId`),
                    INDEX `idx_location_active_contracts` (`LocationId`, `IsActive`),
                    FOREIGN KEY (`ContractId`) REFERENCES `contracts`(`Id`) ON DELETE CASCADE,
                    FOREIGN KEY (`LocationId`) REFERENCES `customer_locations`(`Id`) ON DELETE RESTRICT
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
                COMMENT='Địa điểm được cover bởi hợp đồng';
            ");

            // ====================================================================
            // 8. CONTRACT_SHIFT_SCHEDULES - Mẫu ca trong hợp đồng
            // ====================================================================
            await connection.ExecuteAsync(@"
                CREATE TABLE IF NOT EXISTS `contract_shift_schedules` (
                    `Id` CHAR(36) PRIMARY KEY,
                    `ContractId` CHAR(36) NOT NULL,
                    `LocationId` CHAR(36) NULL COMMENT 'Link trực tiếp đến customer_locations.Id',
                    `ScheduleName` VARCHAR(255) NOT NULL,
                    `ScheduleType` VARCHAR(50) NOT NULL DEFAULT 'regular',
                    `ShiftStartTime` TIME NOT NULL,
                    `ShiftEndTime` TIME NOT NULL,
                    `CrossesMidnight` BOOLEAN NOT NULL DEFAULT FALSE,
                    `DurationHours` DECIMAL(4,2) NOT NULL,
                    `BreakMinutes` INT NOT NULL DEFAULT 0,
                    `GuardsPerShift` INT NOT NULL,
                    `RecurrenceType` VARCHAR(50) NOT NULL DEFAULT 'weekly',
                    `AppliesMonday` BOOLEAN NOT NULL DEFAULT FALSE,
                    `AppliesTuesday` BOOLEAN NOT NULL DEFAULT FALSE,
                    `AppliesWednesday` BOOLEAN NOT NULL DEFAULT FALSE,
                    `AppliesThursday` BOOLEAN NOT NULL DEFAULT FALSE,
                    `AppliesFriday` BOOLEAN NOT NULL DEFAULT FALSE,
                    `AppliesSaturday` BOOLEAN NOT NULL DEFAULT FALSE,
                    `AppliesSunday` BOOLEAN NOT NULL DEFAULT FALSE,
                    `MonthlyDates` VARCHAR(100) NULL,
                    `AppliesOnPublicHolidays` BOOLEAN NOT NULL DEFAULT TRUE,
                    `AppliesOnCustomerHolidays` BOOLEAN NOT NULL DEFAULT TRUE,
                    `AppliesOnWeekends` BOOLEAN NOT NULL DEFAULT TRUE,
                    `SkipWhenLocationClosed` BOOLEAN NOT NULL DEFAULT FALSE,
                    `RequiresArmedGuard` BOOLEAN NOT NULL DEFAULT FALSE,
                    `RequiresSupervisor` BOOLEAN NOT NULL DEFAULT FALSE,
                    `MinimumExperienceMonths` INT NOT NULL DEFAULT 0,
                    `RequiredCertifications` VARCHAR(255) NULL,
                    `AutoGenerateEnabled` BOOLEAN NOT NULL DEFAULT TRUE,
                    `GenerateAdvanceDays` INT NOT NULL DEFAULT 30,
                    `EffectiveFrom` DATE NOT NULL,
                    `EffectiveTo` DATE NULL,
                    `IsActive` BOOLEAN NOT NULL DEFAULT TRUE,
                    `Notes` TEXT NULL,
                    `IsDeleted` BOOLEAN NOT NULL DEFAULT FALSE,
                    `CreatedAt` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    `UpdatedAt` DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
                    `CreatedBy` CHAR(36) NULL,
                    INDEX `idx_schedule_contract` (`ContractId`, `IsActive`),
                    INDEX `idx_schedule_location` (`LocationId`, `IsActive`),
                    INDEX `idx_schedule_dates` (`EffectiveFrom`, `EffectiveTo`),
                    FOREIGN KEY (`ContractId`) REFERENCES `contracts`(`Id`) ON DELETE CASCADE,
                    FOREIGN KEY (`LocationId`) REFERENCES `customer_locations`(`Id`) ON DELETE SET NULL
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
                COMMENT='Mẫu ca shift trong hợp đồng - để tự động tạo shifts';
            ");

            // ====================================================================
            // 9. CONTRACT_SHIFT_EXCEPTIONS - Ngoại lệ ca
            // ====================================================================
            await connection.ExecuteAsync(@"
                CREATE TABLE IF NOT EXISTS `contract_shift_exceptions` (
                    `Id` CHAR(36) PRIMARY KEY,
                    `ContractShiftScheduleId` CHAR(36) NOT NULL,
                    `ExceptionDate` DATE NOT NULL,
                    `ExceptionType` VARCHAR(50) NOT NULL COMMENT 'skip, modify, replace',
                    `Reason` VARCHAR(255) NULL,
                    `ModifiedStartTime` TIME NULL,
                    `ModifiedEndTime` TIME NULL,
                    `ModifiedGuardsCount` INT NULL,
                    `SpecialInstructions` TEXT NULL,
                    `CreatedBy` CHAR(36) NULL,
                    `CreatedAt` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    UNIQUE INDEX `uk_exception_schedule_date` (`ContractShiftScheduleId`, `ExceptionDate`),
                    FOREIGN KEY (`ContractShiftScheduleId`) REFERENCES `contract_shift_schedules`(`Id`) ON DELETE CASCADE
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
                COMMENT='Ngoại lệ ca - các trường hợp đặc biệt';
            ");

            // ====================================================================
            // 10. CONTRACT_PERIODS - Kỳ hạn hợp đồng
            // ====================================================================
            await connection.ExecuteAsync(@"
                CREATE TABLE IF NOT EXISTS `contract_periods` (
                    `Id` CHAR(36) PRIMARY KEY,
                    `ContractId` CHAR(36) NOT NULL,
                    `PeriodNumber` INT NOT NULL,
                    `PeriodType` VARCHAR(50) NOT NULL COMMENT 'initial, renewal, extension...',
                    `PeriodStartDate` DATE NOT NULL,
                    `PeriodEndDate` DATE NOT NULL,
                    `IsCurrentPeriod` BOOLEAN NOT NULL DEFAULT FALSE,
                    `Notes` TEXT NULL,
                    `CreatedAt` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    INDEX `idx_period_contract` (`ContractId`, `PeriodNumber`),
                    INDEX `idx_period_current` (`ContractId`, `IsCurrentPeriod`),
                    FOREIGN KEY (`ContractId`) REFERENCES `contracts`(`Id`) ON DELETE CASCADE
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
                COMMENT='Kỳ hạn hợp đồng - tracking renewals';
            ");

            // ====================================================================
            // 11. CONTRACT_AMENDMENTS - Phụ lục hợp đồng
            // ====================================================================
            await connection.ExecuteAsync(@"
                CREATE TABLE IF NOT EXISTS `contract_amendments` (
                    `Id` CHAR(36) PRIMARY KEY,
                    `ContractId` CHAR(36) NOT NULL,
                    `AmendmentNumber` VARCHAR(50) NOT NULL,
                    `AmendmentType` VARCHAR(50) NOT NULL,
                    `Description` TEXT NOT NULL,
                    `Reason` TEXT NULL,
                    `EffectiveDate` DATE NOT NULL,
                    `ChangesSummary` TEXT NULL COMMENT 'JSON object',
                    `Status` VARCHAR(20) NOT NULL DEFAULT 'draft',
                    `ApprovedBy` CHAR(36) NULL,
                    `ApprovedAt` DATETIME NULL,
                    `DocumentUrl` VARCHAR(500) NULL,
                    `IsDeleted` BOOLEAN NOT NULL DEFAULT FALSE,
                    `CreatedAt` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    `UpdatedAt` DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
                    `CreatedBy` CHAR(36) NULL,
                    INDEX `idx_amendment_contract` (`ContractId`, `EffectiveDate`),
                    FOREIGN KEY (`ContractId`) REFERENCES `contracts`(`Id`) ON DELETE CASCADE
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
                COMMENT='Phụ lục và sửa đổi hợp đồng';
            ");

            // ====================================================================
            // 12. SHIFT_GENERATION_LOG - Log tự động tạo ca
            // ====================================================================
            await connection.ExecuteAsync(@"
                CREATE TABLE IF NOT EXISTS `shift_generation_log` (
                    `Id` CHAR(36) PRIMARY KEY,
                    `ContractId` CHAR(36) NOT NULL,
                    `ContractShiftScheduleId` CHAR(36) NULL,
                    `GenerationDate` DATE NOT NULL,
                    `GeneratedAt` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    `ShiftsCreatedCount` INT NOT NULL DEFAULT 0,
                    `ShiftsSkippedCount` INT NOT NULL DEFAULT 0,
                    `SkipReasons` TEXT NULL COMMENT 'JSON array',
                    `Status` VARCHAR(20) NOT NULL,
                    `ErrorMessage` TEXT NULL,
                    `GeneratedByJob` VARCHAR(100) NULL,
                    INDEX `idx_gen_contract_date` (`ContractId`, `GenerationDate`),
                    INDEX `idx_gen_timestamp` (`GeneratedAt`),
                    FOREIGN KEY (`ContractId`) REFERENCES `contracts`(`Id`) ON DELETE CASCADE,
                    FOREIGN KEY (`ContractShiftScheduleId`) REFERENCES `contract_shift_schedules`(`Id`) ON DELETE SET NULL
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
                COMMENT='Log tự động tạo shifts - debugging và audit';
            ");

            // ====================================================================
            // 13. ATTENDANCE_SYNC_LOG - Log đồng bộ attendance
            // ====================================================================
            await connection.ExecuteAsync(@"
                CREATE TABLE IF NOT EXISTS `attendance_sync_log` (
                    `Id` CHAR(36) PRIMARY KEY,
                    `LocationId` CHAR(36) NOT NULL,
                    `SyncDate` DATE NOT NULL,
                    `TotalShifts` INT NULL,
                    `TotalCheckIns` INT NULL,
                    `TotalCheckOuts` INT NULL,
                    `SyncStatus` VARCHAR(20) NOT NULL DEFAULT 'pending',
                    `SyncedAt` DATETIME NULL,
                    `Notes` TEXT NULL,
                    INDEX `idx_attendance_location_date` (`LocationId`, `SyncDate`),
                    FOREIGN KEY (`LocationId`) REFERENCES `customer_locations`(`Id`) ON DELETE CASCADE
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
                COMMENT='Log đồng bộ attendance data';
            ");

            // ====================================================================
            // 14. CUSTOMER_SYNC_LOG - Log đồng bộ customer
            // ====================================================================
            Console.WriteLine("Creating table: customer_sync_log");
            await connection.ExecuteAsync(@"
                CREATE TABLE IF NOT EXISTS `customer_sync_log` (
                    `Id` CHAR(36) PRIMARY KEY,
                    `UserId` CHAR(36) NOT NULL,
                    `SyncType` VARCHAR(50) NOT NULL COMMENT 'CREATE | UPDATE | DELETE | ROLE_CHANGE',
                    `SyncStatus` VARCHAR(50) NOT NULL COMMENT 'SUCCESS | FAILED | PARTIAL',
                    `FieldsChanged` TEXT NULL COMMENT 'JSON array',
                    `OldValues` TEXT NULL COMMENT 'JSON object',
                    `NewValues` TEXT NULL COMMENT 'JSON object',
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
                    INDEX `idx_customer_sync_user` (`UserId`, `SyncType`),
                    INDEX `idx_customer_sync_status` (`SyncStatus`, `CreatedAt`),
                    INDEX `idx_customer_sync_date` (`CreatedAt`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
                COMMENT='Log đồng bộ customer từ Users Service';
            ");

                _tablesCreated = true;
                Console.WriteLine("✓ Created all 14 Contracts database tables successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error creating tables: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
