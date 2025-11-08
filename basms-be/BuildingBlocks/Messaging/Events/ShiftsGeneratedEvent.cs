namespace BuildingBlocks.Messaging.Events;

/// <summary>
/// Event published khi Shifts.API tự động tạo shifts từ contract schedules
/// Consumed by: Contracts Service (để update shift_generation_log)
///
/// WORKFLOW:
/// 1. Background job trong Shifts.API chạy định kỳ hoặc được trigger
/// 2. Đọc ContractShiftSchedules từ Contracts.API
/// 3. Tự động tạo shifts theo schedules
/// 4. Publish ShiftsGeneratedEvent về Contracts.API
/// 5. Contracts.API update shift_generation_log để audit
/// </summary>
public record ShiftsGeneratedEvent
{
    // ========================================================================
    // CONTRACT INFO
    // ========================================================================

    /// <summary>
    /// ID hợp đồng
    /// </summary>
    public Guid ContractId { get; init; }

    /// <summary>
    /// Mã hợp đồng
    /// </summary>
    public string ContractNumber { get; init; } = string.Empty;

    /// <summary>
    /// ID mẫu ca (nếu từ specific schedule)
    /// </summary>
    public Guid? ContractShiftScheduleId { get; init; }

    // ========================================================================
    // GENERATION INFO
    // ========================================================================

    /// <summary>
    /// Ngày target tạo shifts: 2025-01-15
    /// </summary>
    public DateTime GenerationDate { get; init; }

    /// <summary>
    /// Thời điểm tạo shifts
    /// </summary>
    public DateTime GeneratedAt { get; init; }

    /// <summary>
    /// Job name: "daily_auto_gen", "manual_trigger", "contract_activated"
    /// </summary>
    public string GeneratedByJob { get; init; } = string.Empty;

    // ========================================================================
    // RESULTS
    // ========================================================================

    /// <summary>
    /// Số lượng shifts tạo thành công
    /// </summary>
    public int ShiftsCreatedCount { get; init; }

    /// <summary>
    /// Số lượng shifts bị bỏ qua
    /// </summary>
    public int ShiftsSkippedCount { get; init; }

    /// <summary>
    /// Lý do bỏ qua (JSON array)
    /// ["holiday", "location_closed", "exception_defined", "duplicate"]
    /// </summary>
    public List<string> SkipReasons { get; init; }

    /// <summary>
    /// Trạng thái: success, partial, failed
    /// </summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>
    /// Error message nếu failed
    /// </summary>
    public string? ErrorMessage { get; init; }

    // ========================================================================
    // DANH SÁCH SHIFTS ĐÃ TẠO
    // ========================================================================

    /// <summary>
    /// Danh sách Shift IDs đã tạo
    /// </summary>
    public List<Guid> CreatedShiftIds { get; init; } = new();

    /// <summary>
    /// Thông tin chi tiết shifts (optional)
    /// </summary>
    public List<GeneratedShiftDto> GeneratedShifts { get; init; } = new();

    // ========================================================================
    // STATISTICS
    // ========================================================================

    /// <summary>
    /// Tổng số locations được process
    /// </summary>
    public int LocationsProcessed { get; init; }

    /// <summary>
    /// Tổng số schedules được apply
    /// </summary>
    public int SchedulesProcessed { get; init; }

    /// <summary>
    /// Thời gian generation (milliseconds)
    /// </summary>
    public int GenerationDurationMs { get; init; }
}

/// <summary>
/// DTO cho shift đã tạo
/// </summary>
public record GeneratedShiftDto
{
    public Guid ShiftId { get; init; }
    public Guid LocationId { get; init; }
    public string LocationName { get; init; } = string.Empty;
    public DateTime ShiftDate { get; init; }
    public TimeSpan ShiftStartTime { get; init; }
    public TimeSpan ShiftEndTime { get; init; }
    public int RequiredGuards { get; init; }
    public string ShiftType { get; init; } = string.Empty;
    public bool IsHoliday { get; init; }
    public bool IsWeekend { get; init; }
}
