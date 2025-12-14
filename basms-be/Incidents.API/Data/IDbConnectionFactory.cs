using System.Data;

namespace Incidents.API.Data;

public interface IDbConnectionFactory
{
    Task<IDbConnection> CreateConnectionAsync();
    string GetConnectionString();
}
