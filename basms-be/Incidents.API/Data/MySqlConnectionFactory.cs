using MySql.Data.MySqlClient;
using System.Data;
using Dapper;

namespace Incidents.API.Data;

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

        // Create incidents table
        var createIncidentsTable = @"
            CREATE TABLE IF NOT EXISTS incidents (
                Id CHAR(36) PRIMARY KEY,
                IncidentCode VARCHAR(50) NOT NULL UNIQUE,
                Title VARCHAR(500) NOT NULL,
                Description TEXT,
                IncidentType VARCHAR(50) NOT NULL,
                Severity VARCHAR(20) NOT NULL,
                IncidentTime DATETIME NOT NULL,
                Location VARCHAR(500) NOT NULL,
                ShiftLocation VARCHAR(500),
                ShiftId CHAR(36),
                ShiftAssignmentId CHAR(36),
                ReporterId CHAR(36) NOT NULL,
                ReporterName VARCHAR(255) NOT NULL,
                ReporterEmail VARCHAR(255) NOT NULL,
                ReporterRole VARCHAR(50),
                ReportedTime DATETIME NOT NULL,
                Status VARCHAR(50) NOT NULL DEFAULT 'REPORTED',
                ResponseContent TEXT,
                ResponderId CHAR(36),
                ResponderName VARCHAR(255),
                ResponderEmail VARCHAR(255),
                ResponderRole VARCHAR(50),
                RespondedAt DATETIME,
                IsDeleted BOOLEAN NOT NULL DEFAULT FALSE,
                CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                UpdatedAt DATETIME,
                CreatedBy CHAR(36),
                UpdatedBy CHAR(36),
                INDEX idx_reporter (ReporterId, ReportedTime),
                INDEX idx_shift (ShiftId),
                INDEX idx_status (Status, Severity),
                INDEX idx_incident_time (IncidentTime)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        ";

        // Create incident_media table
        var createIncidentMediaTable = @"
            CREATE TABLE IF NOT EXISTS incident_media (
                Id CHAR(36) PRIMARY KEY,
                IncidentId CHAR(36) NOT NULL,
                MediaType VARCHAR(50) NOT NULL,
                FileUrl VARCHAR(1000) NOT NULL,
                FileName VARCHAR(500) NOT NULL,
                FileSize BIGINT,
                MimeType VARCHAR(100),
                ThumbnailUrl VARCHAR(1000),
                Caption VARCHAR(500),
                DisplayOrder INT,
                UploadedBy CHAR(36),
                UploadedByName VARCHAR(255),
                IsDeleted BOOLEAN NOT NULL DEFAULT FALSE,
                CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                UpdatedAt DATETIME,
                INDEX idx_incident (IncidentId),
                INDEX idx_uploaded_by (UploadedBy)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        ";

        await connection.ExecuteAsync(createIncidentsTable);
        await connection.ExecuteAsync(createIncidentMediaTable);

        _tablesCreated = true;
        Console.WriteLine("✅ Incidents.API database tables created/verified successfully");
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
