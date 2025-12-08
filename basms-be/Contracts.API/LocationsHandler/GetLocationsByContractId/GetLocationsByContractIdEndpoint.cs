namespace Contracts.API.LocationsHandler.GetLocationsByContractId;

/// <summary>
/// Endpoint để lấy tất cả locations của một contract
/// </summary>
public class GetLocationsByContractIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // Route: GET /api/locations/contracts/{contractId}
        app.MapGet("/api/locations/contracts/{contractId}", async (
            Guid contractId,
            ISender sender,
            ILogger<GetLocationsByContractIdEndpoint> logger) =>
        {
            try
            {
                logger.LogInformation("Get locations request for contract: {ContractId}", contractId);

                var query = new GetLocationsByContractIdQuery(contractId);
                var result = await sender.Send(query);

                if (!result.Success)
                {
                    logger.LogWarning("Failed to get locations: {ErrorMessage}", result.ErrorMessage);
                    return Results.NotFound(new
                    {
                        success = false,
                        error = result.ErrorMessage
                    });
                }

                logger.LogInformation(
                    "Successfully retrieved {Count} location(s) for contract {ContractNumber}",
                    result.TotalLocations, result.ContractNumber);

                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing get locations request for contract: {ContractId}", contractId);
                return Results.Problem(
                    title: "Error getting locations",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status500InternalServerError
                );
            }
        })
        .WithTags("Locations")
        .WithName("GetLocationsByContractId")
        .Produces<GetLocationsByContractIdResult>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Lấy tất cả locations của một contract")
        .WithDescription(@"
            Endpoint này trả về danh sách tất cả locations thuộc về một contract,
            bao gồm cả thông tin từ customer_locations và contract_locations.

            FLOW:
            1. Kiểm tra contract có tồn tại không
            2. JOIN contract_locations với customer_locations
            3. Trả về full location details với contract-specific info

            INPUT:
            - contractId: GUID của contract (trong URL path)

            OUTPUT (GetLocationsByContractIdResult):
            - success: true/false
            - errorMessage: Thông báo lỗi (nếu có)
            - contractId: GUID của contract
            - contractNumber: Số hợp đồng
            - totalLocations: Tổng số locations
            - locations: Mảng các location với FULL DETAILS bao gồm:

              ## From customer_locations (master data):
              - id: Location GUID
              - customerId: Customer GUID
              - locationCode: Mã địa điểm (LOC-001)
              - locationName: Tên địa điểm
              - locationType: Loại (office, warehouse, factory...)
              - address, city, district, ward: Địa chỉ đầy đủ
              - latitude, longitude, geofenceRadiusMeters: Geofencing
              - siteManagerName, siteManagerPhone: Quản lý tại chỗ
              - emergencyContactName, emergencyContactPhone: Liên hệ khẩn cấp
              - operatingHoursType: Loại giờ hoạt động (24/7, business_hours...)
              - totalAreaSqm, buildingFloors: Thông tin vật lý
              - requires24x7Coverage, minimumGuardsRequired: Yêu cầu bảo vệ

              ## From contract_locations (contract-specific):
              - contractLocationId: GUID của contract_location record
              - guardsRequired: Số bảo vệ cần cho địa điểm này
              - coverageType: Loại coverage (24x7, day_only, night_only...)
              - serviceStartDate, serviceEndDate: Thời gian dịch vụ tại địa điểm
              - isPrimaryLocation: Có phải địa điểm chính không
              - priorityLevel: Mức độ ưu tiên (1=cao, 2=trung, 3=thấp)
              - autoGenerateShifts: Tự động tạo ca không

            VÍ DỤ REQUEST:
            ==============
            ```bash
            GET /api/locations/contracts/7c05f3c3-57f3-4000-b369-c2f3a1092a6e
            ```

            VÍ DỤ RESPONSE THÀNH CÔNG:
            ==========================
            ```json
            {
              ""success"": true,
              ""contractId"": ""7c05f3c3-57f3-4000-b369-c2f3a1092a6e"",
              ""contractNumber"": ""CTR-20251129-2520"",
              ""totalLocations"": 2,
              ""locations"": [
                {
                  ""id"": ""loc-guid-1"",
                  ""customerId"": ""cust-guid"",
                  ""locationCode"": ""LOC-001"",
                  ""locationName"": ""Chi nhánh Quận 1"",
                  ""locationType"": ""office"",
                  ""address"": ""123 Nguyễn Huệ, Quận 1, TP.HCM"",
                  ""city"": ""TP.HCM"",
                  ""district"": ""Quận 1"",
                  ""latitude"": 10.762622,
                  ""longitude"": 106.660172,
                  ""geofenceRadiusMeters"": 100,
                  ""siteManagerName"": ""Nguyễn Văn A"",
                  ""siteManagerPhone"": ""0901234567"",
                  ""operatingHoursType"": ""business_hours"",
                  ""totalAreaSqm"": 500.0,
                  ""requires24x7Coverage"": false,
                  ""minimumGuardsRequired"": 2,
                  ""contractLocationId"": ""cl-guid-1"",
                  ""guardsRequired"": 2,
                  ""coverageType"": ""24x7"",
                  ""serviceStartDate"": ""2025-11-30T00:00:00"",
                  ""serviceEndDate"": null,
                  ""isPrimaryLocation"": true,
                  ""priorityLevel"": 1,
                  ""autoGenerateShifts"": true,
                  ""isActive"": true
                },
                {
                  ""id"": ""loc-guid-2"",
                  ""locationName"": ""Chi nhánh Quận 3"",
                  ""isPrimaryLocation"": false,
                  ""priorityLevel"": 2,
                  ...
                }
              ]
            }
            ```

            VÍ DỤ RESPONSE KHI KHÔNG TÌM THẤY:
            ===================================
            ```json
            {
              ""success"": false,
              ""error"": ""Contract with ID ... not found""
            }
            ```

            CÁCH SỬ DỤNG:
            =============

            **cURL:**
            ```bash
            curl -X GET 'http://localhost:5002/api/locations/contracts/7c05f3c3-57f3-4000-b369-c2f3a1092a6e'
            ```

            **JavaScript Fetch:**
            ```javascript
            const getLocationsByContract = async (contractId) => {
              const response = await fetch(`/api/locations/contracts/${contractId}`);
              const data = await response.json();

              if (data.success) {
                console.log(`Contract ${data.contractNumber} has ${data.totalLocations} locations:`);
                data.locations.forEach(loc => {
                  console.log(`- ${loc.locationName} (${loc.locationCode})`);
                  console.log(`  Guards required: ${loc.guardsRequired}`);
                  console.log(`  Coverage: ${loc.coverageType}`);
                  console.log(`  Primary: ${loc.isPrimaryLocation}`);
                });
              }

              return data;
            };
            ```

            **React Example:**
            ```jsx
            const ContractLocations = ({ contractId }) => {
              const [data, setData] = useState(null);
              const [loading, setLoading] = useState(true);

              useEffect(() => {
                const fetchLocations = async () => {
                  const response = await fetch(`/api/locations/contracts/${contractId}`);
                  const result = await response.json();

                  if (result.success) {
                    setData(result);
                  }
                  setLoading(false);
                };

                fetchLocations();
              }, [contractId]);

              if (loading) return <div>Loading...</div>;
              if (!data) return <div>No data</div>;

              return (
                <div>
                  <h2>Locations for {data.contractNumber}</h2>
                  <p>Total: {data.totalLocations} location(s)</p>

                  {data.locations.map(loc => (
                    <div key={loc.id} className={loc.isPrimaryLocation ? 'primary' : ''}>
                      <h3>{loc.locationName}</h3>
                      <p>Code: {loc.locationCode}</p>
                      <p>Type: {loc.locationType}</p>
                      <p>Address: {loc.address}</p>
                      <p>Guards: {loc.guardsRequired}</p>
                      <p>Coverage: {loc.coverageType}</p>
                      {loc.isPrimaryLocation && <span className=""badge"">Primary</span>}
                    </div>
                  ))}
                </div>
              );
            };
            ```

            LƯU Ý:
            =======
            - Locations được sắp xếp theo:
              1. isPrimaryLocation (primary location trước)
              2. priorityLevel (priority thấp trước: 1, 2, 3...)
              3. locationName (A-Z)
            - Chỉ trả về locations chưa bị xóa (IsDeleted = 0)
            - Kết hợp data từ 2 tables: customer_locations + contract_locations
            - Địa điểm primary có isPrimaryLocation = true
        ");
    }
}
