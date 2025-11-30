namespace Contracts.API.ContractsHandler.GetAllCustomers;

/// <summary>
/// Endpoint để lấy tất cả customers
/// </summary>
public class GetAllCustomersEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // Route: GET /api/contracts/customers
        app.MapGet("/api/contracts/customers", async (
            ISender sender,
            ILogger<GetAllCustomersEndpoint> logger) =>
        {
            try
            {
                logger.LogInformation("Get all customers request");

                var query = new GetAllCustomersQuery();
                var result = await sender.Send(query);

                if (!result.Success)
                {
                    logger.LogError("Failed to get customers: {ErrorMessage}", result.ErrorMessage);
                    return Results.Problem(
                        title: "Error getting customers",
                        detail: result.ErrorMessage,
                        statusCode: StatusCodes.Status500InternalServerError
                    );
                }

                logger.LogInformation(
                    "Successfully retrieved {Count} customers",
                    result.TotalCount);

                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing get all customers request");
                return Results.Problem(
                    title: "Error getting customers",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status500InternalServerError
                );
            }
        })
        .WithTags("Contracts - Customers")
        .WithName("GetAllCustomers")
        .Produces<GetAllCustomersResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Lấy tất cả customers")
        .WithDescription(@"
            Endpoint này trả về danh sách tất cả customers (khách hàng) trong hệ thống.

            FLOW:
            1. Query tất cả customers từ bảng customers (IsDeleted = 0)
            2. Map sang DTO với đầy đủ thông tin khách hàng
            3. Trả về danh sách đầy đủ

            OUTPUT:
            - success: true/false
            - customers: Array of CustomerDto
              * id: GUID của customer
              * customerCode: Mã khách hàng (CUST-001, CUST-002...)
              * companyName: Tên công ty khách hàng
              * contactPersonName: Tên người liên hệ
              * contactPersonTitle: Chức danh người liên hệ
              * email: Email liên hệ
              * phone: Số điện thoại
              * avatarUrl: URL avatar
              * gender: Giới tính
              * dateOfBirth: Ngày sinh
              * address: Địa chỉ
              * city: Thành phố
              * district: Quận/Huyện
              * industry: Ngành nghề (retail, office, manufacturing, hospital, school, residential)
              * companySize: Quy mô (small, medium, large, enterprise)
              * status: Trạng thái (active, inactive, suspended)
              * customerSince: Ngày bắt đầu là khách hàng
              * followsNationalHolidays: Có theo ngày lễ Việt Nam không
              * createdAt: Ngày tạo
            - totalCount: Tổng số customers

            VÍ DỤ RESPONSE:
            ===============
            ```json
            {
              ""success"": true,
              ""customers"": [
                {
                  ""id"": ""guid-xxx-xxx"",
                  ""customerCode"": ""CUST-001"",
                  ""companyName"": ""Bệnh viện ABC"",
                  ""contactPersonName"": ""Nguyễn Văn A"",
                  ""contactPersonTitle"": ""Giám đốc hành chính"",
                  ""email"": ""admin@benhvien-abc.com"",
                  ""phone"": ""0901234567"",
                  ""avatarUrl"": ""https://example.com/avatar.jpg"",
                  ""gender"": ""male"",
                  ""dateOfBirth"": ""1980-01-15T00:00:00Z"",
                  ""address"": ""123 Đường ABC"",
                  ""city"": ""Hồ Chí Minh"",
                  ""district"": ""Quận 1"",
                  ""industry"": ""hospital"",
                  ""companySize"": ""large"",
                  ""status"": ""active"",
                  ""customerSince"": ""2025-01-01T00:00:00Z"",
                  ""followsNationalHolidays"": true,
                  ""createdAt"": ""2025-01-01T08:00:00Z""
                },
                {
                  ""id"": ""guid-yyy-yyy"",
                  ""customerCode"": ""CUST-002"",
                  ""companyName"": ""Siêu thị XYZ"",
                  ""contactPersonName"": ""Trần Thị B"",
                  ""contactPersonTitle"": ""Trưởng phòng hành chính"",
                  ""email"": ""admin@sieuthi-xyz.com"",
                  ""phone"": ""0907654321"",
                  ""avatarUrl"": ""https://example.com/avatar2.jpg"",
                  ""gender"": ""female"",
                  ""dateOfBirth"": ""1985-05-20T00:00:00Z"",
                  ""address"": ""456 Đường XYZ"",
                  ""city"": ""Hà Nội"",
                  ""district"": ""Hoàn Kiếm"",
                  ""industry"": ""retail"",
                  ""companySize"": ""medium"",
                  ""status"": ""active"",
                  ""customerSince"": ""2025-01-05T00:00:00Z"",
                  ""followsNationalHolidays"": true,
                  ""createdAt"": ""2025-01-05T09:30:00Z""
                }
              ],
              ""totalCount"": 2
            }
            ```

            CÁCH SỬ DỤNG:
            =============

            **cURL:**
            ```bash
            curl -X GET 'http://localhost:5000/api/contracts/customers'
            ```

            **JavaScript Fetch:**
            ```javascript
            fetch('/api/contracts/customers')
              .then(response => response.json())
              .then(data => {
                console.log(`Found ${data.totalCount} customers`);
                data.customers.forEach(customer => {
                  console.log(`${customer.companyName} - ${customer.customerCode}`);
                  console.log(`Contact: ${customer.contactPersonName} (${customer.email})`);
                });
              });
            ```

            **Postman:**
            1. GET request to: /api/contracts/customers
            2. Click Send
            3. View JSON response with all customers

            **React Example:**
            ```jsx
            const CustomersList = () => {
              const [customers, setCustomers] = useState([]);

              useEffect(() => {
                fetch('/api/contracts/customers')
                  .then(res => res.json())
                  .then(data => setCustomers(data.customers));
              }, []);

              return (
                <ul>
                  {customers.map(customer => (
                    <li key={customer.id}>
                      <h3>{customer.companyName}</h3>
                      <p>Code: {customer.customerCode}</p>
                      <p>Contact: {customer.contactPersonName}</p>
                      <p>Email: {customer.email}</p>
                      <p>Status: {customer.status}</p>
                    </li>
                  ))}
                </ul>
              );
            };
            ```

            LƯU Ý:
            =======
            - Chỉ trả về customers chưa bị xóa (IsDeleted = 0)
            - Customers được sắp xếp theo ngày tạo (mới nhất trước)
            - Industry values: retail, office, manufacturing, hospital, school, residential
            - CompanySize values: small, medium, large, enterprise
            - Status values: active, inactive, suspended
            - FollowsNationalHolidays = true: khách hàng nghỉ theo lịch lễ quốc gia
        ");
    }
}
