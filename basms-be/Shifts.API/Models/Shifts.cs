namespace Shifts.API.Models;

/// <summary>
/// SHIFTS - Ca trực thực tế (QUAN TRỌNG NHẤT!)
/// Chức năng: Lưu tất cả ca đã/đang/sẽ diễn ra, DATE SPLITTING cho aggregation nhanh
/// Use case: "Tạo ca 8h-17h ngày 15/01 tại location A, cần 3 guards, pay 150%"
/// </summary>
[Table("shifts")]
public class Shifts
{
    [ExplicitKey]
    public Guid Id { get; set; }

    // ============================================================================
    // FOREIGN KEYS
    // ============================================================================

    /// <summary>
    /// Template gốc (NULL=ad-hoc)
    /// </summary>
    public Guid? ShiftTemplateId { get; set; }

    /// <summary>
    /// Địa điểm làm việc (Contract Service)
    /// </summary>
    public Guid LocationId { get; set; }

    /// <summary>
    /// Hợp đồng khách hàng (optional)
    /// </summary>
    public Guid? ContractId { get; set; }

    // ============================================================================
    // DATE SPLITTING - CỰC KỲ QUAN TRỌNG!
    // Lý do: Query nhanh hơn 10x so với DATE_PART()
    // ============================================================================

    /// <summary>
    /// Ngày chính của ca
    /// </summary>
    public DateTime ShiftDate { get; set; }

    /// <summary>
    /// 1-31: Ngày trong tháng
    /// </summary>
    public int ShiftDay { get; set; }

    /// <summary>
    /// 1-12: Tháng
    /// </summary>
    public int ShiftMonth { get; set; }

    /// <summary>
    /// 2024, 2025: Năm
    /// </summary>
    public int ShiftYear { get; set; }

    /// <summary>
    /// 1-4: Quý (Q1-Q4)
    /// </summary>
    public int ShiftQuarter { get; set; }

    /// <summary>
    /// 1-53: Tuần trong năm (ISO)
    /// </summary>
    public int ShiftWeek { get; set; }

    /// <summary>
    /// 1-7: Thứ (1=T2, 7=CN)
    /// </summary>
    public int DayOfWeek { get; set; }

    /// <summary>
    /// Ngày kết thúc (khác shift_date nếu qua đêm)
    /// </summary>
    public DateTime? ShiftEndDate { get; set; }

    // ============================================================================
    // THỜI GIAN CHÍNH XÁC
    // ============================================================================

    /// <summary>
    /// Bắt đầu: 2025-01-15 08:00:00
    /// </summary>
    public DateTime ShiftStart { get; set; }

    /// <summary>
    /// Kết thúc: 2025-01-15 17:00:00
    /// </summary>
    public DateTime ShiftEnd { get; set; }

    // ============================================================================
    // DURATION (auto-calculated)
    // ============================================================================

    /// <summary>
    /// Tổng phút: 540 phút (9h)
    /// </summary>
    public int TotalDurationMinutes { get; set; }

    /// <summary>
    /// Làm việc thực: 480 phút (9h - 1h nghỉ)
    /// </summary>
    public int WorkDurationMinutes { get; set; }

    /// <summary>
    /// Giờ làm: 8.00h
    /// </summary>
    public decimal WorkDurationHours { get; set; }

    // ============================================================================
    // NGHỈ GIẢI LAO
    // ============================================================================

    /// <summary>
    /// Tổng nghỉ: 60 phút
    /// </summary>
    public int BreakDurationMinutes { get; set; } = 60;

    /// <summary>
    /// Nghỉ có lương: 0
    /// </summary>
    public int PaidBreakMinutes { get; set; } = 0;

    /// <summary>
    /// Nghỉ trừ công: 60 phút
    /// </summary>
    public int UnpaidBreakMinutes { get; set; } = 60;

    // ============================================================================
    // PHÂN LOẠI CA - QUAN TRỌNG CHO TÍNH LƯƠNG!
    // ============================================================================

    /// <summary>
    /// REGULAR=bình thường | OVERTIME=tăng ca | EMERGENCY=khẩn cấp | REPLACEMENT=thay thế | TRAINING=đào tạo
    /// </summary>
    public string ShiftType { get; set; } = "REGULAR";

    // ============================================================================
    // PHÂN LOẠI THEO NGÀY (critical cho pay rates!)
    // ============================================================================

    /// <summary>
    /// T2-T6 → Lương 100%
    /// </summary>
    public bool IsRegularWeekday { get; set; } = true;

    /// <summary>
    /// T7 → 150% (8h đầu), 200% (OT)
    /// </summary>
    public bool IsSaturday { get; set; } = false;

    /// <summary>
    /// CN → 200% (8h đầu), 300% (OT)
    /// </summary>
    public bool IsSunday { get; set; } = false;

    /// <summary>
    /// Lễ → 300% (8h đầu), 400% (OT)
    /// </summary>
    public bool IsPublicHoliday { get; set; } = false;

    /// <summary>
    /// Tết (5 ngày) → 300% + nghỉ bù
    /// </summary>
    public bool IsTetHoliday { get; set; } = false;

    // ============================================================================
    // PHÂN LOẠI THEO THỜI GIAN TRONG NGÀY
    // ============================================================================

    /// <summary>
    /// Ca đêm (22h-6h) → +30% phụ cấp
    /// </summary>
    public bool IsNightShift { get; set; } = false;

    /// <summary>
    /// Số giờ đêm (22h-6h)
    /// </summary>
    public decimal NightHours { get; set; } = 0;

    /// <summary>
    /// Số giờ ngày (6h-22h)
    /// </summary>
    public decimal DayHours { get; set; } = 0;

    // ============================================================================
    // STAFFING - QUẢN LÝ NHÂN SỰ CA
    // ============================================================================

    /// <summary>
    /// Số guards cần: 3
    /// </summary>
    public int RequiredGuards { get; set; } = 1;

    /// <summary>
    /// Số guards đã giao (auto-update)
    /// </summary>
    public int AssignedGuardsCount { get; set; } = 0;

    /// <summary>
    /// Số guards đã xác nhận
    /// </summary>
    public int ConfirmedGuardsCount { get; set; } = 0;

    /// <summary>
    /// Số guards đã check-in
    /// </summary>
    public int CheckedInGuardsCount { get; set; } = 0;

    /// <summary>
    /// Số guards đã hoàn thành
    /// </summary>
    public int CompletedGuardsCount { get; set; } = 0;

    // ============================================================================
    // TRẠNG THÁI ĐỦ NGƯỜI
    // ============================================================================

    /// <summary>
    /// Đủ người: assigned >= required
    /// </summary>
    public bool IsFullyStaffed { get; set; } = false;

    /// <summary>
    /// Thiếu người: assigned < required
    /// </summary>
    public bool IsUnderstaffed { get; set; } = false;

    /// <summary>
    /// Thừa người: assigned > max
    /// </summary>
    public bool IsOverstaffed { get; set; } = false;

    /// <summary>
    /// Tỷ lệ % = (assigned/required)×100
    /// </summary>
    public decimal? StaffingPercentage { get; set; }

    // ============================================================================
    // TRẠNG THÁI CA
    // ============================================================================

    /// <summary>
    /// DRAFT=mới tạo | SCHEDULED=đã lên lịch | IN_PROGRESS=đang diễn ra | COMPLETED=hoàn thành | CANCELLED=hủy | PARTIAL=thiếu guards
    /// </summary>
    public string Status { get; set; } = "DRAFT";

    // ============================================================================
    // LIFECYCLE TIMESTAMPS
    // ============================================================================

    /// <summary>
    /// Thời điểm finalize ca (publish to guards)
    /// </summary>
    public DateTime? ScheduledAt { get; set; }

    /// <summary>
    /// Guard đầu tiên check-in
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// Guard cuối cùng check-out
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Thời điểm hủy ca
    /// </summary>
    public DateTime? CancelledAt { get; set; }

    /// <summary>
    /// Lý do hủy
    /// </summary>
    public string? CancellationReason { get; set; }

    // ============================================================================
    // CỜ ĐẶC BIỆT
    // ============================================================================

    /// <summary>
    /// Ca bắt buộc (không được từ chối)
    /// </summary>
    public bool IsMandatory { get; set; } = false;

    /// <summary>
    /// Ca quan trọng (VIP, sự kiện đặc biệt)
    /// </summary>
    public bool IsCritical { get; set; } = false;

    /// <summary>
    /// Ca đào tạo (guards mới)
    /// </summary>
    public bool IsTrainingShift { get; set; } = false;

    /// <summary>
    /// Yêu cầu vũ trang
    /// </summary>
    public bool RequiresArmedGuard { get; set; } = false;

    // ============================================================================
    // WORKFLOW DUYỆT
    // ============================================================================

    /// <summary>
    /// Cần duyệt ca
    /// </summary>
    public bool RequiresApproval { get; set; } = true;

    /// <summary>
    /// Manager duyệt
    /// </summary>
    public Guid? ApprovedBy { get; set; }

    public DateTime? ApprovedAt { get; set; }

    /// <summary>
    /// PENDING | APPROVED | REJECTED
    /// </summary>
    public string ApprovalStatus { get; set; } = "PENDING";

    public string? RejectionReason { get; set; }

    // ============================================================================
    // GHI CHÚ
    // ============================================================================

    /// <summary>
    /// Mô tả ca
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Hướng dẫn đặc biệt: "Mang bộ đàm"
    /// </summary>
    public string? SpecialInstructions { get; set; }

    /// <summary>
    /// Thiết bị cần: "Đèn pin, còi"
    /// </summary>
    public string? EquipmentNeeded { get; set; }

    /// <summary>
    /// JSON: Liên hệ khẩn cấp
    /// </summary>
    public string? EmergencyContacts { get; set; }

    /// <summary>
    /// Thông tin vào cửa: mật khẩu, key card
    /// </summary>
    public string? SiteAccessInfo { get; set; }

    // ============================================================================
    // AUDIT
    // ============================================================================

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Manager tạo ca
    /// </summary>
    public Guid CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }

    /// <summary>
    /// Optimistic locking - tránh conflict
    /// </summary>
    public int Version { get; set; } = 1;

    // ============================================================================
    // NAVIGATION PROPERTIES
    // ============================================================================

    /// <summary>
    /// Template gốc
    /// </summary>
    [Write(false)]
    [Computed]
    public virtual ShiftTemplates? ShiftTemplate { get; set; }

    /// <summary>
    /// Manager duyệt ca
    /// </summary>
    [Write(false)]
    [Computed]
    public virtual Managers? Approver { get; set; }

    /// <summary>
    /// Manager tạo ca
    /// </summary>
    [Write(false)]
    [Computed]
    public virtual Managers? Creator { get; set; }

    /// <summary>
    /// Danh sách assignments (guards được giao ca)
    /// </summary>
    [Write(false)]
    [Computed]
    public virtual ICollection<ShiftAssignments> Assignments { get; set; } = new List<ShiftAssignments>();

    /// <summary>
    /// Conflicts của ca này
    /// </summary>
    [Write(false)]
    [Computed]
    public virtual ICollection<ShiftConflicts> Conflicts { get; set; } = new List<ShiftConflicts>();
}