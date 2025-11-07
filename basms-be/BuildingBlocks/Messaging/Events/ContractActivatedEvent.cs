namespace BuildingBlocks.Messaging.Events;

/// <summary>
/// Event published khi hợp đồng được kích hoạt (status = active)
/// Consumed by: Shifts Service (để tự động tạo shifts)
///
/// WORKFLOW:
/// 1. Contract được tạo với status = draft
/// 2. Khi contract có đủ thông tin -> chuyển sang status = active/approved
/// 3. Publish ContractActivatedEvent
/// 4. Shifts.API nhận event và bắt đầu tạo shifts theo schedules
/// </summary>
public record ContractActivatedEvent
{
    // ========================================================================
    // HỢP ĐỒNG THÔNG TIN CƠ BẢN
    // ========================================================================

    /// <summary>
    /// ID hợp đồng
    /// </summary>
    public Guid ContractId { get; init; }

    /// <summary>
    /// Mã hợp đồng: CTR-2025-001
    /// </summary>
    public string ContractNumber { get; init; } = string.Empty;

    /// <summary>
    /// Tên hợp đồng
    /// </summary>
    public string ContractTitle { get; init; } = string.Empty;

    /// <summary>
    /// ID khách hàng
    /// </summary>
    public Guid CustomerId { get; init; }

    /// <summary>
    /// Tên công ty khách hàng
    /// </summary>
    public string CustomerName { get; init; } = string.Empty;

    // ========================================================================
    // THỜI HẠN HỢP ĐỒNG
    // ========================================================================

    /// <summary>
    /// Ngày bắt đầu hợp đồng
    /// </summary>
    public DateTime StartDate { get; init; }

    /// <summary>
    /// Ngày kết thúc hợp đồng
    /// </summary>
    public DateTime EndDate { get; init; }

    // ========================================================================
    // CÀI ĐẶT TỰ ĐỘNG TẠO CA
    // ========================================================================

    /// <summary>
    /// Có tự động tạo shifts không?
    /// </summary>
    public bool AutoGenerateShifts { get; init; }

    /// <summary>
    /// Tạo shifts trước bao nhiêu ngày: 30
    /// </summary>
    public int GenerateShiftsAdvanceDays { get; init; }

    // ========================================================================
    // LỊCH LÀM VIỆC
    // ========================================================================

    /// <summary>
    /// Làm việc vào ngày lễ không?
    /// </summary>
    public bool WorkOnPublicHolidays { get; init; }

    /// <summary>
    /// Làm việc khi khách hàng đóng cửa không?
    /// </summary>
    public bool WorkOnCustomerClosedDays { get; init; }

    // ========================================================================
    // DANH SÁCH ĐỊA ĐIỂM VÀ MẪU CA
    // ========================================================================

    /// <summary>
    /// Danh sách địa điểm trong hợp đồng
    /// </summary>
    public List<ContractLocationDto> Locations { get; init; } = new();

    /// <summary>
    /// Danh sách mẫu ca shift
    /// </summary>
    public List<ContractShiftScheduleDto> ShiftSchedules { get; init; } = new();

    // ========================================================================
    // METADATA
    // ========================================================================

    /// <summary>
    /// Thời điểm kích hoạt hợp đồng
    /// </summary>
    public DateTime ActivatedAt { get; init; }

    /// <summary>
    /// Người kích hoạt (manager)
    /// </summary>
    public Guid? ActivatedBy { get; init; }
}

/// <summary>
/// DTO cho địa điểm trong hợp đồng
/// </summary>
public record ContractLocationDto
{
    public Guid LocationId { get; init; }
    public string LocationName { get; init; } = string.Empty;
    public string LocationCode { get; init; } = string.Empty;
    public int GuardsRequired { get; init; }
    public string CoverageType { get; init; } = string.Empty;
    public DateTime ServiceStartDate { get; init; }
    public DateTime? ServiceEndDate { get; init; }

    // GPS cho geofencing
    public decimal? Latitude { get; init; }
    public decimal? Longitude { get; init; }
    public int GeofenceRadiusMeters { get; init; }
}

/// <summary>
/// DTO cho mẫu ca shift
/// </summary>
public record ContractShiftScheduleDto
{
    public Guid ScheduleId { get; init; }
    public string ScheduleName { get; init; } = string.Empty;
    public string ScheduleType { get; init; } = string.Empty;

    /// <summary>
    /// Áp dụng cho location cụ thể nào? (NULL = all locations)
    /// </summary>
    public Guid? LocationId { get; init; }

    // Thời gian ca
    public TimeSpan ShiftStartTime { get; init; }
    public TimeSpan ShiftEndTime { get; init; }
    public bool CrossesMidnight { get; init; }
    public decimal DurationHours { get; init; }
    public int BreakMinutes { get; init; }

    // Nhân sự
    public int GuardsPerShift { get; init; }

    // Lặp lại
    public string RecurrenceType { get; init; } = string.Empty;
    public bool AppliesMonday { get; init; }
    public bool AppliesTuesday { get; init; }
    public bool AppliesWednesday { get; init; }
    public bool AppliesThursday { get; init; }
    public bool AppliesFriday { get; init; }
    public bool AppliesSaturday { get; init; }
    public bool AppliesSunday { get; init; }

    // Xử lý ngày đặc biệt
    public bool AppliesOnPublicHolidays { get; init; }
    public bool AppliesOnWeekends { get; init; }
    public bool SkipWhenLocationClosed { get; init; }

    // Yêu cầu
    public bool RequiresArmedGuard { get; init; }
    public bool RequiresSupervisor { get; init; }
    public int MinimumExperienceMonths { get; init; }

    // Thời gian hiệu lực
    public DateTime EffectiveFrom { get; init; }
    public DateTime? EffectiveTo { get; init; }
}
