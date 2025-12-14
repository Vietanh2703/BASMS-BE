using MySql.Data.MySqlClient;
using System.Data;
using Dapper;

namespace Chats.API.Data;

public class MySqlConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private bool _tablesCreated;

    public MySqlConnectionFactory(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
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

        // Create conversations table
        var createConversationsTable = @"
            CREATE TABLE IF NOT EXISTS conversations (
                Id CHAR(36) PRIMARY KEY,
                ConversationType VARCHAR(50) NOT NULL,
                ConversationName VARCHAR(500),
                ShiftId CHAR(36),
                IncidentId CHAR(36),
                TeamId CHAR(36),
                ContractId CHAR(36),
                IsActive BOOLEAN NOT NULL DEFAULT TRUE,
                LastMessageAt DATETIME,
                LastMessagePreview VARCHAR(200),
                LastMessageSenderId CHAR(36),
                LastMessageSenderName VARCHAR(255),
                IsDeleted BOOLEAN NOT NULL DEFAULT FALSE,
                CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                UpdatedAt DATETIME,
                CreatedBy CHAR(36),
                INDEX idx_shift (ShiftId),
                INDEX idx_incident (IncidentId),
                INDEX idx_team (TeamId),
                INDEX idx_last_message (LastMessageAt)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        ";

        // Create conversation_participants table
        var createParticipantsTable = @"
            CREATE TABLE IF NOT EXISTS conversation_participants (
                Id CHAR(36) PRIMARY KEY,
                ConversationId CHAR(36) NOT NULL,
                UserId CHAR(36) NOT NULL,
                UserName VARCHAR(255) NOT NULL,
                UserAvatarUrl VARCHAR(1000),
                UserRole VARCHAR(50),
                Role VARCHAR(50) NOT NULL DEFAULT 'MEMBER',
                JoinedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                LeftAt DATETIME,
                IsActive BOOLEAN NOT NULL DEFAULT TRUE,
                IsMuted BOOLEAN NOT NULL DEFAULT FALSE,
                MutedUntil DATETIME,
                LastReadMessageId CHAR(36),
                LastReadAt DATETIME,
                CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                UpdatedAt DATETIME,
                AddedBy CHAR(36),
                INDEX idx_conversation (ConversationId, IsActive),
                INDEX idx_user (UserId, IsActive),
                UNIQUE KEY unique_user_conversation (ConversationId, UserId)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        ";

        // Create messages table
        var createMessagesTable = @"
            CREATE TABLE IF NOT EXISTS messages (
                Id CHAR(36) PRIMARY KEY,
                ConversationId CHAR(36) NOT NULL,
                SenderId CHAR(36) NOT NULL,
                SenderName VARCHAR(255) NOT NULL,
                SenderAvatarUrl VARCHAR(1000),
                MessageType VARCHAR(50) NOT NULL DEFAULT 'TEXT',
                Content TEXT,
                FileUrl VARCHAR(1000),
                FileName VARCHAR(500),
                FileSize BIGINT,
                FileType VARCHAR(100),
                ThumbnailUrl VARCHAR(1000),
                Latitude DECIMAL(10,8),
                Longitude DECIMAL(11,8),
                LocationAddress VARCHAR(500),
                LocationMapUrl VARCHAR(1000),
                ReplyToMessageId CHAR(36),
                IsEdited BOOLEAN NOT NULL DEFAULT FALSE,
                EditedAt DATETIME,
                IsDeleted BOOLEAN NOT NULL DEFAULT FALSE,
                DeletedAt DATETIME,
                DeletedBy CHAR(36),
                CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                UpdatedAt DATETIME,
                INDEX idx_conversation (ConversationId, CreatedAt),
                INDEX idx_sender (SenderId),
                INDEX idx_reply (ReplyToMessageId)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        ";

        // Create message_read_receipts table
        var createReadReceiptsTable = @"
            CREATE TABLE IF NOT EXISTS message_read_receipts (
                Id CHAR(36) PRIMARY KEY,
                MessageId CHAR(36) NOT NULL,
                UserId CHAR(36) NOT NULL,
                ConversationId CHAR(36) NOT NULL,
                UserName VARCHAR(255) NOT NULL,
                UserAvatarUrl VARCHAR(1000),
                ReadAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                ReadFrom VARCHAR(50),
                CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                INDEX idx_message (MessageId),
                INDEX idx_user (UserId, ReadAt),
                INDEX idx_conversation (ConversationId),
                UNIQUE KEY unique_message_user (MessageId, UserId)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        ";

        await connection.ExecuteAsync(createConversationsTable);
        await connection.ExecuteAsync(createParticipantsTable);
        await connection.ExecuteAsync(createMessagesTable);
        await connection.ExecuteAsync(createReadReceiptsTable);

        _tablesCreated = true;
        Console.WriteLine("✅ Chats.API database tables created/verified successfully");
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
