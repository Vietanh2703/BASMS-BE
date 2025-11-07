namespace Contracts.API.Models;

/// <summary>
/// CACHE CUSTOMER TỪ USERS SERVICE
/// Lưu trữ thông tin customer được sync từ Users.API
/// Tránh phải gọi HTTP call mỗi lần cần thông tin customer
/// </summary>
[Table("customer_cache")]
public class CustomerCache
{
    [ExplicitKey]
    public Guid Id { get; set; }

    /// <summary>
    /// User ID từ Users Service (same as Customer.UserId)
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Firebase UID
    /// </summary>
    public string FirebaseUid { get; set; } = string.Empty;

    /// <summary>
    /// Email đăng nhập
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Tên đầy đủ
    /// </summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// Số điện thoại
    /// </summary>
    public string? Phone { get; set; }

    /// <summary>
    /// Avatar URL
    /// </summary>
    public string? AvatarUrl { get; set; }

    /// <summary>
    /// Role ID từ Users Service
    /// </summary>
    public Guid RoleId { get; set; }

    /// <summary>
    /// Role name: "customer"
    /// </summary>
    public string RoleName { get; set; } = string.Empty;

    // ============================================================================
    // CUSTOMER SPECIFIC INFO
    // ============================================================================

    /// <summary>
    /// Tên công ty (nếu có trong user profile)
    /// </summary>
    public string? CompanyName { get; set; }

    /// <summary>
    /// Địa chỉ công ty
    /// </summary>
    public string? Address { get; set; }

    /// <summary>
    /// Ngành nghề
    /// </summary>
    public string? Industry { get; set; }

    // ============================================================================
    // SYNC METADATA
    // ============================================================================

    /// <summary>
    /// Lần sync cuối
    /// </summary>
    public DateTime? LastSyncedAt { get; set; }

    /// <summary>
    /// Trạng thái sync: SYNCED | PENDING | FAILED
    /// </summary>
    public string SyncStatus { get; set; } = "SYNCED";

    /// <summary>
    /// Version từ User Service (để track changes)
    /// </summary>
    public int? UserServiceVersion { get; set; }

    /// <summary>
    /// User có active không?
    /// </summary>
    public bool IsActive { get; set; } = true;

    // ============================================================================
    // AUDIT
    // ============================================================================

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
