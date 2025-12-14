using System.Data;

namespace Chats.API.Data;

public interface IDbConnectionFactory
{
    Task<IDbConnection> CreateConnectionAsync();
    string GetConnectionString();
}
