namespace Contracts.API.ContractsHandler.GetContractIdByCustomer;

/// <summary>
/// Endpoint để lấy danh sách contract IDs theo customer ID
/// </summary>
public class GetContractIdByCustomerEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // Route: GET /api/contracts/customers/{customerId}/contracts
        app.MapGet("/api/contracts/customers/{customerId}/contracts", async (
            Guid customerId,
            ISender sender,
            ILogger<GetContractIdByCustomerEndpoint> logger) =>
        {
            try
            {
                logger.LogInformation("Get contract IDs request for customer: {CustomerId}", customerId);

                var query = new GetContractIdByCustomerQuery(customerId);
                var result = await sender.Send(query);

                if (!result.Success)
                {
                    logger.LogWarning("Failed to get contracts: {ErrorMessage}", result.ErrorMessage);
                    return Results.NotFound(new
                    {
                        success = false,
                        error = result.ErrorMessage
                    });
                }

                logger.LogInformation(
                    "Successfully retrieved {Count} contract(s) for customer {CustomerCode}",
                    result.Contracts.Count, result.CustomerCode);

                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing get contracts request for customer: {CustomerId}", customerId);
                return Results.Problem(
                    title: "Error getting contracts",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status500InternalServerError
                );
            }
        })
        .WithTags("Contracts - Customers")
        .WithName("GetContractIdByCustomer")
        .Produces<GetContractIdByCustomerResult>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Lấy danh sách contract IDs theo customer ID")
        .WithDescription(@"
            Endpoint này trả về danh sách tất cả contract IDs của một customer.
            Hữu ích khi cần lấy danh sách contracts để chọn contract cho holiday policy.

            FLOW:
            1. Kiểm tra customer có tồn tại không
            2. Lấy danh sách tất cả contracts của customer (active và inactive)
            3. Trả về contract IDs với thông tin cơ bản

            INPUT:
            - customerId: GUID của customer (trong URL path)

            OUTPUT (GetContractIdByCustomerResult):
            - success: true/false
            - errorMessage: Thông báo lỗi (nếu có)
            - customerId: GUID của customer
            - customerCode: Mã customer (CUST-XXXXXXXX-XXXX)
            - contracts: Mảng các contract với thông tin:
              - id: GUID của contract
              - contractNumber: Số hợp đồng (CTR-XXXXXXXX-XXXX)
              - contractTitle: Tiêu đề hợp đồng
              - status: Trạng thái (draft, pending_approval, active, terminated, expired)
              - startDate: Ngày bắt đầu
              - endDate: Ngày kết thúc

            VÍ DỤ REQUEST:
            ==============
            ```bash
            GET /api/contracts/customers/117bc5b6-abf1-4976-9a27-74368c946dc3/contracts
            ```

            VÍ DỤ RESPONSE THÀNH CÔNG:
            ==========================
            ```json
            {
              ""success"": true,
              ""errorMessage"": null,
              ""customerId"": ""117bc5b6-abf1-4976-9a27-74368c946dc3"",
              ""customerCode"": ""CUST-20251129-9E84"",
              ""contracts"": [
                {
                  ""id"": ""7c05f3c3-57f3-4000-b369-c2f3a1092a6e"",
                  ""contractNumber"": ""CTR-20251129-2520"",
                  ""contractTitle"": ""Hợp đồng bảo vệ - TIỆM MÌ HOÀNH A LỤC"",
                  ""status"": ""active"",
                  ""startDate"": ""2025-11-30T00:00:00"",
                  ""endDate"": ""2026-11-30T00:00:00""
                },
                {
                  ""id"": ""a1b2c3d4-e5f6-4000-b369-c2f3a1092a6e"",
                  ""contractNumber"": ""CTR-20241129-1234"",
                  ""contractTitle"": ""Hợp đồng bảo vệ cũ - TIỆM MÌ HOÀNH A LỤC"",
                  ""status"": ""expired"",
                  ""startDate"": ""2024-01-01T00:00:00"",
                  ""endDate"": ""2024-12-31T00:00:00""
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
            curl -X GET 'http://localhost:5000/api/contracts/customers/117bc5b6-abf1-4976-9a27-74368c946dc3/contracts'
            ```

            **JavaScript Fetch:**
            ```javascript
            const getContractsByCustomer = async (customerId) => {
              const response = await fetch(`/api/contracts/customers/${customerId}/contracts`);
              const result = await response.json();

              if (result.success) {
                console.log(`Found ${result.contracts.length} contract(s) for ${result.customerCode}`);
                result.contracts.forEach(contract => {
                  console.log(`- ${contract.contractNumber}: ${contract.contractTitle} (${contract.status})`);
                });
              } else {
                console.error(`Error: ${result.error}`);
              }

              return result;
            };

            // Sử dụng
            getContractsByCustomer('117bc5b6-abf1-4976-9a27-74368c946dc3');
            ```

            **React Example:**
            ```jsx
            const ContractSelector = ({ customerId }) => {
              const [contracts, setContracts] = useState([]);
              const [loading, setLoading] = useState(true);

              useEffect(() => {
                const fetchContracts = async () => {
                  const response = await fetch(`/api/contracts/customers/${customerId}/contracts`);
                  const data = await response.json();

                  if (data.success) {
                    setContracts(data.contracts);
                  }
                  setLoading(false);
                };

                fetchContracts();
              }, [customerId]);

              if (loading) return <div>Loading...</div>;

              return (
                <select>
                  <option value="""">Select a contract</option>
                  {contracts.map(contract => (
                    <option key={contract.id} value={contract.id}>
                      {contract.contractNumber} - {contract.contractTitle}
                    </option>
                  ))}
                </select>
              );
            };
            ```

            LƯU Ý:
            =======
            - Endpoint này trả về TẤT CẢ contracts (bao gồm cả active, expired, terminated...)
            - Contracts được sắp xếp theo StartDate giảm dần (mới nhất trước)
            - Chỉ trả về contracts chưa bị xóa (IsDeleted = 0)
            - Sử dụng endpoint này khi cần lấy contractId để tạo holiday policy riêng cho contract
        ");
    }
}
