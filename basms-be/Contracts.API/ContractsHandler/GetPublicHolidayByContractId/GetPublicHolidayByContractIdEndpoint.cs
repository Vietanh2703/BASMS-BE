namespace Contracts.API.ContractsHandler.GetPublicHolidayByContractId;

/// <summary>
/// Endpoint để lấy danh sách public holidays theo contract ID
/// </summary>
public class GetPublicHolidayByContractIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // Route: GET /api/contracts/{contractId}/public-holidays
        app.MapGet("/api/contracts/{contractId}/public-holidays", async (
            Guid contractId,
            ISender sender,
            ILogger<GetPublicHolidayByContractIdEndpoint> logger) =>
        {
            try
            {
                logger.LogInformation("Get public holidays request for contract: {ContractId}", contractId);

                var query = new GetPublicHolidayByContractIdQuery(contractId);
                var result = await sender.Send(query);

                if (!result.Success)
                {
                    logger.LogWarning("Failed to get public holidays: {ErrorMessage}", result.ErrorMessage);
                    return Results.NotFound(new
                    {
                        success = false,
                        error = result.ErrorMessage
                    });
                }

                logger.LogInformation(
                    "Successfully retrieved {Count} public holiday(s) for contract {ContractCode}",
                    result.PublicHolidays.Count, result.ContractCode);

                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing get public holidays request for contract: {ContractId}", contractId);
                return Results.Problem(
                    title: "Error getting public holidays",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status500InternalServerError
                );
            }
        })
        .WithTags("Contracts - Public Holidays")
        .WithName("GetPublicHolidayByContractId")
        .Produces<GetPublicHolidayByContractIdResult>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Lấy danh sách public holidays theo contract ID")
        .WithDescription(@"
            Endpoint này trả về danh sách ngày lễ quốc gia áp dụng cho một contract.
            Bao gồm cả public holidays chung (ContractId = null) và holidays riêng cho contract.

            FLOW:
            1. Kiểm tra contract có tồn tại không
            2. Lấy danh sách public holidays (chung + riêng cho contract)
            3. Chỉ lấy holidays từ năm hiện tại trở đi
            4. Trả về danh sách holidays theo thứ tự ngày

            INPUT:
            - contractId: GUID của contract (trong URL path)

            OUTPUT (GetPublicHolidayByContractIdResult):
            - success: true/false
            - errorMessage: Thông báo lỗi (nếu có)
            - contractId: GUID của contract
            - contractCode: Mã contract (CTR-2025-XXX)
            - publicHolidays: Mảng các public holiday với thông tin:

              **Thông tin cơ bản:**
              - id: GUID của holiday
              - contractId: GUID của contract (null = áp dụng chung cho tất cả)
              - holidayDate: Ngày lễ
              - holidayName: Tên ngày lễ (tiếng Việt)
              - holidayNameEn: Tên tiếng Anh
              - holidayCategory: Loại lễ (national/tet/regional/substitute)

              **Tết đặc biệt:**
              - isTetPeriod: Có phải kỳ nghỉ Tết không
              - isTetHoliday: Có phải Tết Nguyên Đán không
              - tetDayNumber: Ngày thứ mấy của Tết (1=Mùng 1, 2=Mùng 2...)
              - holidayStartDate: Ngày bắt đầu nghỉ Tết
              - holidayEndDate: Ngày kết thúc nghỉ Tết
              - totalHolidayDays: Tổng số ngày nghỉ Tết

              **Quy định nghỉ:**
              - isOfficialHoliday: Ngày nghỉ chính thức theo luật
              - isObserved: Có được thực tế nghỉ không
              - originalDate: Ngày gốc (nếu bị dời)
              - observedDate: Ngày thực tế nghỉ (sau khi dời)

              **Phạm vi áp dụng:**
              - appliesNationwide: Áp dụng toàn quốc
              - appliesToRegions: Áp dụng cho khu vực nào (JSON array)

              **Ảnh hưởng công việc:**
              - standardWorkplacesClosed: Các công sở đóng cửa
              - essentialServicesOperating: Dịch vụ thiết yếu vẫn hoạt động
              - description: Mô tả ngày lễ
              - year: Năm

            VÍ DỤ REQUEST:
            ==============
            ```bash
            GET /api/contracts/a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d/public-holidays
            ```

            VÍ DỤ RESPONSE THÀNH CÔNG:
            ==========================
            ```json
            {
              ""success"": true,
              ""errorMessage"": null,
              ""contractId"": ""a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d"",
              ""contractCode"": ""CTR-2025-001"",
              ""publicHolidays"": [
                {
                  ""id"": ""h1o2l3i4-d5a6-4y07-8h09-1a2b3c4d5e6f"",
                  ""contractId"": null,
                  ""holidayDate"": ""2025-01-01T00:00:00"",
                  ""holidayName"": ""Tết Dương Lịch"",
                  ""holidayNameEn"": ""New Year's Day"",
                  ""holidayCategory"": ""national"",
                  ""isTetPeriod"": false,
                  ""isTetHoliday"": false,
                  ""tetDayNumber"": null,
                  ""holidayStartDate"": null,
                  ""holidayEndDate"": null,
                  ""totalHolidayDays"": null,
                  ""isOfficialHoliday"": true,
                  ""isObserved"": true,
                  ""originalDate"": null,
                  ""observedDate"": null,
                  ""appliesNationwide"": true,
                  ""appliesToRegions"": null,
                  ""standardWorkplacesClosed"": true,
                  ""essentialServicesOperating"": true,
                  ""description"": ""Ngày đầu năm dương lịch"",
                  ""year"": 2025
                },
                {
                  ""id"": ""t2e3t4n5-g6u7-4y08-9e10-2b3c4d5e6f7g"",
                  ""contractId"": null,
                  ""holidayDate"": ""2025-01-29T00:00:00"",
                  ""holidayName"": ""Tết Nguyên Đán - Mùng 1"",
                  ""holidayNameEn"": ""Lunar New Year - Day 1"",
                  ""holidayCategory"": ""tet"",
                  ""isTetPeriod"": true,
                  ""isTetHoliday"": true,
                  ""tetDayNumber"": 1,
                  ""holidayStartDate"": ""2025-01-29T00:00:00"",
                  ""holidayEndDate"": ""2025-02-04T00:00:00"",
                  ""totalHolidayDays"": 7,
                  ""isOfficialHoliday"": true,
                  ""isObserved"": true,
                  ""originalDate"": null,
                  ""observedDate"": null,
                  ""appliesNationwide"": true,
                  ""appliesToRegions"": null,
                  ""standardWorkplacesClosed"": true,
                  ""essentialServicesOperating"": true,
                  ""description"": ""Tết Nguyên Đán - Năm Ất Tỵ 2025, nghỉ 7 ngày từ 29/01 đến 04/02"",
                  ""year"": 2025
                },
                {
                  ""id"": ""g3i4o5h6-u7n8-5g09-ah11-3c4d5e6f7g8h"",
                  ""contractId"": null,
                  ""holidayDate"": ""2025-04-10T00:00:00"",
                  ""holidayName"": ""Giỗ Tổ Hùng Vương"",
                  ""holidayNameEn"": ""Hung Kings' Festival"",
                  ""holidayCategory"": ""national"",
                  ""isTetPeriod"": false,
                  ""isTetHoliday"": false,
                  ""tetDayNumber"": null,
                  ""holidayStartDate"": null,
                  ""holidayEndDate"": null,
                  ""totalHolidayDays"": null,
                  ""isOfficialHoliday"": true,
                  ""isObserved"": true,
                  ""originalDate"": null,
                  ""observedDate"": null,
                  ""appliesNationwide"": true,
                  ""appliesToRegions"": null,
                  ""standardWorkplacesClosed"": true,
                  ""essentialServicesOperating"": true,
                  ""description"": ""Ngày giỗ tổ Hùng Vương (10/3 Âm lịch)"",
                  ""year"": 2025
                },
                {
                  ""id"": ""r4e5u6n7-i8o9-6n10-bi12-4d5e6f7g8h9i"",
                  ""contractId"": null,
                  ""holidayDate"": ""2025-04-30T00:00:00"",
                  ""holidayName"": ""Ngày Giải Phóng Miền Nam"",
                  ""holidayNameEn"": ""Reunification Day"",
                  ""holidayCategory"": ""national"",
                  ""isTetPeriod"": false,
                  ""isTetHoliday"": false,
                  ""tetDayNumber"": null,
                  ""holidayStartDate"": null,
                  ""holidayEndDate"": null,
                  ""totalHolidayDays"": null,
                  ""isOfficialHoliday"": true,
                  ""isObserved"": true,
                  ""originalDate"": null,
                  ""observedDate"": null,
                  ""appliesNationwide"": true,
                  ""appliesToRegions"": null,
                  ""standardWorkplacesClosed"": true,
                  ""essentialServicesOperating"": true,
                  ""description"": ""Ngày giải phóng miền Nam, thống nhất đất nước"",
                  ""year"": 2025
                },
                {
                  ""id"": ""l5a6b7o8-r9d10-7a11-cy13-5e6f7g8h9i0j"",
                  ""contractId"": null,
                  ""holidayDate"": ""2025-05-01T00:00:00"",
                  ""holidayName"": ""Ngày Quốc Tế Lao Động"",
                  ""holidayNameEn"": ""International Labor Day"",
                  ""holidayCategory"": ""national"",
                  ""isTetPeriod"": false,
                  ""isTetHoliday"": false,
                  ""tetDayNumber"": null,
                  ""holidayStartDate"": null,
                  ""holidayEndDate"": null,
                  ""totalHolidayDays"": null,
                  ""isOfficialHoliday"": true,
                  ""isObserved"": true,
                  ""originalDate"": null,
                  ""observedDate"": null,
                  ""appliesNationwide"": true,
                  ""appliesToRegions"": null,
                  ""standardWorkplacesClosed"": true,
                  ""essentialServicesOperating"": true,
                  ""description"": ""Ngày Quốc tế Lao động"",
                  ""year"": 2025
                },
                {
                  ""id"": ""n6a7t8i9-o10n11-8a12-ld14-6f7g8h9i0j1k"",
                  ""contractId"": null,
                  ""holidayDate"": ""2025-09-02T00:00:00"",
                  ""holidayName"": ""Quốc Khánh"",
                  ""holidayNameEn"": ""National Day"",
                  ""holidayCategory"": ""national"",
                  ""isTetPeriod"": false,
                  ""isTetHoliday"": false,
                  ""tetDayNumber"": null,
                  ""holidayStartDate"": null,
                  ""holidayEndDate"": null,
                  ""totalHolidayDays"": null,
                  ""isOfficialHoliday"": true,
                  ""isObserved"": true,
                  ""originalDate"": null,
                  ""observedDate"": null,
                  ""appliesNationwide"": true,
                  ""appliesToRegions"": null,
                  ""standardWorkplacesClosed"": true,
                  ""essentialServicesOperating"": true,
                  ""description"": ""Ngày Quốc khánh nước Cộng hòa Xã hội chủ nghĩa Việt Nam"",
                  ""year"": 2025
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

            CÁCH SỬ DỤNG:
            =============

            **cURL:**
            ```bash
            curl -X GET 'http://localhost:5000/api/contracts/a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d/public-holidays'
            ```

            **JavaScript Fetch:**
            ```javascript
            const getPublicHolidaysByContract = async (contractId) => {
              const response = await fetch(`/api/contracts/${contractId}/public-holidays`);
              const result = await response.json();

              if (result.success) {
                console.log(`Found ${result.publicHolidays.length} public holiday(s) for ${result.contractCode}`);
                result.publicHolidays.forEach(holiday => {
                  console.log(`- ${holiday.holidayName} (${holiday.holidayDate.split('T')[0]})`);
                  if (holiday.isTetPeriod) {
                    console.log(`  Tết: ${holiday.totalHolidayDays} ngày nghỉ`);
                  }
                });
              } else {
                console.error(`Error: ${result.error}`);
              }

              return result;
            };

            // Sử dụng
            getPublicHolidaysByContract('a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d');
            ```

            **React Example - Holiday Calendar:**
            ```jsx
            const HolidayCalendar = ({ contractId }) => {
              const [holidays, setHolidays] = useState([]);
              const [loading, setLoading] = useState(true);

              useEffect(() => {
                const fetchHolidays = async () => {
                  const response = await fetch(`/api/contracts/${contractId}/public-holidays`);
                  const data = await response.json();

                  if (data.success) {
                    setHolidays(data.publicHolidays);
                  }
                  setLoading(false);
                };

                fetchHolidays();
              }, [contractId]);

              if (loading) return <div>Loading holidays...</div>;

              const tetHolidays = holidays.filter(h => h.isTetPeriod);
              const nationalHolidays = holidays.filter(h => !h.isTetPeriod);

              return (
                <div className=""holiday-calendar"">
                  <h3>Public Holidays</h3>

                  {tetHolidays.length > 0 && (
                    <div className=""tet-section"">
                      <h4>Tết Holidays</h4>
                      {tetHolidays.map(holiday => (
                        <div key={holiday.id} className=""holiday-card tet"">
                          <h5>{holiday.holidayName}</h5>
                          <p><strong>Date:</strong> {new Date(holiday.holidayDate).toLocaleDateString()}</p>
                          <p><strong>Duration:</strong> {holiday.totalHolidayDays} days</p>
                          <p><strong>Period:</strong> {new Date(holiday.holidayStartDate).toLocaleDateString()} - {new Date(holiday.holidayEndDate).toLocaleDateString()}</p>
                          {holiday.description && <p>{holiday.description}</p>}
                        </div>
                      ))}
                    </div>
                  )}

                  <div className=""national-section"">
                    <h4>National Holidays</h4>
                    {nationalHolidays.map(holiday => (
                      <div key={holiday.id} className=""holiday-card"">
                        <h5>{holiday.holidayName}</h5>
                        <p className=""date"">{new Date(holiday.holidayDate).toLocaleDateString()}</p>
                        <p className=""category"">{holiday.holidayCategory}</p>
                        {holiday.description && <p className=""description"">{holiday.description}</p>}
                        {holiday.standardWorkplacesClosed && <span className=""badge"">Offices Closed</span>}
                        {holiday.essentialServicesOperating && <span className=""badge"">Essential Services Active</span>}
                      </div>
                    ))}
                  </div>
                </div>
              );
            };
            ```

            **Filtering by Year:**
            ```javascript
            const getHolidaysByYear = async (contractId, year) => {
              const response = await fetch(`/api/contracts/${contractId}/public-holidays`);
              const data = await response.json();

              if (data.success) {
                const yearHolidays = data.publicHolidays.filter(h => h.year === year);
                console.log(`${yearHolidays.length} holiday(s) in ${year}`);
                return yearHolidays;
              }

              return [];
            };
            ```

            **Check if Date is Holiday:**
            ```javascript
            const isHoliday = (holidays, date) => {
              const dateStr = date.toISOString().split('T')[0];
              return holidays.some(h => h.holidayDate.startsWith(dateStr));
            };

            const isTetPeriod = (holidays, date) => {
              return holidays.some(h => {
                if (!h.isTetPeriod) return false;
                const checkDate = new Date(date);
                const start = new Date(h.holidayStartDate);
                const end = new Date(h.holidayEndDate);
                return checkDate >= start && checkDate <= end;
              });
            };
            ```

            LƯU Ý:
            =======
            - Endpoint này trả về holidays từ năm hiện tại trở đi (Year >= YEAR(NOW()))
            - Bao gồm cả public holidays chung (ContractId = null) và holidays riêng cho contract
            - ContractId = null nghĩa là holiday áp dụng cho tất cả contracts
            - Holidays được sắp xếp theo HolidayDate
            - Tết Nguyên Đán có thông tin đặc biệt về kỳ nghỉ dài ngày
            - Sử dụng endpoint này khi cần:
              - Hiển thị lịch ngày lễ cho contract
              - Planning shift schedules tránh ngày lễ
              - Tính lương overtime cho ngày lễ
              - Kiểm tra ngày làm việc/nghỉ lễ
              - Schedule attendance và time-off

            USE CASES:
            ==========
            1. **Shift Planning**: Lập lịch ca làm việc tránh ngày lễ hoặc tăng cường nhân sự
            2. **Payroll Calculation**: Tính lương overtime cho ngày lễ (thường gấp 2-3 lần)
            3. **Attendance Management**: Đánh dấu ngày nghỉ lễ trong hệ thống chấm công
            4. **Contract Review**: Xác định các ngày lễ áp dụng cho hợp đồng
            5. **Holiday Calendar**: Hiển thị lịch nghỉ lễ cho nhân viên bảo vệ
            6. **Tết Planning**: Lên kế hoạch đặc biệt cho kỳ nghỉ Tết (7 ngày)
        ");
    }
}
