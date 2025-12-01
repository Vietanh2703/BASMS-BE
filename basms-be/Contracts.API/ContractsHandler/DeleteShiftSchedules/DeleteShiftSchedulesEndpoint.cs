namespace Contracts.API.ContractsHandler.DeleteShiftSchedules;

/// <summary>
/// Endpoint để xóa shift schedule
/// </summary>
public class DeleteShiftSchedulesEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // Route: DELETE /api/contracts/shift-schedules/{shiftScheduleId}
        app.MapDelete("/api/contracts/shift-schedules/{shiftScheduleId}", async (
            Guid shiftScheduleId,
            ISender sender,
            ILogger<DeleteShiftSchedulesEndpoint> logger) =>
        {
            try
            {
                logger.LogInformation("Delete shift schedule request for ID: {ShiftScheduleId}", shiftScheduleId);

                var command = new DeleteShiftSchedulesCommand(shiftScheduleId);
                var result = await sender.Send(command);

                if (!result.Success)
                {
                    logger.LogError("Failed to delete shift schedule: {ErrorMessage}", result.ErrorMessage);
                    return Results.Problem(
                        title: "Error deleting shift schedule",
                        detail: result.ErrorMessage,
                        statusCode: StatusCodes.Status400BadRequest
                    );
                }

                logger.LogInformation("Successfully deleted shift schedule: {ShiftScheduleId}", result.ShiftScheduleId);

                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing delete shift schedule request for ID: {ShiftScheduleId}", shiftScheduleId);
                return Results.Problem(
                    title: "Error deleting shift schedule",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status500InternalServerError
                );
            }
        })
        .WithTags("Contracts - Shift Schedules")
        .WithName("DeleteShiftSchedules")
        .Produces<DeleteShiftSchedulesResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Xóa shift schedule")
        .WithDescription(@"
            Endpoint này xóa một shift schedule template khỏi hệ thống.

            ⚠️ CẢNH BÁO: Đây là HARD DELETE - shift schedule sẽ bị XÓA VĨNH VIỄN khỏi database.

            FLOW:
            1. Kiểm tra shift schedule có tồn tại không
            2. Xóa shift schedule khỏi database (hard delete)
            3. Trả về kết quả

            INPUT:
            - shiftScheduleId: GUID của shift schedule (trong URL path)

            OUTPUT (DeleteShiftSchedulesResult):
            - success: true/false
            - errorMessage: Thông báo lỗi (nếu có)
            - shiftScheduleId: GUID của shift schedule đã xóa

            VÍ DỤ REQUEST:
            ==============
            ```bash
            DELETE /api/contracts/shift-schedules/a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d
            ```

            VÍ DỤ RESPONSE THÀNH CÔNG:
            ==========================
            ```json
            {
              ""success"": true,
              ""errorMessage"": null,
              ""shiftScheduleId"": ""a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d""
            }
            ```

            VÍ DỤ RESPONSE LỖI:
            ===================
            ```json
            {
              ""success"": false,
              ""errorMessage"": ""Shift schedule with ID a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d not found"",
              ""shiftScheduleId"": null
            }
            ```

            CÁCH SỬ DỤNG:
            =============

            **cURL:**
            ```bash
            curl -X DELETE 'http://localhost:5000/api/contracts/shift-schedules/a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d'
            ```

            **JavaScript Fetch:**
            ```javascript
            const deleteShiftSchedule = async (shiftScheduleId) => {
              const response = await fetch(`/api/contracts/shift-schedules/${shiftScheduleId}`, {
                method: 'DELETE'
              });

              const result = await response.json();

              if (result.success) {
                console.log('Shift schedule deleted successfully');
              } else {
                console.error(`Error: ${result.errorMessage}`);
              }

              return result;
            };

            // Sử dụng
            deleteShiftSchedule('a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d');
            ```

            **React Example:**
            ```jsx
            const DeleteShiftScheduleButton = ({ shiftScheduleId, onDeleted }) => {
              const [isDeleting, setIsDeleting] = useState(false);

              const handleDelete = async () => {
                if (!confirm('Bạn có chắc chắn muốn xóa shift schedule này? Hành động này KHÔNG THỂ HOÀN TÁC!')) {
                  return;
                }

                setIsDeleting(true);

                try {
                  const response = await fetch(`/api/contracts/shift-schedules/${shiftScheduleId}`, {
                    method: 'DELETE'
                  });

                  const data = await response.json();

                  if (data.success) {
                    alert('Shift schedule deleted successfully');
                    onDeleted();
                  } else {
                    alert(`Error: ${data.errorMessage}`);
                  }
                } catch (error) {
                  alert(`Error: ${error.message}`);
                } finally {
                  setIsDeleting(false);
                }
              };

              return (
                <button
                  onClick={handleDelete}
                  disabled={isDeleting}
                  className=""btn-delete""
                >
                  {isDeleting ? 'Deleting...' : 'Delete Shift Schedule'}
                </button>
              );
            };
            ```

            USE CASES:
            ==========
            1. **Xóa shift schedule không còn sử dụng**: Khi contract kết thúc hoặc thay đổi mô hình
            2. **Xóa shift schedule tạo nhầm**: Khi tạo schedule sai và cần xóa đi
            3. **Cleanup database**: Dọn dẹp các schedules cũ không còn cần thiết

            LƯU Ý QUAN TRỌNG:
            ==================
            ⚠️ **HARD DELETE - KHÔNG THỂ PHỤC HỒI**
            - Shift schedule sẽ bị XÓA VĨNH VIỄN khỏi database
            - KHÔNG có cơ chế soft delete (IsDeleted = 1)
            - Các shifts đã được tạo từ schedule này KHÔNG bị ảnh hưởng (vẫn tồn tại)
            - Chỉ template schedule bị xóa, không ảnh hưởng đến shifts thực tế đã generated

            ⚠️ **KHI NÀO NÊN XÓA:**
            - Shift schedule tạo nhầm, chưa generate shifts nào
            - Shift schedule cũ không còn sử dụng
            - Contract đã kết thúc và không cần giữ lịch sử

            ⚠️ **KHI NÀO KHÔNG NÊN XÓA:**
            - Shift schedule đang active và đang generate shifts
            - Cần giữ lại để tracking lịch sử
            - Có thể cần sử dụng lại trong tương lai

            💡 **THAY THẾ:**
            Nếu chỉ muốn tạm dừng schedule mà không xóa vĩnh viễn:
            - Sử dụng endpoint UPDATE để set IsActive = false
            - Hoặc set AutoGenerateEnabled = false
            - Hoặc set EffectiveTo = ngày hiện tại
        ");
    }
}
