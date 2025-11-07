namespace Contracts.API.Models;

/// <summary>
/// HỢP ĐỒNG DỊCH VỤ BẢO VỆ
/// Model quan trọng nhất - chứa business logic về dịch vụ
/// Mỗi hợp đồng có thể cover nhiều locations
/// </summary>
[Table("contracts")]
public class Contract
{
    [ExplicitKey]
    public Guid Id { get; set; }

    /// <summary>
    /// Thuộc khách hàng nào
    /// </summary>
    public Guid CustomerId { get; set; }

    /// <summary>
    /// Mã hợp đồng: CTR-2025-001
    /// </summary>
    public string ContractNumber { get; set; } = string.Empty;

    /// <summary>
    /// Tiêu đề hợp đồng: "Dịch vụ bảo vệ 24/7 tại Nhà máy ABC"
    /// </summary>
    public string ContractTitle { get; set; } = string.Empty;

    // ============================================================================
    // PHÂN LOẠI HỢP ĐỒNG
    // ============================================================================

    /// <summary>
    /// Loại hợp đồng: long_term, short_term, trial, event_based, emergency, seasonal
    /// </summary>
    public string ContractType { get; set; } = string.Empty;

    /// <summary>
    /// Phạm vi dịch vụ: continuous_24x7, shift_based, event_only, on_demand, hybrid
    /// </summary>
    public string ServiceScope { get; set; } = string.Empty;

    // ============================================================================
    // THỜI HẠN HỢP ĐỒNG
    // ============================================================================

    /// <summary>
    /// Ngày bắt đầu: 2025-01-01
    /// </summary>
    public DateTime StartDate { get; set; }

    /// <summary>
    /// Ngày kết thúc: 2025-12-31
    /// </summary>
    public DateTime EndDate { get; set; }

    /// <summary>
    /// Thời hạn tính theo tháng: 12 tháng
    /// </summary>
    public int DurationMonths { get; set; }

    // ============================================================================
    // GIA HẠN HỢP ĐỒNG
    // ============================================================================

    /// <summary>
    /// Có thể gia hạn không?
    /// </summary>
    public bool IsRenewable { get; set; } = true;

    /// <summary>
    /// Tự động gia hạn?
    /// </summary>
    public bool AutoRenewal { get; set; } = false;

    /// <summary>
    /// Số ngày thông báo trước khi gia hạn: 30 ngày
    /// </summary>
    public int RenewalNoticeDays { get; set; } = 30;

    /// <summary>
    /// Đã gia hạn bao nhiêu lần: 0, 1, 2...
    /// </summary>
    public int RenewalCount { get; set; } = 0;

    // ============================================================================
    // MÔ HÌNH CUNG CẤP DỊCH VỤ
    // ============================================================================

    /// <summary>
    /// Mô hình coverage: fixed_schedule, rotating_schedule, flexible, on_call
    /// </summary>
    public string CoverageModel { get; set; } = string.Empty;

    // ============================================================================
    // LỊCH LÀM VIỆC
    // ============================================================================

    /// <summary>
    /// Theo lịch khách hàng không?
    /// true = nghỉ khi khách hàng nghỉ
    /// </summary>
    public bool FollowsCustomerCalendar { get; set; } = true;

    /// <summary>
    /// Làm việc vào ngày lễ không?
    /// Bảo vệ thường phải làm ngày lễ = true
    /// </summary>
    public bool WorkOnPublicHolidays { get; set; } = true;

    /// <summary>
    /// Làm việc khi khách hàng đóng cửa không?
    /// Bảo vệ thường vẫn phải canh = true
    /// </summary>
    public bool WorkOnCustomerClosedDays { get; set; } = true;

    // ============================================================================
    // QUY TẮC TỰ ĐỘNG TẠO CA
    // ============================================================================

    /// <summary>
    /// Tự động tạo shifts không?
    /// </summary>
    public bool AutoGenerateShifts { get; set; } = true;

    /// <summary>
    /// Tạo shifts trước bao nhiêu ngày: 30 ngày
    /// Ví dụ: Hôm nay 1/1, tạo ca cho đến 31/1
    /// </summary>
    public int GenerateShiftsAdvanceDays { get; set; } = 30;

    // ============================================================================
    // PHÊ DUYỆT & TRẠNG THÁI
    // ============================================================================

    /// <summary>
    /// Trạng thái: draft, pending_approval, active, suspended, expired, terminated, completed
    /// </summary>
    public string Status { get; set; } = "draft";

    /// <summary>
    /// Người phê duyệt hợp đồng
    /// </summary>
    public Guid? ApprovedBy { get; set; }

    /// <summary>
    /// Thời điểm phê duyệt
    /// </summary>
    public DateTime? ApprovedAt { get; set; }

    /// <summary>
    /// Thời điểm kích hoạt hợp đồng (chuyển sang active)
    /// </summary>
    public DateTime? ActivatedAt { get; set; }

    // ============================================================================
    // CHẤM DỨT HỢP ĐỒNG
    // ============================================================================

    /// <summary>
    /// Ngày chấm dứt thực tế
    /// </summary>
    public DateTime? TerminationDate { get; set; }

    /// <summary>
    /// Loại chấm dứt: completed, mutual, customer_request, breach, non_renewal
    /// </summary>
    public string? TerminationType { get; set; }

    /// <summary>
    /// Lý do chấm dứt
    /// </summary>
    public string? TerminationReason { get; set; }

    /// <summary>
    /// Người chấm dứt hợp đồng
    /// </summary>
    public Guid? TerminatedBy { get; set; }

    // ============================================================================
    // TÀI LIỆU
    // ============================================================================

    /// <summary>
    /// URL file hợp đồng PDF
    /// </summary>
    public string? ContractFileUrl { get; set; }

    /// <summary>
    /// Ngày ký hợp đồng
    /// </summary>
    public DateTime? SignedDate { get; set; }

    /// <summary>
    /// Ghi chú
    /// </summary>
    public string? Notes { get; set; }

    // ============================================================================
    // METADATA
    // ============================================================================

    public bool IsDeleted { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }
}
