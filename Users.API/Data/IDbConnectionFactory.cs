namespace Users.API.Data;

public interface IDbConnectionFactory
{
    Task<IDbConnection> CreateConnectionAsync();
    Task EnsureTablesCreatedAsync();
}
