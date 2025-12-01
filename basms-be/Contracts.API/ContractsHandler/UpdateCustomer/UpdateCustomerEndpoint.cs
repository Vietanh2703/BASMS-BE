namespace Contracts.API.ContractsHandler.UpdateCustomer;

/// <summary>
/// Endpoint để update thông tin customer
/// </summary>
public class UpdateCustomerEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // Route: PUT /api/contracts/customers/{customerId}
        app.MapPut("/api/contracts/customers/{customerId}", async (
            Guid customerId,
            UpdateCustomerRequest request,
            ISender sender,
            ILogger<UpdateCustomerEndpoint> logger) =>
        {
            try
            {
                logger.LogInformation("Update customer request for ID: {CustomerId}", customerId);

                // Map request to command
                var command = new UpdateCustomerCommand
                {
                    CustomerId = customerId,
                    CompanyName = request.CompanyName,
                    ContactPersonName = request.ContactPersonName,
                    ContactPersonTitle = request.ContactPersonTitle,
                    IdentityNumber = request.IdentityNumber,
                    IdentityIssueDate = request.IdentityIssueDate,
                    IdentityIssuePlace = request.IdentityIssuePlace,
                    DateOfBirth = request.DateOfBirth,
                    Address = request.Address
                };

                var result = await sender.Send(command);

                if (!result.Success)
                {
                    logger.LogError("Failed to update customer: {ErrorMessage}", result.ErrorMessage);
                    return Results.Problem(
                        title: "Error updating customer",
                        detail: result.ErrorMessage,
                        statusCode: StatusCodes.Status400BadRequest
                    );
                }

                logger.LogInformation(
                    "Successfully updated customer {CustomerCode} (ID: {CustomerId})",
                    result.CustomerCode, result.CustomerId);

                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing update customer request for ID: {CustomerId}", customerId);
                return Results.Problem(
                    title: "Error updating customer",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status500InternalServerError
                );
            }
        })
        .WithTags("Contracts - Customers")
        .WithName("UpdateCustomer")
        .Produces<UpdateCustomerResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Update thông tin customer")
        .WithDescription(@"
            Endpoint này cập nhật thông tin customer trong hệ thống.

            FLOW:
            1. Validate dữ liệu đầu vào (identity number, date of birth...)
            2. Kiểm tra customer có tồn tại không
            3. Kiểm tra identity number có bị trùng với customer khác không
            4. Update thông tin customer vào database
            5. Trả về kết quả

            INPUT (UpdateCustomerRequest):
            - companyName: Tên công ty (required, max 200 chars)
            - contactPersonName: Tên người liên hệ (required, max 100 chars)
            - contactPersonTitle: Chức danh người liên hệ (optional, max 100 chars)
            - identityNumber: Số CCCD (required, 9 hoặc 12 số)
            - identityIssueDate: Ngày cấp CCCD (optional, không được trong tương lai)
            - identityIssuePlace: Nơi cấp CCCD (optional, max 200 chars)
            - dateOfBirth: Ngày sinh (required, phải trong quá khứ)
            - address: Địa chỉ (required, max 500 chars)

            OUTPUT (UpdateCustomerResult):
            - success: true/false
            - errorMessage: Thông báo lỗi (nếu có)
            - customerId: GUID của customer đã update
            - customerCode: Mã customer (CUST-XXXXXXXX-XXXX)

            VÍ DỤ REQUEST:
            ==============
            ```json
            PUT /api/contracts/customers/117bc5b6-abf1-4976-9a27-74368c946dc3
            {
              ""companyName"": ""TIỆM MÌ HOÀNH A LỤC"",
              ""contactPersonName"": ""Phan Danh Minh"",
              ""contactPersonTitle"": ""Giám đốc điều hành"",
              ""identityNumber"": ""082938465729"",
              ""identityIssueDate"": ""2015-01-15T00:00:00"",
              ""identityIssuePlace"": ""Cục Cảnh sát ĐKQL cư trú và DLQG về dân cư"",
              ""dateOfBirth"": ""1990-01-15T00:00:00"",
              ""address"": ""138 Nguyễn Thị Búp, Tân Chánh Hiệp, Quận 12, Thành phố Hồ Chí Minh""
            }
            ```

            VÍ DỤ RESPONSE THÀNH CÔNG:
            ==========================
            ```json
            {
              ""success"": true,
              ""errorMessage"": null,
              ""customerId"": ""117bc5b6-abf1-4976-9a27-74368c946dc3"",
              ""customerCode"": ""CUST-20251129-9E84""
            }
            ```

            VÍ DỤ RESPONSE LỖI:
            ===================
            ```json
            {
              ""success"": false,
              ""errorMessage"": ""Email minhpdse150908@fpt.edu.vn is already in use by another customer"",
              ""customerId"": null,
              ""customerCode"": null
            }
            ```

            VALIDATION RULES:
            =================
            1. CompanyName: Bắt buộc, tối đa 200 ký tự
            2. ContactPersonName: Bắt buộc, tối đa 100 ký tự
            3. ContactPersonTitle: Optional, tối đa 100 ký tự
            4. IdentityNumber: Bắt buộc, phải là 9 hoặc 12 số, không được trùng với customer khác
            5. IdentityIssueDate: Optional, không được trong tương lai
            6. IdentityIssuePlace: Optional, tối đa 200 ký tự
            7. DateOfBirth: Bắt buộc, phải trong quá khứ, không quá 150 năm trước
            8. Address: Bắt buộc, tối đa 500 ký tự

            CÁCH SỬ DỤNG:
            =============

            **cURL:**
            ```bash
            curl -X PUT 'http://localhost:5000/api/contracts/customers/117bc5b6-abf1-4976-9a27-74368c946dc3' \
              -H 'Content-Type: application/json' \
              -d '{
                ""companyName"": ""TIỆM MÌ HOÀNH A LỤC"",
                ""contactPersonName"": ""Phan Danh Minh"",
                ""contactPersonTitle"": ""Giám đốc điều hành"",
                ""identityNumber"": ""082938465729"",
                ""email"": ""minhpdse150908@fpt.edu.vn"",
                ""phone"": ""+84329465423"",
                ""gender"": ""male"",
                ""dateOfBirth"": ""1990-01-15T00:00:00"",
                ""address"": ""138 Nguyễn Thị Búp, Tân Chánh Hiệp, Quận 12"",
              }'
            ```

            **JavaScript Fetch:**
            ```javascript
            const updateCustomer = async (customerId, customerData) => {
              const response = await fetch(`/api/contracts/customers/${customerId}`, {
                method: 'PUT',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(customerData)
              });

              const result = await response.json();

              if (result.success) {
                console.log(`Updated customer: ${result.customerCode}`);
              } else {
                console.error(`Error: ${result.errorMessage}`);
              }

              return result;
            };

            // Sử dụng
            updateCustomer('117bc5b6-abf1-4976-9a27-74368c946dc3', {
              companyName: 'TIỆM MÌ HOÀNH A LỤC',
              contactPersonName: 'Phan Danh Minh',
              contactPersonTitle: 'Giám đốc điều hành',
              identityNumber: '082938465729',
              email: 'minhpdse150908@fpt.edu.vn',
              phone: '+84329465423',
              gender: 'male',
              dateOfBirth: '1990-01-15T00:00:00',
              address: '138 Nguyễn Thị Búp, Tân Chánh Hiệp, Quận 12',
              status: 'active',
              followsNationalHolidays: true,
              latitude: 10.762622,
              longitude: 106.660172
            });
            ```

            **React Example:**
            ```jsx
            const UpdateCustomerForm = ({ customerId }) => {
              const [formData, setFormData] = useState({});
              const [result, setResult] = useState(null);

              const handleSubmit = async (e) => {
                e.preventDefault();

                const response = await fetch(`/api/contracts/customers/${customerId}`, {
                  method: 'PUT',
                  headers: { 'Content-Type': 'application/json' },
                  body: JSON.stringify(formData)
                });

                const data = await response.json();
                setResult(data);
              };

              return (
                <form onSubmit={handleSubmit}>
                  {/* Form fields */}
                  <input
                    name=""companyName""
                    onChange={(e) => setFormData({...formData, companyName: e.target.value})}
                    required
                  />
                  {/* More fields... */}
                  <button type=""submit"">Update Customer</button>

                  {result && (
                    <div>
                      {result.success ? (
                        <p>Updated: {result.customerCode}</p>
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
            - Update thông tin cơ bản của customer và GPS coordinates của location
            - Không update CustomerCode (auto-generated)
            - Không update CustomerSince (giá trị ban đầu)
            - Email và IdentityNumber phải unique (không trùng với customer khác)
            - Latitude/Longitude sẽ được update vào location đầu tiên của customer
            - Nếu chỉ cung cấp latitude hoặc longitude, field còn lại giữ nguyên
            - UpdatedAt sẽ được tự động cập nhật cho cả customer và location
            - Validation được thực hiện bởi FluentValidation
        ");
    }
}

/// <summary>
/// Request model cho UpdateCustomer endpoint
/// </summary>
public record UpdateCustomerRequest
{
    public string CompanyName { get; init; } = string.Empty;
    public string ContactPersonName { get; init; } = string.Empty;
    public string? ContactPersonTitle { get; init; }
    public string IdentityNumber { get; init; } = string.Empty;
    public DateTime? IdentityIssueDate { get; init; }
    public string? IdentityIssuePlace { get; init; }
    public DateTime DateOfBirth { get; init; }
    public string Address { get; init; } = string.Empty;
}
