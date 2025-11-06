public interface IDbConnectionFactory
{
    Task<IDbConnection> CreateConnectionAsync();
    Task EnsureTablesCreatedAsync();
}