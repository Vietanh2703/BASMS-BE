using System.Data;
using System.Text.Json;
using Dapper.Contrib.Extensions;
using Users.API.Models;

namespace Users.API.Utilities;

/// <summary>
/// Helper class để tạo audit logs một cách clean và consistent
/// </summary>
public static class AuditLogHelper
{
    /// <summary>
    /// Log audit cho action với old values và new values
    /// </summary>
    public static async Task LogAuditAsync(
        this IDbConnection connection,
        IDbTransaction transaction,
        Guid userId,
        string action,
        string entityType,
        Guid entityId,
        object? oldValues = null,
        object? newValues = null,
        string? ipAddress = null,
        string? userAgent = null,
        string? deviceId = null)
    {
        var auditLog = new AuditLogs
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            OldValues = oldValues != null ? JsonSerializer.Serialize(oldValues) : null,
            NewValues = newValues != null ? JsonSerializer.Serialize(newValues) : null,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            DeviceId = deviceId,
            Status = "success",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await connection.InsertAsync(auditLog, transaction);
    }

    /// <summary>
    /// Log audit cho CREATE_USER action
    /// </summary>
    public static async Task LogUserCreatedAsync(
        this IDbConnection connection,
        IDbTransaction transaction,
        Models.Users user,
        string? ipAddress = null)
    {
        await connection.LogAuditAsync(
            transaction,
            userId: user.Id,
            action: "CREATE_USER",
            entityType: "User",
            entityId: user.Id,
            newValues: new
            {
                user.IdentityNumber,
                user.Email,
                user.FullName,
                user.RoleId,
                user.Status,
                user.FirebaseUid
            },
            ipAddress: ipAddress
        );
    }

    /// <summary>
    /// Log audit cho UPDATE_USER action
    /// </summary>
    public static async Task LogUserUpdatedAsync(
        this IDbConnection connection,
        IDbTransaction transaction,
        Guid userId,
        object oldValues,
        object newValues,
        string? ipAddress = null)
    {
        await connection.LogAuditAsync(
            transaction,
            userId: userId,
            action: "UPDATE_USER",
            entityType: "User",
            entityId: userId,
            oldValues: oldValues,
            newValues: newValues,
            ipAddress: ipAddress
        );
    }

    /// <summary>
    /// Log audit cho DELETE_USER action
    /// </summary>
    public static async Task LogUserDeletedAsync(
        this IDbConnection connection,
        IDbTransaction transaction,
        Models.Users user,
        string? ipAddress = null)
    {
        await connection.LogAuditAsync(
            transaction,
            userId: user.Id,
            action: "DELETE_USER",
            entityType: "User",
            entityId: user.Id,
            oldValues: new
            {
                user.Email,
                user.FullName,
                user.FirebaseUid,
                user.Status,
                user.IsActive,
                IsDeleted = false
            },
            newValues: new
            {
                user.Email,
                user.FullName,
                user.FirebaseUid,
                Status = "deleted",
                IsActive = false,
                IsDeleted = true
            },
            ipAddress: ipAddress
        );
    }

    /// <summary>
    /// Log audit cho LOGIN action
    /// </summary>
    public static async Task LogUserLoginAsync(
        this IDbConnection connection,
        IDbTransaction transaction,
        Guid userId,
        string? ipAddress = null,
        string? userAgent = null,
        string? deviceId = null)
    {
        await connection.LogAuditAsync(
            transaction,
            userId: userId,
            action: "LOGIN",
            entityType: "User",
            entityId: userId,
            ipAddress: ipAddress,
            userAgent: userAgent,
            deviceId: deviceId
        );
    }

    /// <summary>
    /// Log audit cho LOGOUT action
    /// </summary>
    public static async Task LogUserLogoutAsync(
        this IDbConnection connection,
        IDbTransaction transaction,
        Guid userId,
        string? ipAddress = null)
    {
        await connection.LogAuditAsync(
            transaction,
            userId: userId,
            action: "LOGOUT",
            entityType: "User",
            entityId: userId,
            ipAddress: ipAddress
        );
    }

    /// <summary>
    /// Log audit cho PASSWORD_CHANGE action
    /// </summary>
    public static async Task LogPasswordChangedAsync(
        this IDbConnection connection,
        IDbTransaction transaction,
        Guid userId,
        string? ipAddress = null)
    {
        await connection.LogAuditAsync(
            transaction,
            userId: userId,
            action: "PASSWORD_CHANGE",
            entityType: "User",
            entityId: userId,
            ipAddress: ipAddress
        );
    }

    /// <summary>
    /// Log audit cho PASSWORD_RESET action
    /// </summary>
    public static async Task LogPasswordResetAsync(
        this IDbConnection connection,
        IDbTransaction transaction,
        Guid userId,
        string? ipAddress = null)
    {
        await connection.LogAuditAsync(
            transaction,
            userId: userId,
            action: "PASSWORD_RESET",
            entityType: "User",
            entityId: userId,
            ipAddress: ipAddress
        );
    }
}
