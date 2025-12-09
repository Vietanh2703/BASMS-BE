namespace Contracts.API.ContractsHandler.CreateShiftSchedules;

/// <summary>
/// Endpoint để tạo mới shift schedule cho contract
/// </summary>
public class CreateShiftSchedulesEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // Route: POST /api/contracts/shift-schedules
        app.MapPost("/api/contracts/shift-schedules", async (
            CreateShiftSchedulesRequest request,
            ISender sender,
            ILogger<CreateShiftSchedulesEndpoint> logger) =>
        {
            try
            {
                logger.LogInformation("Create shift schedule request: {ScheduleName}", request.ScheduleName);

                // Map request to command
                var command = new CreateShiftSchedulesCommand
                {
                    ContractId = request.ContractId,
                    LocationId = request.LocationId,
                    ScheduleName = request.ScheduleName,
                    ScheduleType = request.ScheduleType,
                    ShiftStartTime = request.ShiftStartTime,
                    ShiftEndTime = request.ShiftEndTime,
                    CrossesMidnight = request.CrossesMidnight,
                    DurationHours = request.DurationHours,
                    BreakMinutes = request.BreakMinutes,
                    GuardsPerShift = request.GuardsPerShift,
                    RecurrenceType = request.RecurrenceType,
                    AppliesMonday = request.AppliesMonday,
                    AppliesTuesday = request.AppliesTuesday,
                    AppliesWednesday = request.AppliesWednesday,
                    AppliesThursday = request.AppliesThursday,
                    AppliesFriday = request.AppliesFriday,
                    AppliesSaturday = request.AppliesSaturday,
                    AppliesSunday = request.AppliesSunday,
                    MonthlyDates = request.MonthlyDates,
                    AppliesOnPublicHolidays = request.AppliesOnPublicHolidays,
                    AppliesOnCustomerHolidays = request.AppliesOnCustomerHolidays,
                    AppliesOnWeekends = request.AppliesOnWeekends,
                    SkipWhenLocationClosed = request.SkipWhenLocationClosed,
                    RequiresArmedGuard = request.RequiresArmedGuard,
                    RequiresSupervisor = request.RequiresSupervisor,
                    MinimumExperienceMonths = request.MinimumExperienceMonths,
                    RequiredCertifications = request.RequiredCertifications,
                    AutoGenerateEnabled = request.AutoGenerateEnabled,
                    GenerateAdvanceDays = request.GenerateAdvanceDays,
                    EffectiveFrom = request.EffectiveFrom,
                    EffectiveTo = request.EffectiveTo,
                    IsActive = request.IsActive,
                    Notes = request.Notes
                };

                var result = await sender.Send(command);

                if (!result.Success)
                {
                    logger.LogError("Failed to create shift schedule: {ErrorMessage}", result.ErrorMessage);
                    return Results.Problem(
                        title: "Error creating shift schedule",
                        detail: result.ErrorMessage,
                        statusCode: StatusCodes.Status400BadRequest
                    );
                }

                logger.LogInformation("Successfully created shift schedule: {ShiftScheduleId}", result.ShiftScheduleId);

                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing create shift schedule request");
                return Results.Problem(
                    title: "Error creating shift schedule",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status500InternalServerError
                );
            }
        })
        .RequireAuthorization()
        .WithTags("Contracts - Shift Schedules")
        .WithName("CreateShiftSchedules")
        .Produces<CreateShiftSchedulesResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Tạo mới shift schedule cho contract")
        .WithDescription(@"
            Endpoint này tạo mới một mẫu lịch ca (shift schedule template) cho contract.
            Shift schedule định nghĩa các ca làm việc tự động lặp lại theo chu kỳ.

            FLOW:
            1. Validate dữ liệu đầu vào (thời gian ca, số lượng bảo vệ, recurrence pattern...)
            2. Kiểm tra contract có tồn tại không
            3. Kiểm tra location có tồn tại không (nếu được chỉ định)
            4. Tạo shift schedule mới trong database
            5. Trả về kết quả

            INPUT (CreateShiftSchedulesRequest):
            - contractId: GUID của contract (required)
            - locationId: GUID của location (optional - null = áp dụng cho tất cả locations)
            - scheduleName: Tên mẫu ca (required, max 200 chars)
            - scheduleType: Loại ca - regular/overtime/standby/emergency/event (default: regular)
            - shiftStartTime: Giờ bắt đầu ca (TimeSpan, format HH:mm:ss)
            - shiftEndTime: Giờ kết thúc ca (TimeSpan, format HH:mm:ss)
            - crossesMidnight: Ca có qua đêm không (bool, default: false)
            - durationHours: Thời lượng ca (decimal, 0-24 giờ)
            - breakMinutes: Thời gian nghỉ giải lao (int, 0-480 phút, default: 0)
            - guardsPerShift: Số lượng bảo vệ cần cho ca (int, 1-100, required)
            - recurrenceType: Loại lặp lại - daily/weekly/bi_weekly/monthly/specific_dates (default: weekly)
            - appliesMonday đến appliesSunday: Áp dụng cho các ngày trong tuần (bool, default: false)
            - monthlyDates: Các ngày trong tháng (string, format: 1,15,30, optional)
            - appliesOnPublicHolidays: Áp dụng vào ngày lễ quốc gia (bool, default: true)
            - appliesOnCustomerHolidays: Áp dụng vào ngày nghỉ khách hàng (bool, default: true)
            - appliesOnWeekends: Áp dụng vào cuối tuần (bool, default: true)
            - skipWhenLocationClosed: Bỏ qua khi location đóng cửa (bool, default: false)
            - requiresArmedGuard: Yêu cầu bảo vệ có vũ trang (bool, default: false)
            - requiresSupervisor: Yêu cầu supervisor (bool, default: false)
            - minimumExperienceMonths: Kinh nghiệm tối thiểu (int, 0-600 tháng, default: 0)
            - requiredCertifications: Chứng chỉ yêu cầu (JSON string, optional)
            - autoGenerateEnabled: Bật tự động tạo ca (bool, default: true)
            - generateAdvanceDays: Tạo ca trước bao nhiêu ngày (int, 1-365, default: 30)
            - effectiveFrom: Có hiệu lực từ ngày (DateTime, required)
            - effectiveTo: Có hiệu lực đến ngày (DateTime, optional)
            - isActive: Trạng thái active (bool, default: true)
            - notes: Ghi chú (string, max 1000 chars, optional)

            OUTPUT (CreateShiftSchedulesResult):
            - success: true/false
            - errorMessage: Thông báo lỗi (nếu có)
            - shiftScheduleId: GUID của shift schedule đã tạo
            - scheduleName: Tên mẫu ca

            VÍ DỤ REQUEST:
            ==============
            ```json
            POST /api/contracts/shift-schedules
            {
              ""contractId"": ""117bc5b6-abf1-4976-9a27-74368c946dc3"",
              ""locationId"": null,
              ""scheduleName"": ""Morning Shift - Weekdays"",
              ""scheduleType"": ""regular"",
              ""shiftStartTime"": ""08:00:00"",
              ""shiftEndTime"": ""17:00:00"",
              ""crossesMidnight"": false,
              ""durationHours"": 9.0,
              ""breakMinutes"": 60,
              ""guardsPerShift"": 2,
              ""recurrenceType"": ""weekly"",
              ""appliesMonday"": true,
              ""appliesTuesday"": true,
              ""appliesWednesday"": true,
              ""appliesThursday"": true,
              ""appliesFriday"": true,
              ""appliesSaturday"": false,
              ""appliesSunday"": false,
              ""appliesOnPublicHolidays"": true,
              ""appliesOnCustomerHolidays"": true,
              ""appliesOnWeekends"": true,
              ""skipWhenLocationClosed"": false,
              ""requiresArmedGuard"": false,
              ""requiresSupervisor"": false,
              ""minimumExperienceMonths"": 6,
              ""requiredCertifications"": null,
              ""autoGenerateEnabled"": true,
              ""generateAdvanceDays"": 30,
              ""effectiveFrom"": ""2025-01-01T00:00:00"",
              ""effectiveTo"": null,
              ""isActive"": true,
              ""notes"": ""Standard weekday morning shift""
            }
            ```

            VÍ DỤ RESPONSE THÀNH CÔNG:
            ==========================
            ```json
            {
              ""success"": true,
              ""errorMessage"": null,
              ""shiftScheduleId"": ""a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d"",
              ""scheduleName"": ""Morning Shift - Weekdays""
            }
            ```

            VÍ DỤ RESPONSE LỖI:
            ===================
            ```json
            {
              ""success"": false,
              ""errorMessage"": ""Contract with ID 117bc5b6-abf1-4976-9a27-74368c946dc3 not found"",
              ""shiftScheduleId"": null,
              ""scheduleName"": null
            }
            ```

            VALIDATION RULES:
            =================
            1. ContractId: Bắt buộc, contract phải tồn tại
            2. LocationId: Optional, nếu có thì location phải tồn tại
            3. ScheduleName: Bắt buộc, tối đa 200 ký tự
            4. ScheduleType: Bắt buộc, phải là regular/overtime/standby/emergency/event
            5. ShiftStartTime & ShiftEndTime: Phải trong khoảng 00:00:00 - 23:59:59
            6. DurationHours: Phải > 0 và <= 24
            7. BreakMinutes: Phải >= 0 và <= 480
            8. GuardsPerShift: Phải >= 1 và <= 100
            9. RecurrenceType: Bắt buộc, phải là daily/weekly/bi_weekly/monthly/specific_dates
            10. Weekly recurrence: Phải chọn ít nhất 1 ngày trong tuần
            11. MonthlyDates: Phải là số từ 1-31, phân cách bằng dấu phẩy (e.g., 1,15,30)
            12. MinimumExperienceMonths: Phải >= 0 và <= 600
            13. GenerateAdvanceDays: Phải >= 1 và <= 365
            14. EffectiveFrom: Bắt buộc
            15. EffectiveTo: Optional, nếu có thì phải sau EffectiveFrom
            16. Notes: Tối đa 1000 ký tự

            USE CASES:
            ==========
            1. **Ca sáng T2-T6**: 8h-17h, 2 bảo vệ, lặp hàng tuần
            2. **Ca đêm 24/7**: 22h-6h (qua đêm), 3 bảo vệ, lặp hàng ngày
            3. **Ca cuối tuần**: T7-CN, 1 bảo vệ, chỉ áp dụng cuối tuần
            4. **Ca tăng ca**: Overtime shifts theo yêu cầu đặc biệt
            5. **Ca sự kiện**: Event-based shifts cho các sự kiện đặc biệt

            LƯU Ý:
            =======
            - LocationId = null nghĩa là shift schedule áp dụng cho TẤT CẢ locations trong contract
            - CrossesMidnight = true khi ca bắt đầu hôm nay và kết thúc hôm sau (ví dụ: 22:00-06:00)
            - AutoGenerateEnabled = true sẽ tự động tạo shifts dựa trên schedule này
            - GenerateAdvanceDays: Hệ thống tự động tạo shifts trước X ngày
            - RequiredCertifications: Lưu dưới dạng JSON array string
        ");
    }
}

/// <summary>
/// Request model cho CreateShiftSchedules endpoint
/// </summary>
public record CreateShiftSchedulesRequest
{
    public Guid ContractId { get; init; }
    public Guid? LocationId { get; init; }
    public string ScheduleName { get; init; } = string.Empty;
    public string ScheduleType { get; init; } = "regular";
    public TimeSpan ShiftStartTime { get; init; }
    public TimeSpan ShiftEndTime { get; init; }
    public bool CrossesMidnight { get; init; }
    public decimal DurationHours { get; init; }
    public int BreakMinutes { get; init; }
    public int GuardsPerShift { get; init; }
    public string RecurrenceType { get; init; } = "weekly";
    public bool AppliesMonday { get; init; }
    public bool AppliesTuesday { get; init; }
    public bool AppliesWednesday { get; init; }
    public bool AppliesThursday { get; init; }
    public bool AppliesFriday { get; init; }
    public bool AppliesSaturday { get; init; }
    public bool AppliesSunday { get; init; }
    public string? MonthlyDates { get; init; }
    public bool AppliesOnPublicHolidays { get; init; } = true;
    public bool AppliesOnCustomerHolidays { get; init; } = true;
    public bool AppliesOnWeekends { get; init; } = true;
    public bool SkipWhenLocationClosed { get; init; }
    public bool RequiresArmedGuard { get; init; }
    public bool RequiresSupervisor { get; init; }
    public int MinimumExperienceMonths { get; init; }
    public string? RequiredCertifications { get; init; }
    public bool AutoGenerateEnabled { get; init; } = true;
    public int GenerateAdvanceDays { get; init; } = 30;
    public DateTime EffectiveFrom { get; init; }
    public DateTime? EffectiveTo { get; init; }
    public bool IsActive { get; init; } = true;
    public string? Notes { get; init; }
}
