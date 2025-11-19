namespace Users.API.Data;

public class MySqlConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;
    private bool _tablesCreated = false;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

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

            using var connection = await CreateConnectionAsync();

            // Check if users table exists
            var tableExists = await connection.ExecuteScalarAsync<bool>(
                "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'users'");

            if (!tableExists)
            {
                // Create users table
                await connection.ExecuteAsync(@"
                    CREATE TABLE `users` (
                        `Id` CHAR(36) PRIMARY KEY,
                        `FirebaseUid` VARCHAR(255) NOT NULL UNIQUE,
                        `IdentityNumber` VARCHAR(20) NOT NULL UNIQUE,
                        `IdentityIssueDate` DATETIME NULL,
                        `IdentityIssuePlace` VARCHAR(255) NULL,
                        `Email` VARCHAR(255) NOT NULL UNIQUE,
                        `EmailVerified` BOOLEAN DEFAULT FALSE,
                        `EmailVerifiedAt` DATETIME NULL,
                        `FullName` VARCHAR(255) NOT NULL,
                        `AvatarUrl` VARCHAR(500) NULL,
                        `Gender` VARCHAR(10) NULL,
                        `Phone` VARCHAR(20) NULL,
                        `Address` TEXT NULL,
                        `BirthDay` INT NULL,
                        `BirthMonth` INT NULL,
                        `BirthYear` INT NULL,
                        `RoleId` CHAR(36) NOT NULL,
                        `AuthProvider` VARCHAR(50) NOT NULL DEFAULT 'email',
                        `Status` VARCHAR(50) NOT NULL DEFAULT 'active',
                        `LastLoginAt` DATETIME NULL,
                        `LoginCount` INT NOT NULL DEFAULT 0,
                        `IsDeleted` BOOLEAN NOT NULL DEFAULT FALSE,
                        `IsActive` BOOLEAN NOT NULL DEFAULT TRUE,
                        `Password` VARCHAR(255) NOT NULL,
                        `CreatedAt` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        `UpdatedAt` DATETIME NULL,
                        `CreatedBy` CHAR(36) NULL,
                        `UpdatedBy` CHAR(36) NULL,
                        INDEX `idx_email` (`Email`),
                        INDEX `idx_firebase_uid` (`FirebaseUid`),
                        INDEX `idx_role_id` (`RoleId`),
                        INDEX `idx_is_deleted` (`IsDeleted`)
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
                ");

                // Create roles table if not exists
                var rolesTableExists = await connection.ExecuteScalarAsync<bool>(
                    "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'roles'");

                if (!rolesTableExists)
                {
                    await connection.ExecuteAsync(@"
                        CREATE TABLE `roles` (
                            `Id` CHAR(36) PRIMARY KEY,
                            `Name` VARCHAR(100) NOT NULL UNIQUE,
                            `DisplayName` VARCHAR(255) NOT NULL,
                            `Description` TEXT NULL,
                            `Permissions` JSON NULL,
                            `IsDeleted` BOOLEAN NOT NULL DEFAULT FALSE,
                            `CreatedAt` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                            `UpdatedAt` DATETIME NULL,
                            `CreatedBy` CHAR(36) NULL,
                            `UpdatedBy` CHAR(36) NULL,
                            INDEX `idx_name` (`Name`)
                        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
                    ");

                    // Insert default roles
                    await connection.ExecuteAsync(@"
                        INSERT INTO `roles` (`Id`, `Name`, `DisplayName`, `Description`, `IsDeleted`, `CreatedAt`)
                        VALUES
                            ('ddbd5bad-ba6e-11f0-bcac-00155dca8f48', 'admin', 'Administrator', 'System administrator with full access', FALSE, NOW()),
                            ('ddbd5fad-ba6e-11f0-bcac-00155dca8f48', 'director', 'Director', 'Giám đốc công ty bảo vệ với quyền quản lý toàn bộ hệ thống', FALSE, NOW()),
                            ('ddbd612f-ba6e-11f0-bcac-00155dca8f48', 'manager', 'Manager', 'Quản lý công ty bảo vệ với quyền điều hành nhân sự và dự án', FALSE, NOW()),
                            ('ddbd6230-ba6e-11f0-bcac-00155dca8f48', 'guard', 'Guard', 'Nhân viên bảo vệ thực hiện nhiệm vụ bảo vệ', FALSE, NOW()),
                            ('ddbd630a-ba6e-11f0-bcac-00155dca8f48', 'customer', 'Customer', 'Đối tác thuê dịch vụ bảo vệ (nhà hàng, trường học, ...)', FALSE, NOW());
                    ");
                }

                // Create audit_logs table if not exists
                var auditLogsTableExists = await connection.ExecuteScalarAsync<bool>(
                    "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'audit_logs'");

                if (!auditLogsTableExists)
                {
                    await connection.ExecuteAsync(@"
                        CREATE TABLE `audit_logs` (
                            `Id` CHAR(36) PRIMARY KEY,
                            `UserId` CHAR(36) NULL,
                            `Action` VARCHAR(100) NOT NULL,
                            `EntityType` VARCHAR(100) NULL,
                            `EntityId` CHAR(36) NULL,
                            `OldValues` JSON NULL,
                            `NewValues` JSON NULL,
                            `IpAddress` VARCHAR(50) NULL,
                            `UserAgent` TEXT NULL,
                            `DeviceId` VARCHAR(255) NULL,
                            `Status` VARCHAR(50) NOT NULL DEFAULT 'success',
                            `ErrorMessage` TEXT NULL,
                            `IsDeleted` BOOLEAN NOT NULL DEFAULT FALSE,
                            `CreatedAt` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                            `UpdatedAt` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                            INDEX `idx_user_id` (`UserId`),
                            INDEX `idx_action` (`Action`),
                            INDEX `idx_entity` (`EntityType`, `EntityId`),
                            INDEX `idx_created_at` (`CreatedAt`)
                        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
                    ");
                }

                // Create otp_logs table if not exists
                var otpLogsTableExists = await connection.ExecuteScalarAsync<bool>(
                    "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'otp_logs'");

                if (!otpLogsTableExists)
                {
                    await connection.ExecuteAsync(@"
                        CREATE TABLE `otp_logs` (
                            `Id` CHAR(36) PRIMARY KEY,
                            `UserId` CHAR(36) NOT NULL,
                            `OtpCode` VARCHAR(10) NOT NULL,
                            `Purpose` VARCHAR(50) NOT NULL,
                            `DeliveryMethod` VARCHAR(20) NOT NULL DEFAULT 'email',
                            `ExpiresAt` DATETIME NOT NULL,
                            `IsUsed` BOOLEAN NOT NULL DEFAULT FALSE,
                            `UsedAt` DATETIME NULL,
                            `AttemptCount` INT NOT NULL DEFAULT 0,
                            `LastAttemptAt` DATETIME NULL,
                            `IsExpired` BOOLEAN NOT NULL DEFAULT FALSE,
                            `IpAddress` VARCHAR(50) NULL,
                            `UserAgent` TEXT NULL,
                            `IsDeleted` BOOLEAN NOT NULL DEFAULT FALSE,
                            `CreatedAt` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                            `UpdatedAt` DATETIME NULL,
                            INDEX `idx_user_id` (`UserId`),
                            INDEX `idx_otp_code` (`OtpCode`),
                            INDEX `idx_purpose` (`Purpose`),
                            INDEX `idx_expires_at` (`ExpiresAt`),
                            INDEX `idx_is_used` (`IsUsed`),
                            INDEX `idx_is_expired` (`IsExpired`),
                            FOREIGN KEY (`UserId`) REFERENCES `users`(`Id`) ON DELETE CASCADE
                        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
                    ");
                }
                
                // Create refresh_token table if not exists
                var refreshTokensTableExists = await connection.ExecuteScalarAsync<bool>(
                    "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'refresh_tokens'");

                if (!refreshTokensTableExists)
                {
                    await connection.ExecuteAsync(@"
                        CREATE TABLE `refresh_tokens` (
                            `Id` CHAR(36) PRIMARY KEY,
                            `UserId` CHAR(36) NOT NULL,
                            `Token` TEXT NOT NULL,
                            `ExpiresAt` DATETIME NOT NULL,
                            `IsRevoked` BOOLEAN NOT NULL DEFAULT FALSE,
                            `RevokedAt` DATETIME NULL,
                            `RevokedByIp` VARCHAR(50) NULL,
                            `ReplacedByToken` TEXT NULL,
                            `IpAddress` VARCHAR(50) NULL,
                            `UserAgent` TEXT NULL,
                            `DeviceId` VARCHAR(255) NULL,
                            `IsDeleted` BOOLEAN NOT NULL DEFAULT FALSE,
                            `CreatedAt` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                            `UpdatedAt` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                            INDEX `idx_user_id` (`UserId`),
                            INDEX `idx_token` (`Token`(255)),
                            INDEX `idx_expires_at` (`ExpiresAt`),
                            INDEX `idx_is_revoked` (`IsRevoked`),
                            FOREIGN KEY (`UserId`) REFERENCES `users`(`Id`) ON DELETE CASCADE,
                            UNIQUE INDEX `uniq_token` (`Token`(255))
                        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
                    ");
                }
                

                // Create user_sessions table if not exists
                var userSessionsTableExists = await connection.ExecuteScalarAsync<bool>(
                    "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'user_sessions'");

                if (!userSessionsTableExists)
                {
                    await connection.ExecuteAsync(@"
                        CREATE TABLE `user_sessions` (
                            `Id` CHAR(36) PRIMARY KEY,
                            `UserId` CHAR(36) NOT NULL,
                            `SessionToken` TEXT NOT NULL,
                            `DeviceId` VARCHAR(255) NULL,
                            `IpAddress` VARCHAR(50) NULL,
                            `UserAgent` TEXT NULL,
                            `IsActive` BOOLEAN NOT NULL DEFAULT TRUE,
                            `ExpiresAt` DATETIME NOT NULL,
                            `LastActivityAt` DATETIME NOT NULL,
                            `IsDeleted` BOOLEAN NOT NULL DEFAULT FALSE,
                            `CreatedAt` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                            `UpdatedAt` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                            INDEX `idx_user_id` (`UserId`),
                            INDEX `idx_session_token` (`SessionToken`(255)),
                            INDEX `idx_is_active` (`IsActive`),
                            INDEX `idx_expires_at` (`ExpiresAt`),
                            FOREIGN KEY (`UserId`) REFERENCES `users`(`Id`) ON DELETE CASCADE,
                            UNIQUE INDEX `uniq_session_token` (`SessionToken`(255))
                        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
                    ");
                }

                // Create user_tokens table if not exists
                var userTokensTableExists = await connection.ExecuteScalarAsync<bool>(
                    "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'user_tokens'");

                if (!userTokensTableExists)
                {
                    await connection.ExecuteAsync(@"
                        CREATE TABLE `user_tokens` (
                            `Id` CHAR(36) PRIMARY KEY,
                            `UserId` CHAR(36) NOT NULL,
                            `Token` TEXT NOT NULL,
                            `TokenType` VARCHAR(50) NOT NULL DEFAULT 'access_token',
                            `ExpiresAt` DATETIME NOT NULL,
                            `IsRevoked` BOOLEAN NOT NULL DEFAULT FALSE,
                            `RevokedAt` DATETIME NULL,
                            `RevokedReason` VARCHAR(255) NULL,
                            `IpAddress` VARCHAR(50) NULL,
                            `UserAgent` TEXT NULL,
                            `DeviceId` VARCHAR(255) NULL,
                            `IsDeleted` BOOLEAN NOT NULL DEFAULT FALSE,
                            `CreatedAt` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                            `UpdatedAt` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                            INDEX `idx_user_id` (`UserId`),
                            INDEX `idx_token` (`Token`(255)),
                            INDEX `idx_token_type` (`TokenType`),
                            INDEX `idx_expires_at` (`ExpiresAt`),
                            INDEX `idx_is_revoked` (`IsRevoked`),
                            FOREIGN KEY (`UserId`) REFERENCES `users`(`Id`) ON DELETE CASCADE,
                            UNIQUE INDEX `uniq_token` (`Token`(255))
                        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
                    ");
                }

                // Create password_reset_tokens table if not exists
                var passwordResetTokensTableExists = await connection.ExecuteScalarAsync<bool>(
                    "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'password_reset_tokens'");

                if (!passwordResetTokensTableExists)
                {
                    await connection.ExecuteAsync(@"
                        CREATE TABLE `password_reset_tokens` (
                            `Id` CHAR(36) PRIMARY KEY,
                            `UserId` CHAR(36) NOT NULL,
                            `Token` TEXT NOT NULL,
                            `ExpiresAt` DATETIME NOT NULL,
                            `IsUsed` BOOLEAN NOT NULL DEFAULT FALSE,
                            `UsedAt` DATETIME NULL,
                            `IpAddress` VARCHAR(50) NULL,
                            `UserAgent` TEXT NULL,
                            `IsDeleted` BOOLEAN NOT NULL DEFAULT FALSE,
                            `CreatedAt` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                            `UpdatedAt` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                            INDEX `idx_user_id` (`UserId`),
                            INDEX `idx_token` (`Token`(255)),
                            INDEX `idx_expires_at` (`ExpiresAt`),
                            INDEX `idx_is_used` (`IsUsed`),
                            FOREIGN KEY (`UserId`) REFERENCES `users`(`Id`) ON DELETE CASCADE,
                            UNIQUE INDEX `uniq_token` (`Token`(255))
                        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
                    ");
                }
            }

            _tablesCreated = true;
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
