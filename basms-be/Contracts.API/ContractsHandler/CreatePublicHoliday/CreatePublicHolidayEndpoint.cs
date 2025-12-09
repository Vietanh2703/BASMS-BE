namespace Contracts.API.ContractsHandler.CreatePublicHoliday;

/// <summary>
/// Endpoint để tạo mới public holiday
/// </summary>
public class CreatePublicHolidayEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // Route: POST /api/contracts/holidays
        app.MapPost("/api/contracts/holidays", async (
            CreatePublicHolidayRequest request,
            ISender sender,
            ILogger<CreatePublicHolidayEndpoint> logger) =>
        {
            try
            {
                logger.LogInformation("Create public holiday request: {HolidayName}", request.HolidayName);

                // Map request to command
                var command = new CreatePublicHolidayCommand
                {
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
                    logger.LogError("Failed to create holiday: {ErrorMessage}", result.ErrorMessage);
                    return Results.Problem(
                        title: "Error creating holiday",
                        detail: result.ErrorMessage,
                        statusCode: StatusCodes.Status400BadRequest
                    );
                }

                logger.LogInformation(
                    "Successfully created holiday {HolidayName} (ID: {HolidayId})",
                    result.HolidayName, result.HolidayId);

                return Results.Created(
                    $"/api/contracts/holidays/{result.HolidayId}",
                    result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing create holiday request");
                return Results.Problem(
                    title: "Error creating holiday",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status500InternalServerError
                );
            }
        })
        .RequireAuthorization()
        .WithTags("Contracts - Holidays")
        .WithName("CreatePublicHoliday")
        .Produces<CreatePublicHolidayResult>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Tạo mới public holiday")
        .WithDescription(@"
            Endpoint này tạo mới một public holiday trong hệ thống.
            Có thể tạo holiday áp dụng nationwide hoặc riêng cho từng contract.

            FLOW:
            1. Validate dữ liệu đầu vào
            2. Kiểm tra contract có tồn tại không (nếu có contractId)
            3. Kiểm tra holiday date có bị trùng không
            4. Tạo mới holiday vào database
            5. Trả về kết quả với holiday ID

            INPUT (CreatePublicHolidayRequest):
            Tất cả các trường giống UpdateHolidayPolicy, KHÔNG cần HolidayId

            OUTPUT (CreatePublicHolidayResult):
            - success: true/false
            - errorMessage: Thông báo lỗi (nếu có)
            - holidayId: GUID của holiday mới tạo
            - holidayName: Tên ngày lễ
            - holidayDate: Ngày lễ

            VÍ DỤ REQUEST - TẠO NGÀY LỄ NATIONWIDE:
            =========================================
            ```json
            POST /api/contracts/holidays
            {
              ""contractId"": null,
              ""holidayDate"": ""2026-04-30T00:00:00"",
              ""holidayName"": ""Ngày Giải phóng miền Nam"",
              ""holidayNameEn"": ""Reunification Day"",
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
              ""description"": ""Kỷ niệm ngày giải phóng miền Nam thống nhất đất nước"",
              ""year"": 2026
            }
            ```

            VÍ DỤ REQUEST - TẠO HOLIDAY CHO CONTRACT CỤ THỂ:
            =================================================
            ```json
            POST /api/contracts/holidays
            {
              ""contractId"": ""7c05f3c3-57f3-4000-b369-c2f3a1092a6e"",
              ""holidayDate"": ""2026-05-15T00:00:00"",
              ""holidayName"": ""Ngày nghỉ riêng của công ty"",
              ""holidayNameEn"": ""Company Holiday"",
              ""holidayCategory"": ""regional"",
              ""isTetPeriod"": false,
              ""isTetHoliday"": false,
              ""isOfficialHoliday"": false,
              ""isObserved"": true,
              ""appliesNationwide"": false,
              ""appliesToRegions"": null,
              ""standardWorkplacesClosed"": true,
              ""essentialServicesOperating"": false,
              ""description"": ""Ngày nghỉ đặc biệt theo hợp đồng"",
              ""year"": 2026
            }
            ```

            VÍ DỤ RESPONSE THÀNH CÔNG:
            ==========================
            ```json
            {
              ""success"": true,
              ""errorMessage"": null,
              ""holidayId"": ""a1b2c3d4-e5f6-7890-abcd-ef1234567890"",
              ""holidayName"": ""Ngày Giải phóng miền Nam"",
              ""holidayDate"": ""2026-04-30T00:00:00""
            }
            ```

            VÍ DỤ RESPONSE LỖI:
            ===================
            ```json
            {
              ""success"": false,
              ""errorMessage"": ""A holiday on 2026-04-30 already exists for year 2026"",
              ""holidayId"": null,
              ""holidayName"": null,
              ""holidayDate"": null
            }
            ```

            CÁCH SỬ DỤNG:
            =============

            **cURL:**
            ```bash
            curl -X POST 'http://localhost:5000/api/contracts/holidays' \
              -H 'Content-Type: application/json' \
              -d '{
                ""holidayDate"": ""2026-04-30T00:00:00"",
                ""holidayName"": ""Ngày Giải phóng miền Nam"",
                ""holidayNameEn"": ""Reunification Day"",
                ""holidayCategory"": ""national"",
                ""isTetPeriod"": false,
                ""isTetHoliday"": false,
                ""isOfficialHoliday"": true,
                ""isObserved"": true,
                ""appliesNationwide"": true,
                ""standardWorkplacesClosed"": true,
                ""essentialServicesOperating"": true,
                ""year"": 2026
              }'
            ```

            **JavaScript Fetch:**
            ```javascript
            const createHoliday = async (holidayData) => {
              const response = await fetch('/api/contracts/holidays', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(holidayData)
              });

              const result = await response.json();

              if (result.success) {
                console.log(`Created holiday: ${result.holidayName} (ID: ${result.holidayId})`);
              } else {
                console.error(`Error: ${result.errorMessage}`);
              }

              return result;
            };

            // Tạo holiday nationwide
            createHoliday({
              contractId: null,
              holidayDate: '2026-04-30T00:00:00',
              holidayName: 'Ngày Giải phóng miền Nam',
              holidayNameEn: 'Reunification Day',
              holidayCategory: 'national',
              isTetPeriod: false,
              isTetHoliday: false,
              isOfficialHoliday: true,
              isObserved: true,
              appliesNationwide: true,
              standardWorkplacesClosed: true,
              essentialServicesOperating: true,
              year: 2026
            });

            // Tạo holiday cho contract cụ thể
            createHoliday({
              contractId: '7c05f3c3-57f3-4000-b369-c2f3a1092a6e',
              holidayDate: '2026-05-15T00:00:00',
              holidayName: 'Ngày nghỉ riêng của công ty',
              holidayCategory: 'regional',
              isTetPeriod: false,
              isTetHoliday: false,
              isOfficialHoliday: false,
              isObserved: true,
              appliesNationwide: false,
              standardWorkplacesClosed: true,
              essentialServicesOperating: false,
              year: 2026
            });
            ```

            **React Example:**
            ```jsx
            const CreateHolidayForm = () => {
              const [formData, setFormData] = useState({
                contractId: null,
                isTetPeriod: false,
                isTetHoliday: false,
                isOfficialHoliday: true,
                isObserved: true,
                appliesNationwide: true,
                standardWorkplacesClosed: true,
                essentialServicesOperating: true,
                year: new Date().getFullYear()
              });
              const [result, setResult] = useState(null);

              const handleSubmit = async (e) => {
                e.preventDefault();

                const response = await fetch('/api/contracts/holidays', {
                  method: 'POST',
                  headers: { 'Content-Type': 'application/json' },
                  body: JSON.stringify(formData)
                });

                const data = await response.json();
                setResult(data);
              };

              return (
                <form onSubmit={handleSubmit}>
                  <input
                    type=""date""
                    onChange={(e) => setFormData({...formData, holidayDate: e.target.value})}
                    required
                  />
                  <input
                    name=""holidayName""
                    onChange={(e) => setFormData({...formData, holidayName: e.target.value})}
                    required
                  />
                  <select
                    name=""holidayCategory""
                    onChange={(e) => setFormData({...formData, holidayCategory: e.target.value})}
                    required
                  >
                    <option value=""national"">National</option>
                    <option value=""tet"">Tet</option>
                    <option value=""regional"">Regional</option>
                    <option value=""substitute"">Substitute</option>
                  </select>
                  {/* More fields... */}
                  <button type=""submit"">Create Holiday</button>

                  {result && (
                    <div>
                      {result.success ? (
                        <p>Created: {result.holidayName} (ID: {result.holidayId})</p>
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
            - ContractId = null → Holiday áp dụng nationwide cho tất cả contracts
            - ContractId = specific GUID → Holiday chỉ áp dụng cho contract đó
            - HolidayDate và Year phải unique trong scope (nationwide hoặc contract)
            - Đối với Tết, cần set đầy đủ: isTetPeriod, isTetHoliday, holidayStartDate, holidayEndDate
            - CreatedAt sẽ được tự động set
            - Response trả về status code 201 Created với Location header
        ");
    }
}

/// <summary>
/// Request model cho CreatePublicHoliday endpoint
/// </summary>
public record CreatePublicHolidayRequest
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
    public bool IsOfficialHoliday { get; init; } = true;
    public bool IsObserved { get; init; } = true;
    public DateTime? OriginalDate { get; init; }
    public DateTime? ObservedDate { get; init; }

    // Scope
    public bool AppliesNationwide { get; init; } = true;
    public string? AppliesToRegions { get; init; }

    // Impact
    public bool StandardWorkplacesClosed { get; init; } = true;
    public bool EssentialServicesOperating { get; init; } = true;

    public string? Description { get; init; }
    public int Year { get; init; }
}
