namespace Attendances.API.Data;

public interface IDbConnectionFactory
{
    Task<IDbConnection> CreateConnectionAsync();

    string GetConnectionString();
}
