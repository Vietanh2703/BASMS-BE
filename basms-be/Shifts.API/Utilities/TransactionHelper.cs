namespace Shifts.API.Utilities;

public static class TransactionHelper
{
    public static async Task<TResult> ExecuteInTransactionAsync<TResult>(
        this IDbConnectionFactory connectionFactory,
        Func<IDbConnection, IDbTransaction, Task<TResult>> action,
        ILogger? logger = null,
        string? operationName = null)
    {
        using var connection = await connectionFactory.CreateConnectionAsync();
        using var transaction = connection.BeginTransaction();

        try
        {
            var result = await action(connection, transaction);
            transaction.Commit();

            logger?.LogDebug("Transaction committed successfully{Operation}",
                operationName != null ? $" for {operationName}" : "");

            return result;
        }
        catch (Exception ex)
        {
            transaction.Rollback();

            logger?.LogWarning(ex, "Transaction rolled back{Operation}",
                operationName != null ? $" for {operationName}" : "");

            throw;
        }
    }
    
    public static async Task ExecuteInTransactionAsync(
        this IDbConnectionFactory connectionFactory,
        Func<IDbConnection, IDbTransaction, Task> action,
        ILogger? logger = null,
        string? operationName = null)
    {
        using var connection = await connectionFactory.CreateConnectionAsync();
        using var transaction = connection.BeginTransaction();

        try
        {
            await action(connection, transaction);
            transaction.Commit();

            logger?.LogDebug("Transaction committed successfully{Operation}",
                operationName != null ? $" for {operationName}" : "");
        }
        catch (Exception ex)
        {
            transaction.Rollback();

            logger?.LogWarning(ex, "Transaction rolled back{Operation}",
                operationName != null ? $" for {operationName}" : "");

            throw;
        }
    }
    
    public static async Task<TResult> ExecuteQueryAsync<TResult>(
        this IDbConnectionFactory connectionFactory,
        Func<IDbConnection, Task<TResult>> query,
        ILogger? logger = null,
        string? operationName = null)
    {
        try
        {
            using var connection = await connectionFactory.CreateConnectionAsync();
            var result = await query(connection);

            logger?.LogDebug("Query executed successfully{Operation}",
                operationName != null ? $" for {operationName}" : "");

            return result;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Query failed{Operation}",
                operationName != null ? $" for {operationName}" : "");

            throw;
        }
    }
}
