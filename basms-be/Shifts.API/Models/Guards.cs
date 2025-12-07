namespace Shifts.API.Models;

/// <summary>
/// GUARDS - Cache thông tin bảo vệ từ User Service
/// Mục đích: Lọc available cho assignments, track performance
/// </summary>
[Table("guards")]
public class Guards
{
    /// <summary>
    /// Trùng với User Service user_id
    /// </summary>
    [ExplicitKey]
    public Guid Id { get; set; }

    // ============================================================================
    // THÔNG TIN CƠ BẢN
    // ============================================================================

    /// <summary>
    /// Số CCCD
    /// </summary>
    public string IdentityNumber { get; set; } = string.Empty;

    /// <summary>
    /// Ngày cấp CCCD
    /// </summary>
    public DateTime? IdentityIssueDate { get; set; }

    /// <summary>
    /// Nơi cấp CCCD
    /// </summary>
    public string? IdentityIssuePlace { get; set; }

    /// <summary>
    /// Mã NV: GRD001
    /// </summary>
    public string EmployeeCode { get; set; } = string.Empty;

    /// <summary>
    /// Họ tên đầy đủ
    /// </summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// Link ảnh đại diện
    /// </summary>
    public string? AvatarUrl { get; set; }
    
    /// <summary>
    /// Email (optional cho guards)
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// SĐT bắt buộc - liên lạc khẩn cấp
    /// </summary>
    public string PhoneNumber { get; set; } = string.Empty;

    // ============================================================================
    // THÔNG TIN CÁ NHÂN
    // ============================================================================

    /// <summary>
    /// Ngày sinh - tính tuổi, hưu (60/55)
    /// </summary>
    public DateTime? DateOfBirth { get; set; }

    /// <summary>
    /// MALE | FEMALE
    /// </summary>
    public string? Gender { get; set; }
    
    
    /// <summary>
    /// Địa chỉ hiện tại
    /// </summary>
    public string? CurrentAddress { get; set; }

    // ============================================================================
    // TUYỂN DỤNG
    // ============================================================================

    /// <summary>
    /// ACTIVE=chính thức | PROBATION=thử việc 2-3 tháng | ON_LEAVE=nghỉ dài hạn | SUSPENDED=đình chỉ | TERMINATED=nghỉ việc
    /// </summary>
    public string EmploymentStatus { get; set; } = "ACTIVE";

    /// <summary>
    /// Ngày vào làm - tính thâm niên
    /// </summary>
    public DateTime HireDate { get; set; }

    /// <summary>
    /// Ngày hết thử việc
    /// </summary>
    public DateTime? ProbationEndDate { get; set; }

    /// <summary>
    /// FULL_TIME=48h/tuần | PART_TIME | CONTRACT=có hạn | SEASONAL=Tết/sự kiện
    /// </summary>
    public string? ContractType { get; set; }

    /// <summary>
    /// Ngày nghỉ việc
    /// </summary>
    public DateTime? TerminationDate { get; set; }

    /// <summary>
    /// Lý do nghỉ
    /// </summary>
    public string? TerminationReason { get; set; }

    // ============================================================================
    // QUẢN LÝ
    // ============================================================================

    /// <summary>
    /// Manager trực tiếp - báo cáo, approval
    /// </summary>
    public Guid? DirectManagerId { get; set; }

    // ============================================================================
    // CẤP BẬC NGHIỆP VỤ (Security Certification)
    // ============================================================================

    /// <summary>
    /// Hạng chứng chỉ nghiệp vụ: I, II, III, IV, V, VI
    /// Theo Nghị định 96/2016/NĐ-CP
    /// </summary>
    public string? CertificationLevel { get; set; }

    /// <summary>
    /// Mức lương cơ bản (VNĐ/tháng)
    /// Import từ hợp đồng lao động
    /// Ví dụ: 6000000.00 cho bảo vệ hạng I
    /// </summary>
    public decimal? StandardWage { get; set; }

    // ============================================================================
    // TÀI LIỆU & HÌNH ẢNH (Documents & Images)
    // ============================================================================

    /// <summary>
    /// URL file chứng chỉ nghiệp vụ (S3)
    /// Có thể là PDF hoặc ảnh scan
    /// Ví dụ: "s3://basms/guards/certificates/guard-123-cert.pdf"
    /// </summary>
    public string? CertificationFileUrl { get; set; }

    /// <summary>
    /// URL ảnh CCCD mặt trước (S3)
    /// Dùng cho xác minh danh tính
    /// Ví dụ: "s3://basms/guards/identity/guard-123-front.jpg"
    /// </summary>
    public string? IdentityCardFrontUrl { get; set; }

    /// <summary>
    /// URL ảnh CCCD mặt sau (S3)
    /// Dùng cho xác minh danh tính
    /// Ví dụ: "s3://basms/guards/identity/guard-123-back.jpg"
    /// </summary>
    public string? IdentityCardBackUrl { get; set; }

    // ============================================================================
    // SỞ THÍCH LÀM VIỆC (để gợi ý shifts phù hợp)
    // ============================================================================

    /// <summary>
    /// DAY=ca ngày | NIGHT=ca đêm | ROTATING=luân phiên | FLEXIBLE=linh hoạt
    /// </summary>
    public string? PreferredShiftType { get; set; }

    /// <summary>
    /// JSON array địa điểm ưu tiên: ["loc-001", "loc-005"]
    /// </summary>
    public string? PreferredLocations { get; set; }

    /// <summary>
    /// Giới hạn giờ/tuần theo luật VN
    /// </summary>
    public int MaxWeeklyHours { get; set; } = 48;

    /// <summary>
    /// Chấp nhận tăng ca
    /// </summary>
    public bool CanWorkOvertime { get; set; } = true;

    /// <summary>
    /// Làm T7/CN
    /// </summary>
    public bool CanWorkWeekends { get; set; } = true;

    /// <summary>
    /// Làm ngày lễ
    /// </summary>
    public bool CanWorkHolidays { get; set; } = true;

    // ============================================================================
    // PERFORMANCE METRICS (auto-calculated)
    // ============================================================================

    /// <summary>
    /// Tổng ca đã làm
    /// </summary>
    public int TotalShiftsWorked { get; set; } = 0;

    /// <summary>
    /// Tổng giờ làm
    /// </summary>
    public decimal TotalHoursWorked { get; set; } = 0;

    /// <summary>
    /// Tỷ lệ đi làm % = (worked/assigned)×100
    /// </summary>
    public decimal? AttendanceRate { get; set; }

    /// <summary>
    /// Tỷ lệ đúng giờ % = (on_time/total)×100
    /// </summary>
    public decimal? PunctualityRate { get; set; }

    /// <summary>
    /// Số lần không đến (nghiêm trọng!)
    /// </summary>
    public int NoShowCount { get; set; } = 0;

    /// <summary>
    /// Số lần vi phạm
    /// </summary>
    public int ViolationCount { get; set; } = 0;

    /// <summary>
    /// Số lần khen thưởng
    /// </summary>
    public int CommendationCount { get; set; } = 0;

    // ============================================================================
    // TRẠNG THÁI REALTIME (update khi check-in/out)
    // ============================================================================

    /// <summary>
    /// AVAILABLE=rảnh | ON_SHIFT=đang làm | ON_LEAVE=nghỉ phép | UNAVAILABLE=không nhận ca
    /// </summary>
    public string CurrentAvailability { get; set; } = "AVAILABLE";

    /// <summary>
    /// Ghi chú: "Nghỉ phép đến 15/01"
    /// </summary>
    public string? AvailabilityNotes { get; set; }

    // ============================================================================
    // APP & BIOMETRIC (cho chấm công)
    // ============================================================================

    /// <summary>
    /// Đã đăng ký sinh trắc học
    /// </summary>
    public bool BiometricRegistered { get; set; } = false;

    /// <summary>
    /// S3 URL template khuôn mặt cho face recognition
    /// </summary>
    public string? FaceTemplateUrl { get; set; }

    /// <summary>
    /// Lần mở app cuối - check hoạt động
    /// </summary>
    public DateTime? LastAppLogin { get; set; }

    /// <summary>
    /// JSON array tokens push notification
    /// </summary>
    public string? DeviceTokens { get; set; }
    

    // ============================================================================
    // SYNC METADATA
    // ============================================================================

    /// <summary>
    /// Lần sync cuối từ User Service
    /// </summary>
    public DateTime? LastSyncedAt { get; set; }

    /// <summary>
    /// SYNCED | PENDING | FAILED
    /// </summary>
    public string SyncStatus { get; set; } = "SYNCED";

    /// <summary>
    /// Version từ User Service
    /// </summary>
    public int? UserServiceVersion { get; set; }

    public bool IsActive { get; set; } = true;

    // ============================================================================
    // AUDIT
    // ============================================================================

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }

    // ============================================================================
    // NAVIGATION PROPERTIES
    // ============================================================================

    /// <summary>
    /// Manager trực tiếp
    /// </summary>
    [Write(false)]
    [Computed]
    public virtual Managers? DirectManager { get; set; }

    /// <summary>
    /// Team memberships
    /// </summary>
    [Write(false)]
    [Computed]
    public virtual ICollection<TeamMembers> TeamMemberships { get; set; } = new List<TeamMembers>();

    /// <summary>
    /// Shift assignments
    /// </summary>
    [Write(false)]
    [Computed]
    public virtual ICollection<ShiftAssignments> ShiftAssignments { get; set; } = new List<ShiftAssignments>();
}
