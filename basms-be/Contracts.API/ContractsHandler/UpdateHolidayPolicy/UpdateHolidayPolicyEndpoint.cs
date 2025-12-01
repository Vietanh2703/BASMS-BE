namespace Contracts.API.ContractsHandler.UpdateHolidayPolicy;

/// <summary>
/// Endpoint để update thông tin public holiday policy
/// </summary>
public class UpdateHolidayPolicyEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // Route: PUT /api/contracts/holidays/{holidayId}
        app.MapPut("/api/contracts/holidays/{holidayId}", async (
            Guid holidayId,
            UpdateHolidayPolicyRequest request,
            ISender sender,
            ILogger<UpdateHolidayPolicyEndpoint> logger) =>
        {
            try
            {
                logger.LogInformation("Update holiday policy request for ID: {HolidayId}", holidayId);

                // Map request to command
                var command = new UpdateHolidayPolicyCommand
                {
                    HolidayId = holidayId,
                    ContractId = request.ContractId,
                    HolidayDate = request.HolidayDate,
                    HolidayName = request.HolidayName,
                    HolidayNameEn = request.HolidayNameEn,
                    HolidayCategory = request.HolidayCategory,
                    IsTetPeriod = request.IsTetPeriod,
                    IsTetHoliday = request.IsTetHoliday,
                    TetDayNumber = request.TetDayNumber,
                    HolidayStartDate = request.HolidayStartDate,
                    HolidayEndDate = request.HolidayEndDate,
                    TotalHolidayDays = request.TotalHolidayDays,
                    IsOfficialHoliday = request.IsOfficialHoliday,
                    IsObserved = request.IsObserved,
                    OriginalDate = request.OriginalDate,
                    ObservedDate = request.ObservedDate,
                    AppliesNationwide = request.AppliesNationwide,
                    AppliesToRegions = request.AppliesToRegions,
                    StandardWorkplacesClosed = request.StandardWorkplacesClosed,
                    EssentialServicesOperating = request.EssentialServicesOperating,
                    Description = request.Description,
                    Year = request.Year
                };

                var result = await sender.Send(command);

                if (!result.Success)
                {
                    logger.LogError("Failed to update holiday policy: {ErrorMessage}", result.ErrorMessage);
                    return Results.Problem(
                        title: "Error updating holiday policy",
                        detail: result.ErrorMessage,
                        statusCode: StatusCodes.Status400BadRequest
                    );
                }

                logger.LogInformation(
                    "Successfully updated holiday {HolidayName} (ID: {HolidayId})",
                    result.HolidayName, result.HolidayId);

                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing update holiday policy request for ID: {HolidayId}", holidayId);
                return Results.Problem(
                    title: "Error updating holiday policy",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status500InternalServerError
                );
            }
        })
        .WithTags("Contracts - Holidays")
        .WithName("UpdateHolidayPolicy")
        .Produces<UpdateHolidayPolicyResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Update thông tin public holiday policy")
        .WithDescription(@"
            Endpoint này cập nhật thông tin chi tiết của một public holiday trong hệ thống.
            Hỗ trợ update tất cả các trường thông tin của ngày lễ.

            FLOW:
            1. Validate dữ liệu đầu vào (holiday date, name, category...)
            2. Kiểm tra holiday có tồn tại không
            3. Kiểm tra holiday date có bị trùng với holiday khác không (trong cùng năm)
            4. Update thông tin holiday vào database
            5. Trả về kết quả

            INPUT (UpdateHolidayPolicyRequest):
            - contractId: ID của contract liên kết (optional, null = nationwide)
            - holidayDate: Ngày lễ (required, format: yyyy-MM-dd)
            - holidayName: Tên ngày lễ tiếng Việt (required, max 200 chars)
            - holidayNameEn: Tên tiếng Anh (optional, max 200 chars)
            - holidayCategory: Loại ngày lễ (required, values: national, tet, regional, substitute)

            TET PERIOD:
            - isTetPeriod: Có phải kỳ Tết không (required, boolean)
            - isTetHoliday: Có phải Tết Nguyên Đán không (required, boolean)
            - tetDayNumber: Ngày thứ mấy của Tết (optional, 1-10)
            - holidayStartDate: Ngày bắt đầu nghỉ Tết (optional, required if isTetPeriod = true)
            - holidayEndDate: Ngày kết thúc nghỉ Tết (optional, required if isTetPeriod = true)
            - totalHolidayDays: Tổng số ngày nghỉ (optional, 1-30)

            OFFICIAL & OBSERVED:
            - isOfficialHoliday: Có phải ngày lễ chính thức không (required, boolean)
            - isObserved: Có thực tế nghỉ không (required, boolean)
            - originalDate: Ngày gốc nếu bị dời (optional)
            - observedDate: Ngày thực tế nghỉ (optional)

            SCOPE:
            - appliesNationwide: Áp dụng toàn quốc (required, boolean)
            - appliesToRegions: Khu vực áp dụng (optional, JSON array, max 500 chars)

            IMPACT:
            - standardWorkplacesClosed: Công sở đóng cửa (required, boolean)
            - essentialServicesOperating: Dịch vụ thiết yếu hoạt động (required, boolean)

            OTHER:
            - description: Mô tả ngày lễ (optional, max 1000 chars)
            - year: Năm (required, 2020-2100)

            OUTPUT (UpdateHolidayPolicyResult):
            - success: true/false
            - errorMessage: Thông báo lỗi (nếu có)
            - holidayId: GUID của holiday đã update
            - holidayName: Tên ngày lễ
            - holidayDate: Ngày lễ

            VÍ DỤ REQUEST - NGÀY LỄ THÔNG THƯỜNG:
            ======================================
            ```json
            PUT /api/contracts/holidays/6f07c4d8-0258-4f6c-8659-a40adbccf8a8
            {
              ""contractId"": null,
              ""holidayDate"": ""2025-09-02T00:00:00"",
              ""holidayName"": ""Ngày Quốc khánh"",
              ""holidayNameEn"": ""National Day"",
              ""holidayCategory"": ""national"",
              ""isTetPeriod"": false,
              ""isTetHoliday"": false,
              ""tetDayNumber"": null,
              ""holidayStartDate"": null,
              ""holidayEndDate"": null,
              ""totalHolidayDays"": 1,
              ""isOfficialHoliday"": true,
              ""isObserved"": true,
              ""originalDate"": null,
              ""observedDate"": null,
              ""appliesNationwide"": true,
              ""appliesToRegions"": null,
              ""standardWorkplacesClosed"": true,
              ""essentialServicesOperating"": true,
              ""description"": ""Kỷ niệm ngày thành lập nước Việt Nam Dân chủ Cộng hòa"",
              ""year"": 2025
            }
            ```

            VÍ DỤ REQUEST - TẾT NGUYÊN ĐÁN:
            ================================
            ```json
            PUT /api/contracts/holidays/6f07c4d8-0258-4f6c-8659-a40adbccf8a8
            {
              ""contractId"": null,
              ""holidayDate"": ""2025-01-29T00:00:00"",
              ""holidayName"": ""Tết Nguyên Đán"",
              ""holidayNameEn"": ""Lunar New Year"",
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
              ""description"": ""Tết Nguyên Đán - Tết cổ truyền của dân tộc Việt Nam"",
              ""year"": 2025
            }
            ```

            VÍ DỤ RESPONSE THÀNH CÔNG:
            ==========================
            ```json
            {
              ""success"": true,
              ""errorMessage"": null,
              ""holidayId"": ""6f07c4d8-0258-4f6c-8659-a40adbccf8a8"",
              ""holidayName"": ""Tết Nguyên Đán"",
              ""holidayDate"": ""2025-01-29T00:00:00""
            }
            ```

            VÍ DỤ RESPONSE LỖI:
            ===================
            ```json
            {
              ""success"": false,
              ""errorMessage"": ""A holiday on 2025-01-29 already exists for year 2025"",
              ""holidayId"": null,
              ""holidayName"": null,
              ""holidayDate"": null
            }
            ```

            VALIDATION RULES:
            =================
            1. HolidayName: Bắt buộc, tối đa 200 ký tự
            2. HolidayDate: Bắt buộc
            3. HolidayCategory: Bắt buộc, chỉ chấp nhận national, tet, regional, substitute
            4. Year: Bắt buộc, 2020-2100
            5. TetDayNumber: Optional, 1-10
            6. TotalHolidayDays: Optional, 1-30
            7. Nếu IsTetHoliday = true thì IsTetPeriod phải = true
            8. Nếu IsTetPeriod = true thì HolidayStartDate và HolidayEndDate bắt buộc
            9. HolidayStartDate phải <= HolidayEndDate
            10. Không được trùng HolidayDate trong cùng năm

            CÁCH SỬ DỤNG:
            =============

            **cURL:**
            ```bash
            curl -X PUT 'http://localhost:5000/api/contracts/holidays/6f07c4d8-0258-4f6c-8659-a40adbccf8a8' \
              -H 'Content-Type: application/json' \
              -d '{
                ""holidayDate"": ""2025-09-02T00:00:00"",
                ""holidayName"": ""Ngày Quốc khánh"",
                ""holidayNameEn"": ""National Day"",
                ""holidayCategory"": ""national"",
                ""isTetPeriod"": false,
                ""isTetHoliday"": false,
                ""isOfficialHoliday"": true,
                ""isObserved"": true,
                ""appliesNationwide"": true,
                ""standardWorkplacesClosed"": true,
                ""essentialServicesOperating"": true,
                ""year"": 2025
              }'
            ```

            **JavaScript Fetch:**
            ```javascript
            const updateHolidayPolicy = async (holidayId, holidayData) => {
              const response = await fetch(`/api/contracts/holidays/${holidayId}`, {
                method: 'PUT',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(holidayData)
              });

              const result = await response.json();

              if (result.success) {
                console.log(`Updated holiday: ${result.holidayName}`);
              } else {
                console.error(`Error: ${result.errorMessage}`);
              }

              return result;
            };

            // Sử dụng - Update ngày lễ thông thường
            updateHolidayPolicy('6f07c4d8-0258-4f6c-8659-a40adbccf8a8', {
              contractId: null,
              holidayDate: '2025-09-02T00:00:00',
              holidayName: 'Ngày Quốc khánh',
              holidayNameEn: 'National Day',
              holidayCategory: 'national',
              isTetPeriod: false,
              isTetHoliday: false,
              isOfficialHoliday: true,
              isObserved: true,
              appliesNationwide: true,
              standardWorkplacesClosed: true,
              essentialServicesOperating: true,
              year: 2025
            });

            // Sử dụng - Update Tết Nguyên Đán
            updateHolidayPolicy('6f07c4d8-0258-4f6c-8659-a40adbccf8a8', {
              contractId: null,
              holidayDate: '2025-01-29T00:00:00',
              holidayName: 'Tết Nguyên Đán',
              holidayNameEn: 'Lunar New Year',
              holidayCategory: 'tet',
              isTetPeriod: true,
              isTetHoliday: true,
              tetDayNumber: 1,
              holidayStartDate: '2025-01-29T00:00:00',
              holidayEndDate: '2025-02-04T00:00:00',
              totalHolidayDays: 7,
              isOfficialHoliday: true,
              isObserved: true,
              appliesNationwide: true,
              standardWorkplacesClosed: true,
              essentialServicesOperating: true,
              description: 'Tết Nguyên Đán - Tết cổ truyền của dân tộc Việt Nam',
              year: 2025
            });
            ```

            **React Example:**
            ```jsx
            const UpdateHolidayPolicyForm = ({ holidayId }) => {
              const [formData, setFormData] = useState({
                isTetPeriod: false,
                isTetHoliday: false,
                isOfficialHoliday: true,
                isObserved: true,
                appliesNationwide: true,
                standardWorkplacesClosed: true,
                essentialServicesOperating: true
              });
              const [result, setResult] = useState(null);

              const handleSubmit = async (e) => {
                e.preventDefault();

                const response = await fetch(`/api/contracts/holidays/${holidayId}`, {
                  method: 'PUT',
                  headers: { 'Content-Type': 'application/json' },
                  body: JSON.stringify(formData)
                });

                const data = await response.json();
                setResult(data);
              };

              return (
                <form onSubmit={handleSubmit}>
                  <input
                    name=""holidayName""
                    onChange={(e) => setFormData({...formData, holidayName: e.target.value})}
                    required
                  />
                  <input
                    type=""date""
                    name=""holidayDate""
                    onChange={(e) => setFormData({...formData, holidayDate: e.target.value})}
                    required
                  />
                  {/* More fields... */}
                  <button type=""submit"">Update Holiday</button>

                  {result && (
                    <div>
                      {result.success ? (
                        <p>Updated: {result.holidayName}</p>
                      ) : (
                        <p>Error: {result.errorMessage}</p>
                      )}
                    </div>
                  )}
                </form>
              );
            };
            ```

            LƯU Ý:
            =======
            - Update tất cả các trường thông tin của public holiday
            - HolidayDate và Year phải unique (không trùng với holiday khác trong cùng năm)
            - Đối với Tết Nguyên Đán, cần set đầy đủ: isTetPeriod, isTetHoliday, holidayStartDate, holidayEndDate
            - ContractId = null nghĩa là holiday áp dụng nationwide
            - AppliesToRegions có thể là JSON array để chỉ định khu vực cụ thể
            - UpdatedAt sẽ được tự động cập nhật
            - Validation được thực hiện bởi FluentValidation
        ");
    }
}

/// <summary>
/// Request model cho UpdateHolidayPolicy endpoint
/// </summary>
public record UpdateHolidayPolicyRequest
{
    public Guid? ContractId { get; init; }
    public DateTime HolidayDate { get; init; }
    public string HolidayName { get; init; } = string.Empty;
    public string? HolidayNameEn { get; init; }
    public string HolidayCategory { get; init; } = string.Empty;

    // Tet Period
    public bool IsTetPeriod { get; init; }
    public bool IsTetHoliday { get; init; }
    public int? TetDayNumber { get; init; }
    public DateTime? HolidayStartDate { get; init; }
    public DateTime? HolidayEndDate { get; init; }
    public int? TotalHolidayDays { get; init; }

    // Official & Observed
    public bool IsOfficialHoliday { get; init; }
    public bool IsObserved { get; init; }
    public DateTime? OriginalDate { get; init; }
    public DateTime? ObservedDate { get; init; }

    // Scope
    public bool AppliesNationwide { get; init; }
    public string? AppliesToRegions { get; init; }

    // Impact
    public bool StandardWorkplacesClosed { get; init; }
    public bool EssentialServicesOperating { get; init; }

    public string? Description { get; init; }
    public int Year { get; init; }
}
