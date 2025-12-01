namespace Contracts.API.ContractsHandler.GetShiftScheduleByContractId;

/// <summary>
/// Endpoint để lấy danh sách shift schedules theo contract ID
/// </summary>
public class GetShiftScheduleByContractIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // Route: GET /api/contracts/{contractId}/shift-schedules
        app.MapGet("/api/contracts/{contractId}/shift-schedules", async (
            Guid contractId,
            ISender sender,
            ILogger<GetShiftScheduleByContractIdEndpoint> logger) =>
        {
            try
            {
                logger.LogInformation("Get shift schedules request for contract: {ContractId}", contractId);

                var query = new GetShiftScheduleByContractIdQuery(contractId);
                var result = await sender.Send(query);

                if (!result.Success)
                {
                    logger.LogWarning("Failed to get shift schedules: {ErrorMessage}", result.ErrorMessage);
                    return Results.NotFound(new
                    {
                        success = false,
                        error = result.ErrorMessage
                    });
                }

                logger.LogInformation(
                    "Successfully retrieved {Count} shift schedule(s) for contract {ContractCode}",
                    result.ShiftSchedules.Count, result.ContractCode);

                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing get shift schedules request for contract: {ContractId}", contractId);
                return Results.Problem(
                    title: "Error getting shift schedules",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status500InternalServerError
                );
            }
        })
        .WithTags("Contracts - Shift Schedules")
        .WithName("GetShiftScheduleByContractId")
        .Produces<GetShiftScheduleByContractIdResult>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Lấy danh sách shift schedules theo contract ID")
        .WithDescription(@"
            Endpoint này trả về danh sách tất cả shift schedule templates của một contract.
            Shift schedules định nghĩa các mẫu ca làm việc cho bảo vệ.

            FLOW:
            1. Kiểm tra contract có tồn tại không
            2. Lấy danh sách tất cả shift schedules của contract
            3. Trả về shift schedules với thông tin đầy đủ

            INPUT:
            - contractId: GUID của contract (trong URL path)

            OUTPUT (GetShiftScheduleByContractIdResult):
            - success: true/false
            - errorMessage: Thông báo lỗi (nếu có)
            - contractId: GUID của contract
            - contractCode: Mã contract (CT-XXXXXXXX-XXXX)
            - shiftSchedules: Mảng các shift schedule với thông tin:
              - id: GUID của shift schedule
              - contractId: GUID của contract
              - locationId: GUID của location (null = áp dụng cho tất cả locations)
              - scheduleName: Tên mẫu ca
              - scheduleType: Loại ca (regular/overtime/standby/emergency/event)

              **Thông tin thời gian:**
              - shiftStartTime: Giờ bắt đầu ca (TimeSpan)
              - shiftEndTime: Giờ kết thúc ca (TimeSpan)
              - crossesMidnight: Ca có qua đêm không
              - durationHours: Thời lượng ca (giờ)
              - breakMinutes: Thời gian nghỉ giải lao (phút)

              **Nhân sự:**
              - guardsPerShift: Số lượng bảo vệ cần cho mỗi ca

              **Lịch lặp lại:**
              - recurrenceType: Loại lặp lại (daily/weekly/bi_weekly/monthly/specific_dates)
              - appliesMonday đến appliesSunday: Áp dụng cho các ngày trong tuần
              - monthlyDates: Các ngày trong tháng (string: ""1,15,30"")

              **Ngày đặc biệt:**
              - appliesOnPublicHolidays: Áp dụng vào ngày lễ quốc gia
              - appliesOnCustomerHolidays: Áp dụng vào ngày nghỉ khách hàng
              - appliesOnWeekends: Áp dụng vào cuối tuần
              - skipWhenLocationClosed: Bỏ qua khi location đóng cửa

              **Yêu cầu bảo vệ:**
              - requiresArmedGuard: Yêu cầu bảo vệ có vũ trang
              - requiresSupervisor: Yêu cầu supervisor
              - minimumExperienceMonths: Kinh nghiệm tối thiểu (tháng)
              - requiredCertifications: Chứng chỉ yêu cầu (JSON string)

              **Tự động tạo ca:**
              - autoGenerateEnabled: Bật tự động tạo ca
              - generateAdvanceDays: Tạo ca trước bao nhiêu ngày

              **Thời gian hiệu lực:**
              - effectiveFrom: Có hiệu lực từ ngày
              - effectiveTo: Có hiệu lực đến ngày (null = vô thời hạn)
              - isActive: Trạng thái active
              - notes: Ghi chú
              - createdBy: Người tạo

            VÍ DỤ REQUEST:
            ==============
            ```bash
            GET /api/contracts/a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d/shift-schedules
            ```

            VÍ DỤ RESPONSE THÀNH CÔNG:
            ==========================
            ```json
            {
              ""success"": true,
              ""errorMessage"": null,
              ""contractId"": ""a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d"",
              ""contractCode"": ""CT-20251129-A1B2"",
              ""shiftSchedules"": [
                {
                  ""id"": ""f1e2d3c4-b5a6-4f05-9f04-1b046d7d4dd9"",
                  ""contractId"": ""a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d"",
                  ""locationId"": null,
                  ""scheduleName"": ""Ca Sáng - Thứ 2 đến Thứ 6"",
                  ""scheduleType"": ""regular"",
                  ""shiftStartTime"": ""07:00:00"",
                  ""shiftEndTime"": ""16:00:00"",
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
                  ""monthlyDates"": null,
                  ""appliesOnPublicHolidays"": true,
                  ""appliesOnCustomerHolidays"": true,
                  ""appliesOnWeekends"": false,
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
                  ""notes"": ""Ca sáng cho ngày thường"",
                  ""createdBy"": ""admin@company.com""
                },
                {
                  ""id"": ""g2f3e4d5-c6b7-5f06-af05-2c157e8e5ee8"",
                  ""contractId"": ""a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d"",
                  ""locationId"": null,
                  ""scheduleName"": ""Ca Chiều - Thứ 2 đến Thứ 6"",
                  ""scheduleType"": ""regular"",
                  ""shiftStartTime"": ""16:00:00"",
                  ""shiftEndTime"": ""00:00:00"",
                  ""crossesMidnight"": true,
                  ""durationHours"": 8.0,
                  ""breakMinutes"": 30,
                  ""guardsPerShift"": 2,
                  ""recurrenceType"": ""weekly"",
                  ""appliesMonday"": true,
                  ""appliesTuesday"": true,
                  ""appliesWednesday"": true,
                  ""appliesThursday"": true,
                  ""appliesFriday"": true,
                  ""appliesSaturday"": false,
                  ""appliesSunday"": false,
                  ""monthlyDates"": null,
                  ""appliesOnPublicHolidays"": true,
                  ""appliesOnCustomerHolidays"": true,
                  ""appliesOnWeekends"": false,
                  ""skipWhenLocationClosed"": false,
                  ""requiresArmedGuard"": true,
                  ""requiresSupervisor"": false,
                  ""minimumExperienceMonths"": 12,
                  ""requiredCertifications"": null,
                  ""autoGenerateEnabled"": true,
                  ""generateAdvanceDays"": 30,
                  ""effectiveFrom"": ""2025-01-01T00:00:00"",
                  ""effectiveTo"": null,
                  ""isActive"": true,
                  ""notes"": ""Ca chiều, yêu cầu bảo vệ có vũ trang"",
                  ""createdBy"": ""admin@company.com""
                },
                {
                  ""id"": ""h3g4f5e6-d7c8-6f07-bf06-3d268f9f6ff9"",
                  ""contractId"": ""a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d"",
                  ""locationId"": ""e294fe79-6fac-4f05-9f04-1b046d7d4dd9"",
                  ""scheduleName"": ""Ca Cuối Tuần - Location A"",
                  ""scheduleType"": ""regular"",
                  ""shiftStartTime"": ""08:00:00"",
                  ""shiftEndTime"": ""20:00:00"",
                  ""crossesMidnight"": false,
                  ""durationHours"": 12.0,
                  ""breakMinutes"": 90,
                  ""guardsPerShift"": 3,
                  ""recurrenceType"": ""weekly"",
                  ""appliesMonday"": false,
                  ""appliesTuesday"": false,
                  ""appliesWednesday"": false,
                  ""appliesThursday"": false,
                  ""appliesFriday"": false,
                  ""appliesSaturday"": true,
                  ""appliesSunday"": true,
                  ""monthlyDates"": null,
                  ""appliesOnPublicHolidays"": true,
                  ""appliesOnCustomerHolidays"": false,
                  ""appliesOnWeekends"": true,
                  ""skipWhenLocationClosed"": true,
                  ""requiresArmedGuard"": false,
                  ""requiresSupervisor"": true,
                  ""minimumExperienceMonths"": 6,
                  ""requiredCertifications"": null,
                  ""autoGenerateEnabled"": true,
                  ""generateAdvanceDays"": 30,
                  ""effectiveFrom"": ""2025-01-01T00:00:00"",
                  ""effectiveTo"": ""2025-12-31T23:59:59"",
                  ""isActive"": true,
                  ""notes"": ""Ca cuối tuần cho location cụ thể, yêu cầu supervisor"",
                  ""createdBy"": ""manager@company.com""
                }
              ]
            }
            ```

            VÍ DỤ RESPONSE KHI KHÔNG TÌM THẤY:
            ===================================
            ```json
            {
              ""success"": false,
              ""error"": ""Contract with ID a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d not found""
            }
            ```

            VÍ DỤ RESPONSE KHI CONTRACT CHƯA CÓ SHIFT SCHEDULES:
            =====================================================
            ```json
            {
              ""success"": true,
              ""errorMessage"": null,
              ""contractId"": ""a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d"",
              ""contractCode"": ""CT-20251129-A1B2"",
              ""shiftSchedules"": []
            }
            ```

            CÁCH SỬ DỤNG:
            =============

            **cURL:**
            ```bash
            curl -X GET 'http://localhost:5000/api/contracts/a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d/shift-schedules'
            ```

            **JavaScript Fetch:**
            ```javascript
            const getShiftSchedulesByContract = async (contractId) => {
              const response = await fetch(`/api/contracts/${contractId}/shift-schedules`);
              const result = await response.json();

              if (result.success) {
                console.log(`Found ${result.shiftSchedules.length} shift schedule(s) for ${result.contractCode}`);
                result.shiftSchedules.forEach(schedule => {
                  console.log(`- ${schedule.scheduleName}: ${schedule.scheduleType}`);
                  console.log(`  Time: ${schedule.shiftStartTime} - ${schedule.shiftEndTime}`);
                  console.log(`  Guards: ${schedule.guardsPerShift}, Duration: ${schedule.durationHours}h`);
                });
              } else {
                console.error(`Error: ${result.error}`);
              }

              return result;
            };

            // Sử dụng
            getShiftSchedulesByContract('a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d');
            ```

            **React Example:**
            ```jsx
            const ShiftSchedulesList = ({ contractId }) => {
              const [schedules, setSchedules] = useState([]);
              const [loading, setLoading] = useState(true);

              useEffect(() => {
                const fetchSchedules = async () => {
                  const response = await fetch(`/api/contracts/${contractId}/shift-schedules`);
                  const data = await response.json();

                  if (data.success) {
                    setSchedules(data.shiftSchedules);
                  }
                  setLoading(false);
                };

                fetchSchedules();
              }, [contractId]);

              if (loading) return <div>Loading shift schedules...</div>;

              return (
                <div>
                  <h3>Shift Schedules for Contract</h3>
                  {schedules.length === 0 ? (
                    <p>No shift schedules found for this contract.</p>
                  ) : (
                    <div className=""schedules-grid"">
                      {schedules.map(schedule => (
                        <div key={schedule.id} className=""schedule-card"">
                          <h4>{schedule.scheduleName}</h4>
                          <p><strong>Type:</strong> {schedule.scheduleType}</p>
                          <p><strong>Time:</strong> {schedule.shiftStartTime} - {schedule.shiftEndTime}</p>
                          <p><strong>Duration:</strong> {schedule.durationHours} hours</p>
                          <p><strong>Guards:</strong> {schedule.guardsPerShift}</p>
                          <p><strong>Recurrence:</strong> {schedule.recurrenceType}</p>
                          {schedule.requiresArmedGuard && <span className=""badge"">Armed Guard Required</span>}
                          {schedule.requiresSupervisor && <span className=""badge"">Supervisor Required</span>}
                          {schedule.crossesMidnight && <span className=""badge"">Crosses Midnight</span>}
                          <p><strong>Status:</strong> {schedule.isActive ? 'Active' : 'Inactive'}</p>
                          {schedule.notes && <p><em>{schedule.notes}</em></p>}
                        </div>
                      ))}
                    </div>
                  )}
                </div>
              );
            };
            ```

            **Filtering by Location:**
            ```javascript
            const getShiftSchedulesForLocation = async (contractId, locationId) => {
              const response = await fetch(`/api/contracts/${contractId}/shift-schedules`);
              const data = await response.json();

              if (data.success) {
                // Filter schedules for specific location or global schedules (locationId = null)
                const locationSchedules = data.shiftSchedules.filter(
                  s => s.locationId === locationId || s.locationId === null
                );

                console.log(`Found ${locationSchedules.length} schedule(s) applicable to this location`);
                return locationSchedules;
              }

              return [];
            };
            ```

            **Filtering Active Schedules:**
            ```javascript
            const getActiveSchedules = async (contractId) => {
              const response = await fetch(`/api/contracts/${contractId}/shift-schedules`);
              const data = await response.json();

              if (data.success) {
                const now = new Date();
                const activeSchedules = data.shiftSchedules.filter(s => {
                  const effectiveFrom = new Date(s.effectiveFrom);
                  const effectiveTo = s.effectiveTo ? new Date(s.effectiveTo) : null;

                  return s.isActive &&
                         effectiveFrom <= now &&
                         (!effectiveTo || effectiveTo >= now);
                });

                console.log(`${activeSchedules.length} currently active schedule(s)`);
                return activeSchedules;
              }

              return [];
            };
            ```

            LƯU Ý:
            =======
            - Endpoint này trả về TẤT CẢ shift schedules (active và inactive)
            - Shift schedules được sắp xếp theo ScheduleName
            - Chỉ trả về schedules chưa bị xóa (IsDeleted = 0)
            - LocationId = null nghĩa là shift schedule áp dụng cho tất cả locations trong contract
            - LocationId có giá trị nghĩa là shift schedule chỉ áp dụng cho location cụ thể đó
            - Sử dụng endpoint này khi cần:
              - Hiển thị danh sách mẫu ca làm việc của contract
              - Quản lý shift schedule templates
              - Chọn shift schedule để tạo shift assignments
              - Phân tích workload và scheduling patterns

            USE CASES:
            ==========
            1. **Quản lý Shift Templates**: Xem tất cả mẫu ca đã định nghĩa cho contract
            2. **Scheduling**: Chọn shift schedule để tạo ca làm việc cho bảo vệ
            3. **Workload Planning**: Tính toán tổng số bảo vệ cần thiết và thời gian làm việc
            4. **Contract Review**: Kiểm tra các yêu cầu ca làm việc trong contract
            5. **Auto-generation Setup**: Xem các schedules có bật auto-generation
            6. **Location-specific Schedules**: Lọc schedules theo location cụ thể
        ");
    }
}
