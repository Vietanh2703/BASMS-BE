namespace Contracts.API.ContractsHandler.GetCustomerById;

/// <summary>
/// Endpoint để lấy customer detail với đầy đủ thông tin liên quan
/// </summary>
public class GetCustomerByIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // Route: GET /api/contracts/customers/{id}
        app.MapGet("/api/contracts/customers/{id:guid}", async (
            Guid id,
            ISender sender,
            ILogger<GetCustomerByIdEndpoint> logger) =>
        {
            try
            {
                logger.LogInformation("Get customer detail request for ID: {CustomerId}", id);

                var query = new GetCustomerByIdQuery(id);
                var result = await sender.Send(query);

                if (!result.Success)
                {
                    logger.LogError("Failed to get customer detail: {ErrorMessage}", result.ErrorMessage);
                    return Results.Problem(
                        title: "Error getting customer detail",
                        detail: result.ErrorMessage,
                        statusCode: StatusCodes.Status404NotFound
                    );
                }

                logger.LogInformation(
                    "Successfully retrieved customer {CustomerCode}",
                    result.Customer?.CustomerCode);

                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing get customer detail request for ID: {CustomerId}", id);
                return Results.Problem(
                    title: "Error getting customer detail",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status500InternalServerError
                );
            }
        })
        .WithTags("Contracts - Customers")
        .WithName("GetCustomerById")
        .Produces<GetCustomerByIdResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Lấy thông tin chi tiết customer theo ID")
        .WithDescription(@"
            Endpoint này trả về thông tin chi tiết đầy đủ của một customer, bao gồm:
            - Thông tin cơ bản của customer
            - Danh sách tất cả locations (địa điểm) của customer
            - Danh sách tất cả contracts (hợp đồng) của customer
            - Cho mỗi contract, bao gồm:
              * Document (tài liệu hợp đồng chính)
              * Contract Locations (các địa điểm trong hợp đồng)
              * Shift Schedules (lịch ca làm việc)
              * Public Holidays (ngày lễ áp dụng)

            FLOW:
            1. Query customer từ bảng customers (WHERE Id = @Id AND IsDeleted = 0)
            2. Query customer_locations (WHERE CustomerId = @Id)
            3. Query contracts (WHERE CustomerId = @Id)
            4. Cho mỗi contract, query:
               - contract_documents (WHERE Id = contract.DocumentId)
               - contract_locations (WHERE ContractId = contract.Id)
               - contract_shift_schedules (WHERE ContractId = contract.Id)
               - public_holidays (WHERE ContractId = contract.Id OR ContractId IS NULL)
            5. Map sang DTOs và trả về kết quả

            OUTPUT:
            - success: true/false
            - errorMessage: Thông báo lỗi (nếu có)
            - customer: CustomerDetailDto
              * id: GUID của customer
              * customerCode: Mã khách hàng (CUST-001...)
              * companyName: Tên công ty
              * contactPersonName: Tên người liên hệ
              * contactPersonTitle: Chức danh
              * identityNumber: Số CCCD
              * identityIssueDate: Ngày cấp CCCD
              * identityIssuePlace: Nơi cấp CCCD
              * email: Email
              * phone: Số điện thoại
              * avatarUrl: URL avatar
              * gender: Giới tính
              * dateOfBirth: Ngày sinh
              * address: Địa chỉ
              * city: Thành phố
              * district: Quận/Huyện
              * industry: Ngành nghề
              * companySize: Quy mô công ty
              * status: Trạng thái (active, inactive, suspended)
              * customerSince: Ngày bắt đầu là khách hàng
              * followsNationalHolidays: Có theo ngày lễ Việt Nam không
              * notes: Ghi chú
              * createdAt: Ngày tạo

            - locations: Array of CustomerLocationDto
              * id: GUID của location
              * customerId: GUID của customer
              * locationCode: Mã địa điểm (LOC-001...)
              * locationName: Tên địa điểm
              * locationType: Loại địa điểm (office, warehouse, factory...)
              * address: Địa chỉ
              * city: Thành phố
              * district: Quận/Huyện
              * ward: Phường/Xã
              * latitude: Vĩ độ (GPS)
              * longitude: Kinh độ (GPS)
              * geofenceRadiusMeters: Bán kính check-in (meters)
              * siteManagerName: Tên quản lý địa điểm
              * siteManagerPhone: SĐT quản lý địa điểm
              * operatingHoursType: Loại giờ hoạt động (24/7, business_hours...)
              * requires24x7Coverage: Yêu cầu bảo vệ 24/7
              * minimumGuardsRequired: Số lượng bảo vệ tối thiểu
              * isActive: Trạng thái hoạt động

            - contracts: Array of ContractDto
              * id: GUID của contract
              * customerId: GUID của customer
              * documentId: GUID của document chính
              * contractNumber: Mã hợp đồng (CTR-2025-001...)
              * contractTitle: Tiêu đề hợp đồng
              * contractType: Loại hợp đồng (long_term, short_term...)
              * serviceScope: Phạm vi dịch vụ (continuous_24x7, shift_based...)
              * startDate: Ngày bắt đầu hợp đồng
              * endDate: Ngày kết thúc hợp đồng
              * durationMonths: Thời hạn (tháng)
              * status: Trạng thái (draft, active, suspended...)
              * autoGenerateShifts: Tự động tạo ca
              * signedDate: Ngày ký hợp đồng
              * activatedAt: Ngày kích hoạt
              * createdAt: Ngày tạo

              * document: ContractDocumentDto (tài liệu hợp đồng chính)
                - id: GUID của document
                - documentType: Loại tài liệu (contract, amendment...)
                - category: Danh mục (labor_contract, service_contract...)
                - documentName: Tên file
                - fileUrl: URL file trên S3
                - fileSize: Kích thước (bytes)
                - fileSizeFormatted: Kích thước dạng human-readable
                - version: Phiên bản (1.0, 1.1...)
                - startDate: Ngày bắt đầu hợp đồng
                - endDate: Ngày kết thúc hợp đồng
                - signDate: Ngày ký
                - approvedAt: Ngày phê duyệt
                - createdAt: Ngày tạo

              * locations: Array of ContractLocationDto
                - id: GUID của contract location
                - contractId: GUID của contract
                - locationId: GUID của customer location
                - guardsRequired: Số lượng bảo vệ yêu cầu
                - coverageType: Loại coverage (24x7, day_only...)
                - serviceStartDate: Ngày bắt đầu dịch vụ
                - serviceEndDate: Ngày kết thúc dịch vụ
                - isPrimaryLocation: Địa điểm chính
                - priorityLevel: Mức độ ưu tiên (1=cao nhất)
                - autoGenerateShifts: Tự động tạo ca
                - isActive: Trạng thái hoạt động

              * shiftSchedules: Array of ContractShiftScheduleDto
                - id: GUID của shift schedule
                - contractId: GUID của contract
                - locationId: GUID của location (NULL = all locations)
                - scheduleName: Tên mẫu ca (Morning Shift, Night Patrol...)
                - scheduleType: Loại ca (regular, overtime...)
                - shiftStartTime: Giờ bắt đầu ca
                - shiftEndTime: Giờ kết thúc ca
                - crossesMidnight: Ca có qua đêm không
                - durationHours: Thời lượng ca (giờ)
                - breakMinutes: Thời gian nghỉ giải lao (phút)
                - guardsPerShift: Số lượng bảo vệ/ca
                - recurrenceType: Loại lặp lại (daily, weekly...)
                - appliesMonday/Tuesday/etc: Áp dụng cho thứ mấy
                - appliesOnPublicHolidays: Áp dụng vào ngày lễ
                - appliesOnWeekends: Áp dụng vào cuối tuần
                - autoGenerateEnabled: Bật tự động tạo ca
                - effectiveFrom: Có hiệu lực từ ngày
                - effectiveTo: Có hiệu lực đến ngày
                - isActive: Trạng thái hoạt động

              * publicHolidays: Array of PublicHolidayDto
                - id: GUID của holiday
                - contractId: GUID của contract (NULL = áp dụng chung)
                - holidayDate: Ngày lễ
                - holidayName: Tên ngày lễ (Tết Nguyên Đán...)
                - holidayNameEn: Tên tiếng Anh
                - holidayCategory: Loại (national, tet, regional...)
                - isTetPeriod: Có phải kỳ nghỉ Tết không
                - isTetHoliday: Có phải Tết Nguyên Đán không
                - isOfficialHoliday: Nghỉ chính thức theo luật
                - appliesNationwide: Áp dụng toàn quốc
                - year: Năm

            VÍ DỤ RESPONSE:
            ===============
            ```json
            {
              ""success"": true,
              ""customer"": {
                ""id"": ""guid-customer-001"",
                ""customerCode"": ""CUST-001"",
                ""companyName"": ""Bệnh viện ABC"",
                ""contactPersonName"": ""Nguyễn Văn A"",
                ""contactPersonTitle"": ""Giám đốc hành chính"",
                ""identityNumber"": ""001234567890"",
                ""email"": ""admin@benhvien-abc.com"",
                ""phone"": ""0901234567"",
                ""address"": ""123 Đường ABC"",
                ""city"": ""Hồ Chí Minh"",
                ""status"": ""active"",
                ""customerSince"": ""2025-01-01T00:00:00Z""
              },
              ""locations"": [
                {
                  ""id"": ""guid-location-001"",
                  ""locationCode"": ""LOC-001"",
                  ""locationName"": ""Chi nhánh Quận 1"",
                  ""locationType"": ""hospital"",
                  ""address"": ""123 Đường ABC, Quận 1"",
                  ""latitude"": 10.762622,
                  ""longitude"": 106.660172,
                  ""geofenceRadiusMeters"": 100,
                  ""requires24x7Coverage"": true,
                  ""minimumGuardsRequired"": 2
                }
              ],
              ""contracts"": [
                {
                  ""id"": ""guid-contract-001"",
                  ""contractNumber"": ""CTR-2025-001"",
                  ""contractTitle"": ""Dịch vụ bảo vệ 24/7"",
                  ""contractType"": ""long_term"",
                  ""startDate"": ""2025-01-01T00:00:00Z"",
                  ""endDate"": ""2025-12-31T00:00:00Z"",
                  ""status"": ""active"",
                  ""document"": {
                    ""id"": ""guid-doc-001"",
                    ""documentName"": ""contract-2025-001.pdf"",
                    ""fileUrl"": ""https://s3.../contract.pdf"",
                    ""fileSizeFormatted"": ""2.5 MB"",
                    ""version"": ""1.0""
                  },
                  ""locations"": [
                    {
                      ""id"": ""guid-cl-001"",
                      ""locationId"": ""guid-location-001"",
                      ""guardsRequired"": 2,
                      ""coverageType"": ""24x7"",
                      ""isPrimaryLocation"": true
                    }
                  ],
                  ""shiftSchedules"": [
                    {
                      ""id"": ""guid-schedule-001"",
                      ""scheduleName"": ""Morning Shift"",
                      ""shiftStartTime"": ""08:00:00"",
                      ""shiftEndTime"": ""17:00:00"",
                      ""durationHours"": 9,
                      ""guardsPerShift"": 2,
                      ""appliesMonday"": true,
                      ""appliesTuesday"": true,
                      ""appliesWednesday"": true,
                      ""appliesThursday"": true,
                      ""appliesFriday"": true
                    }
                  ],
                  ""publicHolidays"": [
                    {
                      ""id"": ""guid-holiday-001"",
                      ""holidayDate"": ""2025-01-01T00:00:00Z"",
                      ""holidayName"": ""Tết Dương lịch"",
                      ""holidayCategory"": ""national"",
                      ""isOfficialHoliday"": true,
                      ""year"": 2025
                    }
                  ]
                }
              ]
            }
            ```

            CÁCH SỬ DỤNG:
            =============

            **cURL:**
            ```bash
            curl -X GET 'http://localhost:5000/api/contracts/customers/guid-xxx-xxx'
            ```

            **JavaScript Fetch:**
            ```javascript
            const customerId = 'guid-xxx-xxx';
            fetch(`/api/contracts/customers/${customerId}`)
              .then(response => response.json())
              .then(data => {
                console.log('Customer:', data.customer.companyName);
                console.log('Locations:', data.locations.length);
                console.log('Contracts:', data.contracts.length);

                // Iterate through contracts
                data.contracts.forEach(contract => {
                  console.log(`Contract ${contract.contractNumber}:`);
                  console.log(`  - ${contract.locations.length} locations`);
                  console.log(`  - ${contract.shiftSchedules.length} shift schedules`);
                  console.log(`  - ${contract.publicHolidays.length} holidays`);
                });
              });
            ```

            **React Example:**
            ```jsx
            const CustomerDetail = ({ customerId }) => {
              const [data, setData] = useState(null);

              useEffect(() => {
                fetch(`/api/contracts/customers/${customerId}`)
                  .then(res => res.json())
                  .then(setData);
              }, [customerId]);

              if (!data) return <div>Loading...</div>;

              return (
                <div>
                  <h1>{data.customer.companyName}</h1>
                  <p>Code: {data.customer.customerCode}</p>

                  <h2>Locations ({data.locations.length})</h2>
                  <ul>
                    {data.locations.map(loc => (
                      <li key={loc.id}>
                        {loc.locationName} - {loc.locationType}
                      </li>
                    ))}
                  </ul>

                  <h2>Contracts ({data.contracts.length})</h2>
                  {data.contracts.map(contract => (
                    <div key={contract.id}>
                      <h3>{contract.contractNumber} - {contract.contractTitle}</h3>
                      <p>Status: {contract.status}</p>
                      <p>Period: {contract.startDate} - {contract.endDate}</p>

                      <h4>Shift Schedules ({contract.shiftSchedules.length})</h4>
                      <ul>
                        {contract.shiftSchedules.map(schedule => (
                          <li key={schedule.id}>
                            {schedule.scheduleName}: {schedule.shiftStartTime} - {schedule.shiftEndTime}
                          </li>
                        ))}
                      </ul>
                    </div>
                  ))}
                </div>
              );
            };
            ```

            LƯU Ý:
            =======
            - Chỉ trả về customer chưa bị xóa (IsDeleted = 0)
            - Chỉ trả về locations, contracts, documents chưa bị xóa
            - Public holidays bao gồm cả ngày lễ chung (ContractId IS NULL) và ngày lễ riêng của contract
            - Chỉ query public holidays từ năm hiện tại trở đi
            - Nếu không tìm thấy customer, trả về 404 Not Found
            - Contracts được sắp xếp theo ngày tạo (mới nhất trước)
            - Locations được sắp xếp theo LocationCode
            - Contract locations được sắp xếp theo priority (primary location trước)
        ");
    }
}
