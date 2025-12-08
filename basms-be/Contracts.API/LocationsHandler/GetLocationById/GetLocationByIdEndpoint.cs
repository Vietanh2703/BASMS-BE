namespace Contracts.API.LocationsHandler.GetLocationById;

/// <summary>
/// Endpoint để lấy chi tiết một location theo ID
/// </summary>
public class GetLocationByIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // Route: GET /api/locations/{locationId}
        app.MapGet("/api/locations/{locationId}", async (
            Guid locationId,
            ISender sender,
            ILogger<GetLocationByIdEndpoint> logger) =>
        {
            try
            {
                logger.LogInformation("Get location detail request for: {LocationId}", locationId);

                var query = new GetLocationByIdQuery(locationId);
                var result = await sender.Send(query);

                if (!result.Success)
                {
                    logger.LogWarning("Failed to get location: {ErrorMessage}", result.ErrorMessage);
                    return Results.NotFound(new
                    {
                        success = false,
                        error = result.ErrorMessage
                    });
                }

                logger.LogInformation(
                    "Successfully retrieved location: {LocationCode} - {LocationName}",
                    result.Location?.LocationCode, result.Location?.LocationName);

                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing get location request for: {LocationId}", locationId);
                return Results.Problem(
                    title: "Error getting location",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status500InternalServerError
                );
            }
        })
        .WithTags("Locations")
        .WithName("GetLocationById")
        .Produces<GetLocationByIdResult>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Lấy chi tiết một location theo ID")
        .WithDescription(@"
            Endpoint này trả về thông tin chi tiết của một location (địa điểm khách hàng).

            FLOW:
            1. Query customer_locations table by ID
            2. Trả về full location details

            INPUT:
            - locationId: GUID của location (trong URL path)

            OUTPUT (GetLocationByIdResult):
            - success: true/false
            - errorMessage: Thông báo lỗi (nếu có)
            - location: Object chứa FULL location details:

              ## Thông tin cơ bản:
              - id: Location GUID
              - customerId: Customer GUID
              - locationCode: Mã địa điểm (LOC-001, LOC-002...)
              - locationName: Tên địa điểm (""Chi nhánh Quận 1"")
              - locationType: Loại (office, warehouse, factory, retail_store, residential, hospital, industrial)

              ## Địa chỉ:
              - address: Địa chỉ đầy đủ
              - city: Thành phố
              - district: Quận/Huyện
              - ward: Phường/Xã

              ## Geofencing (cho check-in/out):
              - latitude: Vĩ độ (10.762622)
              - longitude: Kinh độ (106.660172)
              - geofenceRadiusMeters: Bán kính check-in (meters, mặc định 100m)
              - altitudeMeters: Độ cao (cho buildings cao tầng)

              ## Liên hệ tại chỗ:
              - siteManagerName: Tên quản lý địa điểm
              - siteManagerPhone: SĐT quản lý
              - emergencyContactName: Tên liên hệ khẩn cấp
              - emergencyContactPhone: SĐT khẩn cấp

              ## Đặc điểm hoạt động:
              - operatingHoursType: Loại giờ hoạt động (24/7, business_hours, shift_based, seasonal)
              - totalAreaSqm: Diện tích (m²)
              - buildingFloors: Số tầng

              ## Lịch làm việc:
              - followsStandardWorkweek: Theo lịch T2-T6 chuẩn không
              - customWeekendDays: Ngày cuối tuần tùy chỉnh

              ## Yêu cầu bảo vệ:
              - requires24x7Coverage: Yêu cầu 24/7 liên tục không
              - allowsSingleGuard: Cho phép 1 bảo vệ đơn lẻ không
              - minimumGuardsRequired: Số lượng bảo vệ tối thiểu

              ## Trạng thái:
              - isActive: Đang hoạt động không
              - isDeleted: Đã xóa chưa

              ## Metadata:
              - createdAt, updatedAt, createdBy, updatedBy

            VÍ DỤ REQUEST:
            ==============
            ```bash
            GET /api/locations/a1b2c3d4-e5f6-7890-abcd-123456789abc
            ```

            VÍ DỤ RESPONSE THÀNH CÔNG:
            ==========================
            ```json
            {
              ""success"": true,
              ""errorMessage"": null,
              ""location"": {
                ""id"": ""a1b2c3d4-e5f6-7890-abcd-123456789abc"",
                ""customerId"": ""cust-guid"",
                ""locationCode"": ""LOC-001"",
                ""locationName"": ""Chi nhánh Quận 1"",
                ""locationType"": ""office"",
                ""address"": ""123 Nguyễn Huệ, Quận 1, TP.HCM"",
                ""city"": ""TP.HCM"",
                ""district"": ""Quận 1"",
                ""ward"": ""Phường Bến Nghé"",
                ""latitude"": 10.762622,
                ""longitude"": 106.660172,
                ""geofenceRadiusMeters"": 100,
                ""altitudeMeters"": null,
                ""siteManagerName"": ""Nguyễn Văn A"",
                ""siteManagerPhone"": ""0901234567"",
                ""emergencyContactName"": ""Trần Thị B"",
                ""emergencyContactPhone"": ""0909876543"",
                ""operatingHoursType"": ""business_hours"",
                ""totalAreaSqm"": 500.0,
                ""buildingFloors"": 5,
                ""followsStandardWorkweek"": true,
                ""customWeekendDays"": null,
                ""requires24x7Coverage"": false,
                ""allowsSingleGuard"": true,
                ""minimumGuardsRequired"": 2,
                ""isActive"": true,
                ""isDeleted"": false,
                ""createdAt"": ""2025-11-29T10:30:00"",
                ""updatedAt"": null,
                ""createdBy"": null,
                ""updatedBy"": null
              }
            }
            ```

            VÍ DỤ RESPONSE KHI KHÔNG TÌM THẤY:
            ===================================
            ```json
            {
              ""success"": false,
              ""error"": ""Location with ID ... not found"",
              ""location"": null
            }
            ```

            CÁCH SỬ DỤNG:
            =============

            **cURL:**
            ```bash
            curl -X GET 'http://localhost:5002/api/locations/a1b2c3d4-e5f6-7890-abcd-123456789abc'
            ```

            **JavaScript Fetch:**
            ```javascript
            const getLocationById = async (locationId) => {
              const response = await fetch(`/api/locations/${locationId}`);
              const data = await response.json();

              if (data.success) {
                const loc = data.location;
                console.log(`Location: ${loc.locationName} (${loc.locationCode})`);
                console.log(`Type: ${loc.locationType}`);
                console.log(`Address: ${loc.address}`);
                console.log(`Coordinates: ${loc.latitude}, ${loc.longitude}`);
                console.log(`Minimum guards: ${loc.minimumGuardsRequired}`);
              } else {
                console.error(`Error: ${data.error}`);
              }

              return data;
            };
            ```

            **React Example:**
            ```jsx
            const LocationDetail = ({ locationId }) => {
              const [data, setData] = useState(null);
              const [loading, setLoading] = useState(true);

              useEffect(() => {
                const fetchLocation = async () => {
                  const response = await fetch(`/api/locations/${locationId}`);
                  const result = await response.json();

                  if (result.success) {
                    setData(result.location);
                  }
                  setLoading(false);
                };

                fetchLocation();
              }, [locationId]);

              if (loading) return <div>Loading...</div>;
              if (!data) return <div>Location not found</div>;

              return (
                <div className=""location-detail"">
                  <h2>{data.locationName}</h2>
                  <p className=""code"">{data.locationCode}</p>

                  <section>
                    <h3>Basic Info</h3>
                    <p>Type: {data.locationType}</p>
                    <p>Address: {data.address}, {data.district}, {data.city}</p>
                  </section>

                  <section>
                    <h3>Geofencing</h3>
                    <p>Coordinates: {data.latitude}, {data.longitude}</p>
                    <p>Check-in radius: {data.geofenceRadiusMeters}m</p>
                  </section>

                  <section>
                    <h3>Contact</h3>
                    <p>Site Manager: {data.siteManagerName} ({data.siteManagerPhone})</p>
                    <p>Emergency: {data.emergencyContactName} ({data.emergencyContactPhone})</p>
                  </section>

                  <section>
                    <h3>Security Requirements</h3>
                    <p>Minimum guards: {data.minimumGuardsRequired}</p>
                    <p>24/7 Coverage: {data.requires24x7Coverage ? 'Yes' : 'No'}</p>
                    <p>Single guard allowed: {data.allowsSingleGuard ? 'Yes' : 'No'}</p>
                  </section>

                  <section>
                    <h3>Operating Info</h3>
                    <p>Hours type: {data.operatingHoursType}</p>
                    <p>Area: {data.totalAreaSqm}m²</p>
                    <p>Floors: {data.buildingFloors}</p>
                  </section>
                </div>
              );
            };
            ```

            USE CASES:
            ==========
            - Xem chi tiết địa điểm để assign guards
            - Lấy thông tin geofencing cho attendance check-in/out
            - Hiển thị thông tin liên hệ tại chỗ
            - Kiểm tra yêu cầu bảo vệ tối thiểu
            - Lấy tọa độ để hiển thị trên bản đồ

            LƯU Ý:
            =======
            - Trả về cả locations đã xóa (isDeleted = true)
            - Trả về cả locations không active (isActive = false)
            - Để lấy locations của một contract, dùng GET /api/locations/contracts/{contractId}
            - Endpoint này chỉ lấy từ customer_locations (master data)
            - Không bao gồm contract-specific info (như guardsRequired, coverageType...)
        ");
    }
}
