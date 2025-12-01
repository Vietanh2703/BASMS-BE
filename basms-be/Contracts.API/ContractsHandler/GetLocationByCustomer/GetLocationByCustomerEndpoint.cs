namespace Contracts.API.ContractsHandler.GetLocationByCustomer;

/// <summary>
/// Endpoint để lấy danh sách locations theo customer ID
/// </summary>
public class GetLocationByCustomerEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // Route: GET /api/contracts/customers/{customerId}/locations
        app.MapGet("/api/contracts/customers/{customerId}/locations", async (
            Guid customerId,
            ISender sender,
            ILogger<GetLocationByCustomerEndpoint> logger) =>
        {
            try
            {
                logger.LogInformation("Get locations request for customer: {CustomerId}", customerId);

                var query = new GetLocationByCustomerQuery(customerId);
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
                    "Successfully retrieved {Count} location(s) for customer {CustomerCode}",
                    result.Locations.Count, result.CustomerCode);

                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing get locations request for customer: {CustomerId}", customerId);
                return Results.Problem(
                    title: "Error getting locations",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status500InternalServerError
                );
            }
        })
        .WithTags("Contracts - Customers")
        .WithName("GetLocationByCustomer")
        .Produces<GetLocationByCustomerResult>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Lấy danh sách locations theo customer ID")
        .WithDescription(@"
            Endpoint này trả về danh sách tất cả customer locations của một customer.
            Hữu ích khi cần chọn location để update GPS coordinates hoặc tạo contract location.

            FLOW:
            1. Kiểm tra customer có tồn tại không
            2. Lấy danh sách tất cả customer locations
            3. Trả về location IDs với thông tin cơ bản

            INPUT:
            - customerId: GUID của customer (trong URL path)

            OUTPUT (GetLocationByCustomerResult):
            - success: true/false
            - errorMessage: Thông báo lỗi (nếu có)
            - customerId: GUID của customer
            - customerCode: Mã customer (CUST-XXXXXXXX-XXXX)
            - locations: Mảng các location với thông tin:
              - id: GUID của location
              - locationCode: Mã location (LOC-XXXXXXXX-XXX)
              - locationName: Tên địa điểm
              - address: Địa chỉ đầy đủ
              - city: Thành phố
              - district: Quận/Huyện
              - ward: Phường/Xã
              - locationType: Loại địa điểm (office, warehouse, factory, retail_store...)
              - isActive: Còn hoạt động không

            VÍ DỤ REQUEST:
            ==============
            ```bash
            GET /api/contracts/customers/117bc5b6-abf1-4976-9a27-74368c946dc3/locations
            ```

            VÍ DỤ RESPONSE THÀNH CÔNG:
            ==========================
            ```json
            {
              ""success"": true,
              ""errorMessage"": null,
              ""customerId"": ""117bc5b6-abf1-4976-9a27-74368c946dc3"",
              ""customerCode"": ""CUST-20251129-9E84"",
              ""locations"": [
                {
                  ""id"": ""e294fe79-6fac-4f05-9f04-1b046d7d4dd9"",
                  ""locationCode"": ""LOC-20251129-001"",
                  ""locationName"": ""TIỆM MÌ HOÀNH A LỤC"",
                  ""address"": ""138 Nguyễn Thị Búp, Tân Chánh Hiệp, Quận 12, Thành phố Hồ Chí Minh"",
                  ""city"": ""Hồ Chí Minh"",
                  ""district"": ""Quận 12"",
                  ""ward"": ""Tân Chánh Hiệp"",
                  ""locationType"": ""office"",
                  ""isActive"": true
                },
                {
                  ""id"": ""f1e2d3c4-b5a6-4f05-9f04-2c157e8e5ee8"",
                  ""locationCode"": ""LOC-20251129-002"",
                  ""locationName"": ""Chi nhánh Quận 1"",
                  ""address"": ""123 Nguyễn Huệ, Phường Bến Nghé, Quận 1, Thành phố Hồ Chí Minh"",
                  ""city"": ""Hồ Chí Minh"",
                  ""district"": ""Quận 1"",
                  ""ward"": ""Bến Nghé"",
                  ""locationType"": ""retail_store"",
                  ""isActive"": true
                }
              ]
            }
            ```

            VÍ DỤ RESPONSE KHI KHÔNG TÌM THẤY:
            ===================================
            ```json
            {
              ""success"": false,
              ""error"": ""Customer with ID 117bc5b6-abf1-4976-9a27-74368c946dc3 not found""
            }
            ```

            CÁCH SỬ DỤNG:
            =============

            **cURL:**
            ```bash
            curl -X GET 'http://localhost:5000/api/contracts/customers/117bc5b6-abf1-4976-9a27-74368c946dc3/locations'
            ```

            **JavaScript Fetch:**
            ```javascript
            const getLocationsByCustomer = async (customerId) => {
              const response = await fetch(`/api/contracts/customers/${customerId}/locations`);
              const result = await response.json();

              if (result.success) {
                console.log(`Found ${result.locations.length} location(s) for ${result.customerCode}`);
                result.locations.forEach(location => {
                  console.log(`- ${location.locationCode}: ${location.locationName}`);
                  console.log(`  Address: ${location.address}`);
                });
              } else {
                console.error(`Error: ${result.error}`);
              }

              return result;
            };

            // Sử dụng
            getLocationsByCustomer('117bc5b6-abf1-4976-9a27-74368c946dc3');
            ```

            **React Example:**
            ```jsx
            const LocationSelector = ({ customerId }) => {
              const [locations, setLocations] = useState([]);
              const [loading, setLoading] = useState(true);

              useEffect(() => {
                const fetchLocations = async () => {
                  const response = await fetch(`/api/contracts/customers/${customerId}/locations`);
                  const data = await response.json();

                  if (data.success) {
                    setLocations(data.locations);
                  }
                  setLoading(false);
                };

                fetchLocations();
              }, [customerId]);

              if (loading) return <div>Loading locations...</div>;

              return (
                <div>
                  <h3>Select a Location</h3>
                  <select>
                    <option value="""">Choose location...</option>
                    {locations.map(location => (
                      <option key={location.id} value={location.id}>
                        {location.locationName} - {location.address}
                      </option>
                    ))}
                  </select>

                  <div className=""locations-list"">
                    {locations.map(location => (
                      <div key={location.id} className=""location-card"">
                        <h4>{location.locationName}</h4>
                        <p><strong>Code:</strong> {location.locationCode}</p>
                        <p><strong>Type:</strong> {location.locationType}</p>
                        <p><strong>Address:</strong> {location.address}</p>
                        {location.city && <p><strong>City:</strong> {location.city}</p>}
                        {location.district && <p><strong>District:</strong> {location.district}</p>}
                        <p><strong>Status:</strong> {location.isActive ? 'Active' : 'Inactive'}</p>
                      </div>
                    ))}
                  </div>
                </div>
              );
            };
            ```

            **With Google Maps Integration:**
            ```javascript
            const LocationsMap = ({ customerId }) => {
              const [locations, setLocations] = useState([]);

              useEffect(() => {
                const fetchLocations = async () => {
                  const response = await fetch(`/api/contracts/customers/${customerId}/locations`);
                  const data = await response.json();

                  if (data.success) {
                    setLocations(data.locations);
                    // Plot locations on Google Maps
                    data.locations.forEach(location => {
                      console.log(`Location: ${location.locationName} at ${location.address}`);
                      // Add marker to map using latitude/longitude if available
                    });
                  }
                };

                fetchLocations();
              }, [customerId]);

              return (
                <div>
                  <h3>Customer Locations</h3>
                  <div id=""map""></div>
                  <ul>
                    {locations.map(location => (
                      <li key={location.id}>
                        <strong>{location.locationName}</strong>
                        <br />
                        {location.address}
                      </li>
                    ))}
                  </ul>
                </div>
              );
            };
            ```

            LƯU Ý:
            =======
            - Endpoint này trả về TẤT CẢ locations (active và inactive)
            - Locations được sắp xếp theo LocationCode
            - Chỉ trả về locations chưa bị xóa (IsDeleted = 0)
            - Sử dụng endpoint này khi cần:
              - Chọn location để update GPS coordinates
              - Hiển thị danh sách địa điểm của customer
              - Tạo contract location
              - Quản lý địa điểm khách hàng

            USE CASES:
            ==========
            1. Update GPS coordinates cho location
            2. Chọn location khi tạo contract
            3. Hiển thị danh sách chi nhánh/cơ sở của khách hàng
            4. Quản lý địa điểm triển khai dịch vụ bảo vệ
        ");
    }
}
