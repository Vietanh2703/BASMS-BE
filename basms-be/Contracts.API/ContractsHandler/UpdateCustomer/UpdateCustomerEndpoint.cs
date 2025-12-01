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
                    Email = request.Email,
                    Phone = request.Phone,
                    AvatarUrl = request.AvatarUrl,
                    Gender = request.Gender,
                    DateOfBirth = request.DateOfBirth,
                    Address = request.Address,
                    City = request.City,
                    District = request.District,
                    Industry = request.Industry,
                    CompanySize = request.CompanySize,
                    Status = request.Status,
                    FollowsNationalHolidays = request.FollowsNationalHolidays,
                    Notes = request.Notes
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
            1. Validate dữ liệu đầu vào (email, phone, identity number...)
            2. Kiểm tra customer có tồn tại không
            3. Kiểm tra email và identity number có bị trùng với customer khác không
            4. Update thông tin customer vào database
            5. Trả về kết quả

            INPUT (UpdateCustomerRequest):
            - companyName: Tên công ty (required, max 200 chars)
            - contactPersonName: Tên người liên hệ (required, max 100 chars)
            - contactPersonTitle: Chức danh người liên hệ (optional, max 100 chars)
            - identityNumber: Số CCCD (required, 9 hoặc 12 số)
            - identityIssueDate: Ngày cấp CCCD (optional, không được trong tương lai)
            - identityIssuePlace: Nơi cấp CCCD (optional, max 200 chars)
            - email: Email (required, valid email format, max 100 chars)
            - phone: Số điện thoại (required, format +84XXXXXXXXX hoặc 0XXXXXXXXX)
            - avatarUrl: URL avatar (optional, max 500 chars)
            - gender: Giới tính (optional, values: 'male', 'female', 'other')
            - dateOfBirth: Ngày sinh (required, phải trong quá khứ)
            - address: Địa chỉ (required, max 500 chars)
            - city: Thành phố (optional, max 100 chars)
            - district: Quận/Huyện (optional, max 100 chars)
            - industry: Ngành nghề (optional, values: retail, office, manufacturing, hospital, school, residential)
            - companySize: Quy mô (optional, values: small, medium, large, enterprise)
            - status: Trạng thái (required, values: active, inactive, suspended, assigning_manager)
            - followsNationalHolidays: Có theo ngày lễ quốc gia không (required, boolean)
            - notes: Ghi chú (optional, max 1000 chars)

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
              ""email"": ""minhpdse150908@fpt.edu.vn"",
              ""phone"": ""+84329465423"",
              ""avatarUrl"": ""https://example.com/avatar.jpg"",
              ""gender"": ""male"",
              ""dateOfBirth"": ""1990-01-15T00:00:00"",
              ""address"": ""138 Nguyễn Thị Búp, Tân Chánh Hiệp, Quận 12, Thành phố Hồ Chí Minh"",
              ""city"": ""Hồ Chí Minh"",
              ""district"": ""Quận 12"",
              ""industry"": ""retail"",
              ""companySize"": ""small"",
              ""status"": ""active"",
              ""followsNationalHolidays"": true,
              ""notes"": ""Khách hàng VIP""
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
            3. IdentityNumber: Bắt buộc, phải là 9 hoặc 12 số, không được trùng
            4. Email: Bắt buộc, format hợp lệ, không được trùng
            5. Phone: Bắt buộc, format +84XXXXXXXXX hoặc 0XXXXXXXXX
            6. DateOfBirth: Phải trong quá khứ
            7. Address: Bắt buộc, tối đa 500 ký tự
            8. Gender: Chỉ chấp nhận 'male', 'female', hoặc 'other'
            9. Industry: Chỉ chấp nhận retail, office, manufacturing, hospital, school, residential
            10. CompanySize: Chỉ chấp nhận small, medium, large, enterprise
            11. Status: Bắt buộc, chỉ chấp nhận active, inactive, suspended, assigning_manager

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
                ""status"": ""active"",
                ""followsNationalHolidays"": true
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
              followsNationalHolidays: true
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
            - Chỉ update thông tin cơ bản của customer
            - Không update CustomerCode (auto-generated)
            - Không update CustomerSince (giá trị ban đầu)
            - Email và IdentityNumber phải unique (không trùng với customer khác)
            - UpdatedAt sẽ được tự động cập nhật
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
    public string Email { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public string? AvatarUrl { get; init; }
    public string? Gender { get; init; }
    public DateTime DateOfBirth { get; init; }
    public string Address { get; init; } = string.Empty;
    public string? City { get; init; }
    public string? District { get; init; }
    public string? Industry { get; init; }
    public string? CompanySize { get; init; }
    public string Status { get; init; } = string.Empty;
    public bool FollowsNationalHolidays { get; init; }
    public string? Notes { get; init; }
}
