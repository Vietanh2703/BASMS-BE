using System.Data;
using Dapper.Contrib.Extensions;
using BuildingBlocks.Exceptions;
using Users.API.Models;

namespace Users.API.Utilities;

/// <summary>
/// Database helper methods để giảm code lặp lại trong handlers
/// </summary>
public static class DatabaseHelpers
{
    /// <summary>
    /// Get user by ID với validation, tự động throw exception nếu không tìm thấy
    /// </summary>
    public static async Task<Models.Users> GetUserByIdOrThrowAsync(
        this IDbConnection connection,
        Guid userId,
        IDbTransaction? transaction = null,
        string? customErrorMessage = null)
    {
        var users = await connection.GetAllAsync<Models.Users>(transaction);
        var user = users.FirstOrDefault(u => u.Id == userId && !u.IsDeleted);

        if (user == null)
        {
            throw new NotFoundException(
                customErrorMessage ?? $"User with ID {userId} not found",
                "USER_NOT_FOUND");
        }

        return user;
    }

    /// <summary>
    /// Get user by Email với validation
    /// </summary>
    public static async Task<Models.Users> GetUserByEmailOrThrowAsync(
        this IDbConnection connection,
        string email,
        IDbTransaction? transaction = null,
        string? customErrorMessage = null)
    {
        var users = await connection.GetAllAsync<Models.Users>(transaction);
        var user = users.FirstOrDefault(u => u.Email == email && !u.IsDeleted);

        if (user == null)
        {
            throw new NotFoundException(
                customErrorMessage ?? $"User with email {email} not found",
                "USER_NOT_FOUND");
        }

        return user;
    }

    /// <summary>
    /// Get user by Firebase UID
    /// </summary>
    public static async Task<Models.Users?> GetUserByFirebaseUidAsync(
        this IDbConnection connection,
        string firebaseUid,
        IDbTransaction? transaction = null)
    {
        var users = await connection.GetAllAsync<Models.Users>(transaction);
        return users.FirstOrDefault(u => u.FirebaseUid == firebaseUid && !u.IsDeleted);
    }

    /// <summary>
    /// Check xem email đã tồn tại chưa (excluding specific user ID)
    /// </summary>
    public static async Task<bool> IsEmailExistsAsync(
        this IDbConnection connection,
        string email,
        Guid? excludeUserId = null,
        IDbTransaction? transaction = null)
    {
        var users = await connection.GetAllAsync<Models.Users>(transaction);
        return users.Any(u =>
            u.Email == email &&
            !u.IsDeleted &&
            (excludeUserId == null || u.Id != excludeUserId.Value));
    }

    /// <summary>
    /// Get role by ID với validation
    /// </summary>
    public static async Task<Roles> GetRoleByIdOrThrowAsync(
        this IDbConnection connection,
        Guid roleId,
        IDbTransaction? transaction = null)
    {
        var role = await connection.GetAsync<Roles>(roleId, transaction);

        if (role == null || role.IsDeleted)
        {
            throw new NotFoundException(
                $"Role with ID {roleId} not found",
                "ROLE_NOT_FOUND");
        }

        return role;
    }

    /// <summary>
    /// Get role by Name với validation
    /// </summary>
    public static async Task<Roles> GetRoleByNameOrThrowAsync(
        this IDbConnection connection,
        string roleName,
        IDbTransaction? transaction = null)
    {
        var roles = await connection.GetAllAsync<Roles>(transaction);
        var role = roles.FirstOrDefault(r => r.Name == roleName && !r.IsDeleted);

        if (role == null)
        {
            throw new NotFoundException(
                $"Role '{roleName}' not found",
                "ROLE_NOT_FOUND");
        }

        return role;
    }

    /// <summary>
    /// Get default role (guard) cho user mới
    /// </summary>
    public static async Task<Guid> GetDefaultRoleIdAsync(
        this IDbConnection connection,
        IDbTransaction? transaction = null)
    {
        var role = await connection.GetRoleByNameOrThrowAsync("guard", transaction);
        return role.Id;
    }

    /// <summary>
    /// Bulk get users by IDs
    /// </summary>
    public static async Task<IEnumerable<Models.Users>> GetUsersByIdsAsync(
        this IDbConnection connection,
        IEnumerable<Guid> userIds,
        IDbTransaction? transaction = null,
        bool includeDeleted = false)
    {
        var users = await connection.GetAllAsync<Models.Users>(transaction);
        var userIdList = userIds.ToList();

        return users.Where(u =>
            userIdList.Contains(u.Id) &&
            (includeDeleted || !u.IsDeleted));
    }

    /// <summary>
    /// Get users by role ID
    /// </summary>
    public static async Task<IEnumerable<Models.Users>> GetUsersByRoleIdAsync(
        this IDbConnection connection,
        Guid roleId,
        IDbTransaction? transaction = null,
        bool includeInactive = false)
    {
        var users = await connection.GetAllAsync<Models.Users>(transaction);

        return users.Where(u =>
            u.RoleId == roleId &&
            !u.IsDeleted &&
            (includeInactive || u.IsActive));
    }

    /// <summary>
    /// Soft delete user
    /// </summary>
    public static async Task SoftDeleteUserAsync(
        this IDbConnection connection,
        Models.Users user,
        IDbTransaction transaction)
    {
        user.IsDeleted = true;
        user.IsActive = false;
        user.Status = "deleted";
        user.UpdatedAt = DateTime.UtcNow;

        await connection.UpdateAsync(user, transaction);
    }

    /// <summary>
    /// Update user last login info
    /// </summary>
    public static async Task UpdateLastLoginAsync(
        this IDbConnection connection,
        Guid userId,
        IDbTransaction transaction)
    {
        var user = await connection.GetAsync<Models.Users>(userId, transaction);
        if (user != null)
        {
            user.LastLoginAt = DateTime.UtcNow;
            user.LoginCount++;
            user.UpdatedAt = DateTime.UtcNow;
            await connection.UpdateAsync(user, transaction);
        }
    }
}
