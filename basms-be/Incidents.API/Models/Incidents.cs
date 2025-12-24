namespace Incidents.API.Models;

[Table("incidents")]
public class Incidents
{
    [ExplicitKey]
    public Guid Id { get; set; }
    public string IncidentCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    // ============================================================================
    // PHÂN LOẠI & MỨC ĐỘ
    // ============================================================================

    /// <summary>
    /// Loại sự cố: INTRUSION=xâm nhập | THEFT=trộm cắp | FIRE=hỏa hoạn |
    /// MEDICAL=y tế | EQUIPMENT_FAILURE=hỏng thiết bị | VANDALISM=phá hoại |
    /// DISPUTE=tranh chấp | OTHER=khác
    /// </summary>
    public string IncidentType { get; set; } = string.Empty;

    /// <summary>
    /// Mức độ nghiêm trọng: LOW=thấp | MEDIUM=trung bình | HIGH=cao | CRITICAL=nguy kịch
    /// </summary>
    public string Severity { get; set; } = string.Empty;

    // ============================================================================
    // THỜI GIAN & ĐỊA ĐIỂM
    // ============================================================================

    /// <summary>
    /// Thời điểm xảy ra sự cố
    /// </summary>
    public DateTime IncidentTime { get; set; }

    /// <summary>
    /// Địa điểm xảy ra (tên location hoặc mô tả chi tiết)
    /// Ví dụ: "Cổng chính - Location A" hoặc "Tầng 3, phòng 301"
    /// </summary>
    public string Location { get; set; } = string.Empty;

    /// <summary>
    /// Địa chỉ ca trực (Shift Location)
    /// Ví dụ: "Cổng chính - Tòa nhà A"
    /// </summary>
    public string? ShiftLocation { get; set; }

    // ============================================================================
    // LIÊN KẾT CA TRỰC
    // ============================================================================

    /// <summary>
    /// Ca trực khi xảy ra sự cố (NULL=ngoài giờ làm hoặc không phải guard báo cáo)
    /// </summary>
    public Guid? ShiftId { get; set; }

    /// <summary>
    /// Assignment của guard báo cáo (NULL=không phải guard trong ca)
    /// </summary>
    public Guid? ShiftAssignmentId { get; set; }

    // ============================================================================
    // NGƯỜI BÁO CÁO
    // ============================================================================

    /// <summary>
    /// ID người báo cáo (từ Users.API)
    /// </summary>
    public Guid ReporterId { get; set; }

    /// <summary>
    /// Tên người báo cáo (cached từ Users.API để hiển thị nhanh)
    /// </summary>
    public string ReporterName { get; set; } = string.Empty;

    /// <summary>
    /// Email người báo cáo (cached từ Users.API)
    /// </summary>
    public string ReporterEmail { get; set; } = string.Empty;

    /// <summary>
    /// Thời điểm báo cáo
    /// </summary>
    public DateTime ReportedTime { get; set; }

    // ============================================================================
    // TRẠNG THÁI XỬ LÝ
    // ============================================================================

    /// <summary>
    /// REPORTED=mới báo cáo | IN_PROGRESS=đang xử lý | RESOLVED=đã giải quyết |
    /// ESCALATED=leo thang | CLOSED=đóng
    /// </summary>
    public string Status { get; set; } = "REPORTED";

    // ============================================================================
    // PHẢN HỒI & XỬ LÝ
    // ============================================================================

    /// <summary>
    /// Nội dung phản hồi/xử lý
    /// </summary>
    public string? ResponseContent { get; set; }

    /// <summary>
    /// ID người xử lý (từ Users.API)
    /// </summary>
    public Guid? ResponderId { get; set; }

    /// <summary>
    /// Tên người xử lý (cached từ Users.API)
    /// </summary>
    public string? ResponderName { get; set; }

    /// <summary>
    /// Email người xử lý (cached từ Users.API)
    /// </summary>
    public string? ResponderEmail { get; set; }

    /// <summary>
    /// Thời điểm phản hồi
    /// </summary>
    public DateTime? RespondedAt { get; set; }

    // ============================================================================
    // AUDIT
    // ============================================================================

    public bool IsDeleted { get; set; } = false;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }

    // ============================================================================
    // NAVIGATION PROPERTIES
    // ============================================================================

    /// <summary>
    /// Media files đính kèm (ảnh, video)
    /// </summary>
    [Write(false)]
    [Computed]
    public virtual ICollection<IncidentMedia> Media { get; set; } = new List<IncidentMedia>();
}
