using Carter;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Shifts.API.Extensions;
using System.Text.Json;

namespace Shifts.API.ShiftsHandler.BulkCancelShift;

/// <summary>
/// Endpoint để hủy nhiều ca trực cùng lúc (ốm dài ngày, thai sản, nghỉ phép dài hạn)
///
/// HỖ TRỢ 2 FORMATS:
/// 1. JSON (application/json): Không có file, chỉ URL
/// 2. MULTIPART (multipart/form-data): JSON data + file upload
///
/// USE CASE:
/// - Guard nghỉ ốm dài ngày → Hủy tất cả ca trong khoảng thời gian
/// - Guard nghỉ thai sản 3 tháng → Hủy tất cả ca trong 3 tháng
/// - Guard nghỉ phép dài hạn → Hủy tất cả ca trong khoảng thời gian
///
/// FEATURES:
/// - Upload file chứng từ (ảnh/PDF/Word/video) lên AWS S3
/// - Lưu thông tin vào shift_issues table
/// - Sync với Attendances.API qua events
/// - Send email cho guard và director
/// </summary>
public class BulkCancelShiftEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/shifts/bulk-cancel",
            async (
                HttpRequest request,
                ISender sender,
                IS3Service s3Service,
                ILogger<BulkCancelShiftEndpoint> logger,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    BulkCancelShiftRequest? requestData = null;
                    string? evidenceImageUrl = null;

                    // ================================================================
                    // BƯỚC 1: PARSE REQUEST (JSON hoặc MULTIPART)
                    // ================================================================
                    if (request.HasFormContentType)
                    {
                        // MULTIPART/FORM-DATA với file upload
                        logger.LogInformation("📦 Parsing multipart/form-data request");

                        var form = await request.ReadFormAsync(cancellationToken);

                        // Parse JSON data từ form field "data"
                        if (!form.ContainsKey("data"))
                        {
                            return Results.BadRequest(new
                            {
                                success = false,
                                message = "Thiếu field 'data' chứa JSON request trong multipart form"
                            });
                        }

                        var jsonData = form["data"].ToString();
                        try
                        {
                            requestData = JsonSerializer.Deserialize<BulkCancelShiftRequest>(
                                jsonData,
                                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        }
                        catch (JsonException ex)
                        {
                            return Results.BadRequest(new
                            {
                                success = false,
                                message = $"JSON data không hợp lệ: {ex.Message}"
                            });
                        }

                        // Upload file (nếu có)
                        if (form.Files.Count > 0)
                        {
                            var file = form.Files[0];

                            // Validation: File size (max 100MB)
                            const long maxFileSize = 100 * 1024 * 1024;
                            if (file.Length > maxFileSize)
                            {
                                return Results.BadRequest(new
                                {
                                    success = false,
                                    message = $"File quá lớn. Kích thước tối đa: 100MB"
                                });
                            }

                            // Validation: File type
                            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".pdf", ".doc", ".docx", ".mp4", ".avi", ".mov" };
                            var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();

                            if (!allowedExtensions.Contains(fileExtension))
                            {
                                return Results.BadRequest(new
                                {
                                    success = false,
                                    message = $"Định dạng file không được hỗ trợ: {fileExtension}"
                                });
                            }

                            logger.LogInformation(
                                "📁 Uploading evidence file: {FileName} ({Size}MB)",
                                file.FileName,
                                file.Length / 1024.0 / 1024.0);

                            // Upload lên S3
                            var contentType = file.ContentType ?? GetContentType(fileExtension);
                            using var stream = file.OpenReadStream();

                            var (success, fileUrl, errorMessage) = await s3Service.UploadFileAsync(
                                stream,
                                file.FileName,
                                contentType,
                                cancellationToken);

                            if (!success)
                            {
                                logger.LogError("❌ Failed to upload evidence file: {ErrorMessage}", errorMessage);
                                return Results.BadRequest(new
                                {
                                    success = false,
                                    message = $"Upload file thất bại: {errorMessage}"
                                });
                            }

                            evidenceImageUrl = fileUrl;
                            logger.LogInformation("✅ Evidence file uploaded successfully: {FileUrl}", fileUrl);
                        }
                        else if (!string.IsNullOrEmpty(requestData?.EvidenceImageUrl))
                        {
                            // Nếu không có file nhưng có URL sẵn
                            evidenceImageUrl = requestData.EvidenceImageUrl;
                        }
                    }
                    else if (request.ContentType?.Contains("application/json") == true)
                    {
                        // JSON REQUEST
                        logger.LogInformation("📄 Parsing JSON request");

                        requestData = await request.ReadFromJsonAsync<BulkCancelShiftRequest>(cancellationToken);
                        evidenceImageUrl = requestData?.EvidenceImageUrl;
                    }
                    else
                    {
                        return Results.BadRequest(new
                        {
                            success = false,
                            message = "Content-Type phải là application/json hoặc multipart/form-data"
                        });
                    }

                    if (requestData == null)
                    {
                        return Results.BadRequest(new
                        {
                            success = false,
                            message = "Request data không hợp lệ"
                        });
                    }

                    // ================================================================
                    // BƯỚC 2: VALIDATE REQUEST DATA
                    // ================================================================
                    if (requestData.GuardId == Guid.Empty)
                    {
                        return Results.BadRequest(new { success = false, message = "GuardId không hợp lệ" });
                    }

                    if (requestData.FromDate > requestData.ToDate)
                    {
                        return Results.BadRequest(new { success = false, message = "FromDate phải nhỏ hơn hoặc bằng ToDate" });
                    }

                    if (string.IsNullOrWhiteSpace(requestData.CancellationReason))
                    {
                        return Results.BadRequest(new { success = false, message = "Vui lòng nhập lý do nghỉ việc" });
                    }

                    if (requestData.CancelledBy == Guid.Empty)
                    {
                        return Results.BadRequest(new { success = false, message = "CancelledBy không hợp lệ" });
                    }

                    var validLeaveTypes = new[] { "SICK_LEAVE", "MATERNITY_LEAVE", "LONG_TERM_LEAVE", "OTHER" };
                    if (!validLeaveTypes.Contains(requestData.LeaveType))
                    {
                        return Results.BadRequest(new
                        {
                            success = false,
                            message = "LeaveType không hợp lệ. Chỉ chấp nhận: SICK_LEAVE, MATERNITY_LEAVE, LONG_TERM_LEAVE, OTHER"
                        });
                    }

                    // ================================================================
                    // BƯỚC 3: TẠO COMMAND VÀ EXECUTE BULK CANCEL
                    // ================================================================
                    var command = new BulkCancelShiftCommand(
                        GuardId: requestData.GuardId,
                        FromDate: requestData.FromDate,
                        ToDate: requestData.ToDate,
                        CancellationReason: requestData.CancellationReason,
                        LeaveType: requestData.LeaveType,
                        EvidenceImageUrl: evidenceImageUrl,
                        CancelledBy: requestData.CancelledBy
                    );

                    var result = await sender.Send(command, cancellationToken);

                    if (!result.Success)
                    {
                        return Results.BadRequest(new
                        {
                            success = false,
                            message = result.Message,
                            errors = result.Errors
                        });
                    }

                    // ================================================================
                    // SUCCESS RESPONSE
                    // ================================================================
                    return Results.Ok(new
                    {
                        success = true,
                        message = result.Message,
                        data = new
                        {
                            totalShiftsProcessed = result.TotalShiftsProcessed,
                            shiftsCancelled = result.ShiftsCancelled,
                            assignmentsCancelled = result.AssignmentsCancelled,
                            guardsAffected = result.GuardsAffected,
                            evidenceImageUrl, // URL của file đã upload
                            details = result.Details.Select(d => new
                            {
                                shiftId = d.ShiftId,
                                shiftDate = d.ShiftDate.ToString("yyyy-MM-dd"),
                                shiftTimeSlot = d.ShiftTimeSlot,
                                shiftTime = $"{d.ShiftStartTime:hh\\:mm}-{d.ShiftEndTime:hh\\:mm}",
                                assignmentsCancelled = d.AssignmentsCancelled,
                                success = d.Success,
                                errorMessage = d.ErrorMessage
                            }),
                            warnings = result.Warnings,
                            errors = result.Errors
                        }
                    });
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "❌ Error in bulk cancel shift");
                    return Results.StatusCode(500);
                }
            })
            .WithName("BulkCancelShift")
            .WithTags("Shifts - Bulk Operations")
            .WithDescription("Hủy nhiều ca trực cùng lúc với tùy chọn upload file chứng từ (hỗ trợ JSON và multipart/form-data)")
            .Produces<object>(StatusCodes.Status200OK)
            .Produces<object>(StatusCodes.Status400BadRequest)
            .DisableAntiforgery() // Disable antiforgery cho multipart upload
            .RequireAuthorization();
    }

    /// <summary>
    /// Xác định content type dựa trên file extension
    /// </summary>
    private static string GetContentType(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".mp4" => "video/mp4",
            ".avi" => "video/x-msvideo",
            ".mov" => "video/quicktime",
            _ => "application/octet-stream"
        };
    }
}
