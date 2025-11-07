namespace Contracts.API.Models;

/// <summary>
/// PHỤ LỤC/SỬA ĐỔI HỢP ĐỒNG
/// Tracking các thay đổi sau khi hợp đồng đã ký
/// Ví dụ: Thêm địa điểm, thay đổi lịch ca, tăng số bảo vệ...
/// </summary>
[Table("contract_amendments")]
public class ContractAmendment
{
    [ExplicitKey]
    public Guid Id { get; set; }

    /// <summary>
    /// Thuộc hợp đồng nào
    /// </summary>
    public Guid ContractId { get; set; }

    /// <summary>
    /// Mã phụ lục: AMD-001, AMD-002...
    /// </summary>
    public string AmendmentNumber { get; set; } = string.Empty;

    /// <summary>
    /// Loại thay đổi: schedule_change, location_add, location_remove,
    /// scope_change, extension, staffing_change
    /// </summary>
    public string AmendmentType { get; set; } = string.Empty;

    /// <summary>
    /// Mô tả chi tiết thay đổi
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Lý do thay đổi
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// Ngày có hiệu lực
    /// </summary>
    public DateTime EffectiveDate { get; set; }

    /// <summary>
    /// Tóm tắt các thay đổi (JSON object)
    /// Ví dụ: {"location_added": "LOC-005", "guards_increased_from": 2, "guards_increased_to": 3}
    /// </summary>
    public string? ChangesSummary { get; set; }

    /// <summary>
    /// Trạng thái: draft, pending, approved, rejected, active
    /// </summary>
    public string Status { get; set; } = "draft";

    /// <summary>
    /// Người phê duyệt
    /// </summary>
    public Guid? ApprovedBy { get; set; }

    /// <summary>
    /// Thời điểm phê duyệt
    /// </summary>
    public DateTime? ApprovedAt { get; set; }

    /// <summary>
    /// URL file phụ lục PDF
    /// </summary>
    public string? DocumentUrl { get; set; }

    // ============================================================================
    // METADATA
    // ============================================================================

    public bool IsDeleted { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
}
