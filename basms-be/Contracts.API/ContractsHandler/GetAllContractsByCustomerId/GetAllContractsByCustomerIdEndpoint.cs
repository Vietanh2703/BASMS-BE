namespace Contracts.API.ContractsHandler.GetAllContractsByCustomerId;

/// <summary>
/// Endpoint để lấy tất cả contracts với full details theo customer ID
/// </summary>
public class GetAllContractsByCustomerIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/contracts/customers/{customerId}/all", async (
            Guid customerId,
            ISender sender,
            ILogger<GetAllContractsByCustomerIdEndpoint> logger) =>
        {
            try
            {
                logger.LogInformation("Get all contracts (full details) request for customer: {CustomerId}", customerId);

                var query = new GetAllContractsByCustomerIdQuery(customerId);
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
                    "Successfully retrieved {Count} contract(s) with full details for customer {CustomerCode} ({CustomerName})",
                    result.TotalContracts, result.CustomerCode, result.CustomerName);

                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing get all contracts request for customer: {CustomerId}", customerId);
                return Results.Problem(
                    title: "Error getting contracts",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status500InternalServerError
                );
            }
        })
        .RequireAuthorization()
        .WithTags("Contracts - Customers")
        .WithName("GetAllContractsByCustomerId")
        .Produces<GetAllContractsByCustomerIdResult>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Lấy tất cả contracts (full details) theo customer ID")
        .WithDescription(@"
            Endpoint này trả về TẤT CẢ thông tin chi tiết của tất cả contracts của một customer.
            Khác với endpoint /api/contracts/customers/{customerId}/contracts (chỉ trả về IDs),
            endpoint này trả về FULL CONTRACT DETAILS.

            FLOW:
            1. Kiểm tra customer có tồn tại không
            2. Lấy thông tin customer (CustomerCode, CustomerName)
            3. Lấy tất cả contracts của customer với đầy đủ thông tin
            4. Trả về full contract details

            INPUT:
            - customerId: GUID của customer (trong URL path)

            OUTPUT (GetAllContractsByCustomerIdResult):
            - success: true/false
            - errorMessage: Thông báo lỗi (nếu có)
            - customerId: GUID của customer
            - customerCode: Mã customer (CUST-001, CUST-002...)
            - customerName: Tên customer (CompanyName hoặc ContactPersonName)
            - totalContracts: Tổng số contracts
            - contracts: Mảng các contract với FULL DETAILS bao gồm:

              ## Thông tin cơ bản:
              - id: GUID của contract
              - customerId: GUID của customer
              - documentId: GUID của document template
              - contractNumber: Số hợp đồng (CTR-20251129-2520)
              - contractTitle: Tiêu đề hợp đồng
              - contractType: Loại (long_term, short_term, trial, event_based...)
              - serviceScope: Phạm vi (continuous_24x7, shift_based, event_only...)

              ## Thời hạn:
              - startDate: Ngày bắt đầu
              - endDate: Ngày kết thúc
              - durationMonths: Thời hạn (tháng)

              ## Gia hạn:
              - isRenewable: Có thể gia hạn không
              - autoRenewal: Tự động gia hạn không
              - renewalNoticeDays: Số ngày thông báo trước
              - renewalCount: Đã gia hạn bao nhiêu lần

              ## Mô hình:
              - coverageModel: Mô hình coverage (fixed_schedule, rotating_schedule...)

              ## Lịch làm việc:
              - followsCustomerCalendar: Theo lịch khách hàng không
              - workOnPublicHolidays: Làm việc ngày lễ không
              - workOnCustomerClosedDays: Làm khi khách đóng cửa không

              ## Tự động tạo ca:
              - autoGenerateShifts: Tự động tạo shifts không
              - generateShiftsAdvanceDays: Tạo trước bao nhiêu ngày

              ## Trạng thái:
              - status: draft, pending_approval, active, suspended, expired, terminated, completed
              - approvedBy: Người phê duyệt
              - approvedAt: Thời điểm phê duyệt
              - activatedAt: Thời điểm kích hoạt

              ## Chấm dứt:
              - terminationDate: Ngày chấm dứt
              - terminationType: Loại chấm dứt
              - terminationReason: Lý do chấm dứt
              - terminatedBy: Người chấm dứt

              ## Tài liệu:
              - contractFileUrl: URL file PDF
              - signedDate: Ngày ký
              - notes: Ghi chú

              ## Working contract (nếu có):
              - monthlyWage: Tiền lương tháng
              - monthlyWageInWords: Tiền lương bằng chữ
              - certificationLevel: Hạng chứng chỉ
              - jobTitle: Chức danh

              ## Metadata:
              - createdAt, updatedAt, createdBy, updatedBy

            VÍ DỤ REQUEST:
            ==============
            ```bash
            GET /api/contracts/customers/117bc5b6-abf1-4976-9a27-74368c946dc3/all
            ```

            VÍ DỤ RESPONSE THÀNH CÔNG:
            ==========================
            ```json
            {
              ""success"": true,
              ""errorMessage"": null,
              ""customerId"": ""117bc5b6-abf1-4976-9a27-74368c946dc3"",
              ""customerCode"": ""CUST-001"",
              ""customerName"": ""TIỆM MÌ HOÀNH A LỤC"",
              ""totalContracts"": 2,
              ""contracts"": [
                {
                  ""id"": ""7c05f3c3-57f3-4000-b369-c2f3a1092a6e"",
                  ""customerId"": ""117bc5b6-abf1-4976-9a27-74368c946dc3"",
                  ""documentId"": ""123e4567-e89b-12d3-a456-426614174000"",
                  ""contractNumber"": ""CTR-20251129-2520"",
                  ""contractTitle"": ""Hợp đồng bảo vệ - TIỆM MÌ HOÀNH A LỤC"",
                  ""contractType"": ""long_term"",
                  ""serviceScope"": ""shift_based"",
                  ""startDate"": ""2025-11-30T00:00:00"",
                  ""endDate"": ""2026-11-30T00:00:00"",
                  ""durationMonths"": 12,
                  ""isRenewable"": true,
                  ""autoRenewal"": false,
                  ""renewalNoticeDays"": 30,
                  ""renewalCount"": 0,
                  ""coverageModel"": ""fixed_schedule"",
                  ""followsCustomerCalendar"": true,
                  ""workOnPublicHolidays"": true,
                  ""workOnCustomerClosedDays"": true,
                  ""autoGenerateShifts"": true,
                  ""generateShiftsAdvanceDays"": 30,
                  ""status"": ""active"",
                  ""approvedBy"": null,
                  ""approvedAt"": null,
                  ""activatedAt"": ""2025-11-30T00:00:00"",
                  ""terminationDate"": null,
                  ""terminationType"": null,
                  ""terminationReason"": null,
                  ""terminatedBy"": null,
                  ""contractFileUrl"": ""https://s3.amazonaws.com/....."",
                  ""signedDate"": ""2025-11-29T00:00:00"",
                  ""notes"": ""Hợp đồng 1 năm, tự động gia hạn"",
                  ""monthlyWage"": null,
                  ""monthlyWageInWords"": null,
                  ""certificationLevel"": null,
                  ""jobTitle"": null,
                  ""createdAt"": ""2025-11-29T10:30:00"",
                  ""updatedAt"": null,
                  ""createdBy"": null,
                  ""updatedBy"": null
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
            curl -X GET 'http://localhost:5002/api/contracts/customers/117bc5b6-abf1-4976-9a27-74368c946dc3/all'
            ```

            **JavaScript Fetch:**
            ```javascript
            const getAllContractsByCustomer = async (customerId) => {
              const response = await fetch(`/api/contracts/customers/${customerId}/all`);
              const result = await response.json();

              if (result.success) {
                console.log(`Found ${result.totalContracts} contract(s) for ${result.customerName}`);
                result.contracts.forEach(contract => {
                  console.log(`Contract: ${contract.contractNumber}`);
                  console.log(`  Title: ${contract.contractTitle}`);
                  console.log(`  Type: ${contract.contractType}`);
                  console.log(`  Status: ${contract.status}`);
                  console.log(`  Period: ${contract.startDate} - ${contract.endDate}`);
                  console.log(`  Duration: ${contract.durationMonths} months`);
                  console.log('---');
                });
              } else {
                console.error(`Error: ${result.error}`);
              }

              return result;
            };

            // Sử dụng
            getAllContractsByCustomer('117bc5b6-abf1-4976-9a27-74368c946dc3');
            ```

            **React Example:**
            ```jsx
            const CustomerContractsList = ({ customerId }) => {
              const [data, setData] = useState(null);
              const [loading, setLoading] = useState(true);

              useEffect(() => {
                const fetchContracts = async () => {
                  const response = await fetch(`/api/contracts/customers/${customerId}/all`);
                  const result = await response.json();

                  if (result.success) {
                    setData(result);
                  }
                  setLoading(false);
                };

                fetchContracts();
              }, [customerId]);

              if (loading) return <div>Loading...</div>;
              if (!data) return <div>No data</div>;

              return (
                <div>
                  <h2>Contracts for {data.customerName} ({data.customerCode})</h2>
                  <p>Total: {data.totalContracts} contract(s)</p>

                  {data.contracts.map(contract => (
                    <div key={contract.id} className=""contract-card"">
                      <h3>{contract.contractNumber} - {contract.contractTitle}</h3>
                      <p>Type: {contract.contractType}</p>
                      <p>Status: <span className={`status-${contract.status}`}>{contract.status}</span></p>
                      <p>Period: {new Date(contract.startDate).toLocaleDateString()} - {new Date(contract.endDate).toLocaleDateString()}</p>
                      <p>Duration: {contract.durationMonths} months</p>
                      {contract.autoGenerateShifts && (
                        <p>Auto-generate shifts: {contract.generateShiftsAdvanceDays} days in advance</p>
                      )}
                      {contract.contractFileUrl && (
                        <a href={contract.contractFileUrl} target=""_blank"">View Contract PDF</a>
                      )}
                    </div>
                  ))}
                </div>
              );
            };
            ```

            SO SÁNH VỚI ENDPOINT KHÁC:
            ==========================

            | Endpoint | Route | Dữ liệu trả về | Use case |
            |----------|-------|----------------|----------|
            | GetContractIdByCustomer | /api/contracts/customers/{id}/contracts | Chỉ IDs + thông tin cơ bản | Dropdown selector, quick list |
            | GetAllContractsByCustomerId | /api/contracts/customers/{id}/all | FULL CONTRACT DETAILS | Chi tiết đầy đủ, báo cáo, dashboard |

            LƯU Ý:
            =======
            - Endpoint này trả về TẤT CẢ contracts (active, expired, terminated...)
            - Contracts được sắp xếp theo StartDate giảm dần (mới nhất trước)
            - Chỉ trả về contracts chưa bị xóa (IsDeleted = 0)
            - Trả về FULL DETAILS - có thể response size lớn nếu customer có nhiều contracts
            - Nếu chỉ cần IDs để tạo dropdown, dùng endpoint /api/contracts/customers/{id}/contracts
        ");
    }
}
