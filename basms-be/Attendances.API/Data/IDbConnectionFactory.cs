using System.Data;

namespace Attendances.API.Data;

/// <summary>
/// Factory interface để tạo database connections
/// Mục đích: Abstraction layer cho database access, dễ test và switch database
/// </summary>
public interface IDbConnectionFactory
{
    /// <summary>
    /// Tạo và mở connection mới đến database
    /// </summary>
    /// <returns>IDbConnection đã được mở sẵn</returns>
    Task<IDbConnection> CreateConnectionAsync();

    /// <summary>
    /// Lấy connection string hiện tại (dùng cho logging/debugging)
    /// </summary>
    string GetConnectionString();
}
