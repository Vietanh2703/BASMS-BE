using System.Data;

namespace Chats.API.Data;

public interface IDbConnectionFactory
{
    Task<IDbConnection> CreateConnectionAsync();
    Task EnsureTablesCreatedAsync();
    string GetConnectionString();
}
