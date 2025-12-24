namespace Shifts.API.ShiftsHandler.BulkCancelShift;

public class BulkCancelShiftEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/shifts/bulk-cancel",
            async (
                HttpRequest request,
                ISender sender,
                ILogger<BulkCancelShiftEndpoint> logger,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    BulkCancelShiftRequest? requestData = null;
                    Stream? fileStream = null;
                    string? fileName = null;
                    string? contentType = null;
                    
                    if (request.HasFormContentType)
                    {
                        logger.LogInformation("Parsing multipart/form-data request");

                        var form = await request.ReadFormAsync(cancellationToken);
                        
                        logger.LogInformation("Form keys: {Keys}", string.Join(", ", form.Keys));
                        logger.LogInformation("Form files count: {Count}", form.Files.Count);
                        
                        var dataKey = form.Keys.FirstOrDefault(k => k.Equals("data", StringComparison.OrdinalIgnoreCase));

                        if (dataKey == null)
                        {
                            return Results.BadRequest(new
                            {
                                success = false,
                                message = $"Thiếu field 'data' chứa JSON request trong multipart form. Các field hiện tại: [{string.Join(", ", form.Keys)}]"
                            });
                        }

                        var jsonData = form[dataKey].ToString();
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
                        
                        if (form.Files.Count > 0)
                        {
                            var file = form.Files[0];
                            
                            const long maxFileSize = 100 * 1024 * 1024;
                            if (file.Length > maxFileSize)
                            {
                                return Results.BadRequest(new
                                {
                                    success = false,
                                    message = $"File quá lớn. Kích thước tối đa: 100MB"
                                });
                            }
                            
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
                                "Received evidence file: {FileName} ({Size}MB)",
                                file.FileName,
                                file.Length / 1024.0 / 1024.0);
                            
                            fileStream = file.OpenReadStream();
                            fileName = file.FileName;
                            contentType = file.ContentType;
                        }
                    }
                    else if (request.ContentType?.Contains("application/json") == true)
                    {
                        logger.LogInformation("Parsing JSON request");
                        requestData = await request.ReadFromJsonAsync<BulkCancelShiftRequest>(cancellationToken);
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
                    
                    var command = new BulkCancelShiftCommand(
                        GuardId: requestData.GuardId,
                        FromDate: requestData.FromDate,
                        ToDate: requestData.ToDate,
                        CancellationReason: requestData.CancellationReason,
                        LeaveType: requestData.LeaveType,
                        EvidenceFileStream: fileStream,
                        EvidenceFileName: fileName,
                        EvidenceContentType: contentType,
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
                            evidenceFileUrl = result.EvidenceFileUrl, 
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
                    logger.LogError(ex, "Error in bulk cancel shift");
                    return Results.StatusCode(500);
                }
            })
            .WithName("BulkCancelShift")
            .WithTags("Shifts - Bulk Operations")
            .WithDescription("Hủy nhiều ca trực cùng lúc với tùy chọn upload file chứng từ (hỗ trợ JSON và multipart/form-data)")
            .Produces<object>(StatusCodes.Status200OK)
            .Produces<object>(StatusCodes.Status400BadRequest)
            .DisableAntiforgery() 
            .RequireAuthorization();
    }


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
