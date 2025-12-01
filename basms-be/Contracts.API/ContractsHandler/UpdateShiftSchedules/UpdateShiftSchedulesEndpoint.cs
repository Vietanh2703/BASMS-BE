namespace Contracts.API.ContractsHandler.UpdateShiftSchedules;

/// <summary>
/// Endpoint để update shift schedule
/// </summary>
public class UpdateShiftSchedulesEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // Route: PUT /api/contracts/shift-schedules/{shiftScheduleId}
        app.MapPut("/api/contracts/shift-schedules/{shiftScheduleId}", async (
            Guid shiftScheduleId,
            UpdateShiftSchedulesRequest request,
            ISender sender,
            ILogger<UpdateShiftSchedulesEndpoint> logger) =>
        {
            try
            {
                logger.LogInformation("Update shift schedule request for ID: {ShiftScheduleId}", shiftScheduleId);

                // Map request to command
                var command = new UpdateShiftSchedulesCommand
                {
                    ShiftScheduleId = shiftScheduleId,
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
                    logger.LogError("Failed to update shift schedule: {ErrorMessage}", result.ErrorMessage);
                    return Results.Problem(
                        title: "Error updating shift schedule",
                        detail: result.ErrorMessage,
                        statusCode: StatusCodes.Status400BadRequest
                    );
                }

                logger.LogInformation("Successfully updated shift schedule: {ShiftScheduleId}", result.ShiftScheduleId);

                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing update shift schedule request for ID: {ShiftScheduleId}", shiftScheduleId);
                return Results.Problem(
                    title: "Error updating shift schedule",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status500InternalServerError
                );
            }
        })
        .WithTags("Contracts - Shift Schedules")
        .WithName("UpdateShiftSchedules")
        .Produces<UpdateShiftSchedulesResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Update thông tin shift schedule")
        .WithDescription(@"
            Endpoint này cập nhật thông tin của một shift schedule template.
            Tất cả các fields đều có thể update, trừ ShiftScheduleId và ContractId.

            FLOW:
            1. Validate dữ liệu đầu vào
            2. Kiểm tra shift schedule có tồn tại không
            3. Kiểm tra location có tồn tại không (nếu được chỉ định)
            4. Update shift schedule trong database
            5. Trả về kết quả

            INPUT (UpdateShiftSchedulesRequest):
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
            - monthlyDates: Các ngày trong tháng (string, format: ""1,15,30"", optional)
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

            OUTPUT (UpdateShiftSchedulesResult):
            - success: true/false
            - errorMessage: Thông báo lỗi (nếu có)
            - shiftScheduleId: GUID của shift schedule đã update
            - scheduleName: Tên mẫu ca

            VÍ DỤ REQUEST:
            ==============
            ```json
            PUT /api/contracts/shift-schedules/a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d
            {
              ""locationId"": null,
              ""scheduleName"": ""Morning Shift - Updated"",
              ""scheduleType"": ""regular"",
              ""shiftStartTime"": ""07:00:00"",
              ""shiftEndTime"": ""16:00:00"",
              ""crossesMidnight"": false,
              ""durationHours"": 9.0,
              ""breakMinutes"": 60,
              ""guardsPerShift"": 3,
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
              ""appliesOnWeekends"": false,
              ""skipWhenLocationClosed"": false,
              ""requiresArmedGuard"": false,
              ""requiresSupervisor"": true,
              ""minimumExperienceMonths"": 12,
              ""requiredCertifications"": null,
              ""autoGenerateEnabled"": true,
              ""generateAdvanceDays"": 30,
              ""effectiveFrom"": ""2025-01-01T00:00:00"",
              ""effectiveTo"": null,
              ""isActive"": true,
              ""notes"": ""Updated shift schedule with supervisor requirement""
            }
            ```

            VÍ DỤ RESPONSE THÀNH CÔNG:
            ==========================
            ```json
            {
              ""success"": true,
              ""errorMessage"": null,
              ""shiftScheduleId"": ""a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d"",
              ""scheduleName"": ""Morning Shift - Updated""
            }
            ```

            VÍ DỤ RESPONSE LỖI:
            ===================
            ```json
            {
              ""success"": false,
              ""errorMessage"": ""Shift schedule with ID a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d not found"",
              ""shiftScheduleId"": null,
              ""scheduleName"": null
            }
            ```

            VALIDATION RULES:
            =================
            1. ShiftScheduleId: Bắt buộc (từ URL path), shift schedule phải tồn tại
            2. LocationId: Optional, nếu có thì location phải tồn tại
            3. ScheduleName: Bắt buộc, tối đa 200 ký tự
            4. ScheduleType: Bắt buộc, phải là regular/overtime/standby/emergency/event
            5. ShiftStartTime & ShiftEndTime: Phải trong khoảng 00:00:00 - 23:59:59
            6. DurationHours: Phải > 0 và <= 24
            7. BreakMinutes: Phải >= 0 và <= 480
            8. GuardsPerShift: Phải >= 1 và <= 100
            9. RecurrenceType: Bắt buộc, phải là daily/weekly/bi_weekly/monthly/specific_dates
            10. Weekly recurrence: Phải chọn ít nhất 1 ngày trong tuần
            11. MonthlyDates: Phải là số từ 1-31, phân cách bằng dấu phẩy
            12. MinimumExperienceMonths: Phải >= 0 và <= 600
            13. GenerateAdvanceDays: Phải >= 1 và <= 365
            14. EffectiveFrom: Bắt buộc
            15. EffectiveTo: Optional, nếu có thì phải sau EffectiveFrom
            16. Notes: Tối đa 1000 ký tự

            USE CASES:
            ==========
            1. **Thay đổi giờ ca**: Update shift start/end time khi yêu cầu thay đổi
            2. **Điều chỉnh số lượng bảo vệ**: Tăng/giảm guardsPerShift theo nhu cầu
            3. **Cập nhật recurrence pattern**: Thay đổi ngày áp dụng trong tuần
            4. **Bật/tắt auto generation**: Toggle AutoGenerateEnabled
            5. **Cập nhật yêu cầu bảo vệ**: Thêm/bỏ requirement như armed guard, supervisor
            6. **Điều chỉnh thời gian hiệu lực**: Update EffectiveFrom/EffectiveTo

            LƯU Ý:
            =======
            - KHÔNG thể thay đổi ContractId của shift schedule
            - Update sẽ set UpdatedAt = DateTime.UtcNow tự động
            - LocationId = null nghĩa là áp dụng cho tất cả locations trong contract
            - Nếu thay đổi recurrence pattern, các shifts đã tạo trước đó KHÔNG bị ảnh hưởng
            - Chỉ các shifts mới được generate sau khi update mới theo pattern mới
        ");
    }
}

/// <summary>
/// Request model cho UpdateShiftSchedules endpoint
/// </summary>
public record UpdateShiftSchedulesRequest
{
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
