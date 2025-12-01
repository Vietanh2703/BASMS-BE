namespace Contracts.API.ContractsHandler.DeletePublicHoliday;

/// <summary>
/// Endpoint để xóa public holiday
/// </summary>
public class DeletePublicHolidayEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // Route: DELETE /api/contracts/holidays/{holidayId}
        app.MapDelete("/api/contracts/holidays/{holidayId}", async (
            Guid holidayId,
            ISender sender,
            ILogger<DeletePublicHolidayEndpoint> logger) =>
        {
            try
            {
                logger.LogInformation("Delete public holiday request for ID: {HolidayId}", holidayId);

                var command = new DeletePublicHolidayCommand(holidayId);
                var result = await sender.Send(command);

                if (!result.Success)
                {
                    logger.LogError("Failed to delete holiday: {ErrorMessage}", result.ErrorMessage);
                    return Results.NotFound(new
                    {
                        success = false,
                        error = result.ErrorMessage
                    });
                }

                logger.LogInformation(
                    "Successfully deleted holiday {HolidayName} (ID: {HolidayId})",
                    result.HolidayName, result.HolidayId);

                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing delete holiday request for ID: {HolidayId}", holidayId);
                return Results.Problem(
                    title: "Error deleting holiday",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status500InternalServerError
                );
            }
        })
        .WithTags("Contracts - Holidays")
        .WithName("DeletePublicHoliday")
        .Produces<DeletePublicHolidayResult>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Xóa public holiday")
        .WithDescription(@"
            Endpoint này xóa một public holiday khỏi hệ thống.
            Sử dụng HARD DELETE - holiday sẽ bị xóa vĩnh viễn khỏi database.

            FLOW:
            1. Kiểm tra holiday có tồn tại không
            2. Xóa holiday khỏi database (hard delete)
            3. Trả về kết quả

            INPUT:
            - holidayId: GUID của holiday cần xóa (trong URL path)

            OUTPUT (DeletePublicHolidayResult):
            - success: true/false
            - errorMessage: Thông báo lỗi (nếu có)
            - holidayId: GUID của holiday đã xóa
            - holidayName: Tên ngày lễ đã xóa

            VÍ DỤ REQUEST:
            ==============
            ```bash
            DELETE /api/contracts/holidays/6f07c4d8-0258-4f6c-8659-a40adbccf8a8
            ```

            VÍ DỤ RESPONSE THÀNH CÔNG:
            ==========================
            ```json
            {
              ""success"": true,
              ""errorMessage"": null,
              ""holidayId"": ""6f07c4d8-0258-4f6c-8659-a40adbccf8a8"",
              ""holidayName"": ""Tết Nguyên Đán""
            }
            ```

            VÍ DỤ RESPONSE KHI KHÔNG TÌM THẤY:
            ===================================
            ```json
            {
              ""success"": false,
              ""error"": ""Public holiday with ID 6f07c4d8-0258-4f6c-8659-a40adbccf8a8 not found""
            }
            ```

            CÁCH SỬ DỤNG:
            =============

            **cURL:**
            ```bash
            curl -X DELETE 'http://localhost:5000/api/contracts/holidays/6f07c4d8-0258-4f6c-8659-a40adbccf8a8'
            ```

            **JavaScript Fetch:**
            ```javascript
            const deleteHoliday = async (holidayId) => {
              const response = await fetch(`/api/contracts/holidays/${holidayId}`, {
                method: 'DELETE'
              });

              const result = await response.json();

              if (result.success) {
                console.log(`Deleted holiday: ${result.holidayName}`);
              } else {
                console.error(`Error: ${result.error || result.errorMessage}`);
              }

              return result;
            };

            // Sử dụng
            deleteHoliday('6f07c4d8-0258-4f6c-8659-a40adbccf8a8');
            ```

            **React Example:**
            ```jsx
            const DeleteHolidayButton = ({ holidayId, holidayName, onDeleted }) => {
              const [deleting, setDeleting] = useState(false);

              const handleDelete = async () => {
                if (!confirm(`Are you sure you want to delete ${holidayName}?`)) {
                  return;
                }

                setDeleting(true);

                try {
                  const response = await fetch(`/api/contracts/holidays/${holidayId}`, {
                    method: 'DELETE'
                  });

                  const result = await response.json();

                  if (result.success) {
                    alert(`Deleted: ${result.holidayName}`);
                    onDeleted(holidayId);
                  } else {
                    alert(`Error: ${result.error || result.errorMessage}`);
                  }
                } catch (error) {
                  alert(`Error: ${error.message}`);
                } finally {
                  setDeleting(false);
                }
              };

              return (
                <button onClick={handleDelete} disabled={deleting}>
                  {deleting ? 'Deleting...' : 'Delete Holiday'}
                </button>
              );
            };
            ```

            **With Confirmation Dialog:**
            ```javascript
            const deleteHolidayWithConfirmation = async (holidayId, holidayName) => {
              // Show confirmation dialog
              const confirmed = confirm(
                `Bạn có chắc muốn xóa ngày lễ '${holidayName}'?\n\n` +
                `Hành động này KHÔNG THỂ HOÀN TÁC!`
              );

              if (!confirmed) {
                console.log('Delete cancelled');
                return { success: false, cancelled: true };
              }

              // Proceed with deletion
              const response = await fetch(`/api/contracts/holidays/${holidayId}`, {
                method: 'DELETE'
              });

              const result = await response.json();

              if (result.success) {
                console.log(`✓ Successfully deleted: ${result.holidayName}`);
              } else {
                console.error(`✗ Failed to delete: ${result.error || result.errorMessage}`);
              }

              return result;
            };

            // Sử dụng
            deleteHolidayWithConfirmation(
              '6f07c4d8-0258-4f6c-8659-a40adbccf8a8',
              'Tết Nguyên Đán'
            );
            ```

            LƯU Ý QUAN TRỌNG:
            =================
            ⚠️ HARD DELETE - Holiday sẽ bị XÓA VĨNH VIỄN khỏi database
            ⚠️ KHÔNG THỂ KHÔI PHỤC sau khi xóa
            ⚠️ Nên hiển thị confirmation dialog trước khi xóa
            ⚠️ Cân nhắc sử dụng soft delete (IsDeleted flag) nếu cần khôi phục

            USE CASES:
            ==========
            - Xóa holiday đã tạo nhầm
            - Xóa holiday duplicate
            - Xóa holiday không còn áp dụng
            - Admin cleanup data

            BEST PRACTICES:
            ===============
            1. Luôn confirm với user trước khi xóa
            2. Log action để audit trail
            3. Kiểm tra permission trước khi cho phép xóa
            4. Xem xét soft delete thay vì hard delete cho production
        ");
    }
}
