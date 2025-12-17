using Carter;
using Shifts.API.Extensions;

namespace Shifts.API.FileHandler;

/// <summary>
/// Endpoint để upload file chứng từ lên AWS S3
/// Hỗ trợ: Ảnh (jpg, png), PDF, Word (doc, docx), Video (mp4, avi)
///
/// USE CASE:
/// 1. Upload đơn xin nghỉ (PDF, Word)
/// 2. Upload giấy khám bệnh (PDF, ảnh)
/// 3. Upload giấy thai sản (PDF, ảnh)
/// 4. Upload video chứng minh (mp4)
/// </summary>
public class UploadFileEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/files/upload",
            async (
                HttpRequest request,
                IS3Service s3Service,
                ILogger<UploadFileEndpoint> logger,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    // Kiểm tra có file không
                    if (!request.HasFormContentType || request.Form.Files.Count == 0)
                    {
                        return Results.BadRequest(new
                        {
                            success = false,
                            message = "Vui lòng chọn file để upload"
                        });
                    }

                    var file = request.Form.Files[0];

                    // Validation: File size (max 100MB)
                    const long maxFileSize = 100 * 1024 * 1024; // 100MB
                    if (file.Length > maxFileSize)
                    {
                        return Results.BadRequest(new
                        {
                            success = false,
                            message = $"File quá lớn. Kích thước tối đa: 100MB. File của bạn: {file.Length / 1024 / 1024}MB"
                        });
                    }

                    // Validation: File type
                    var allowedExtensions = new[]
                    {
                        ".jpg", ".jpeg", ".png", ".gif", ".bmp", // Ảnh
                        ".pdf", // PDF
                        ".doc", ".docx", // Word
                        ".mp4", ".avi", ".mov", ".wmv" // Video
                    };

                    var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
                    if (!allowedExtensions.Contains(fileExtension))
                    {
                        return Results.BadRequest(new
                        {
                            success = false,
                            message = $"Định dạng file không được hỗ trợ: {fileExtension}. " +
                                     $"Chỉ chấp nhận: {string.Join(", ", allowedExtensions)}"
                        });
                    }

                    // Xác định content type
                    var contentType = file.ContentType ?? GetContentType(fileExtension);

                    logger.LogInformation(
                        "📁 Uploading file: {FileName} ({Size}MB, Type: {ContentType})",
                        file.FileName,
                        file.Length / 1024.0 / 1024.0,
                        contentType);

                    // Upload lên S3
                    using var stream = file.OpenReadStream();
                    var (success, fileUrl, errorMessage) = await s3Service.UploadFileAsync(
                        stream,
                        file.FileName,
                        contentType,
                        cancellationToken);

                    if (!success)
                    {
                        logger.LogError("❌ Failed to upload file: {ErrorMessage}", errorMessage);
                        return Results.BadRequest(new
                        {
                            success = false,
                            message = errorMessage ?? "Upload thất bại"
                        });
                    }

                    logger.LogInformation("✅ File uploaded successfully: {FileUrl}", fileUrl);

                    return Results.Ok(new
                    {
                        success = true,
                        message = "Upload file thành công",
                        data = new
                        {
                            fileUrl,
                            fileName = file.FileName,
                            fileSize = file.Length,
                            contentType,
                            uploadedAt = DateTime.UtcNow
                        }
                    });
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "❌ Error uploading file");
                    return Results.StatusCode(500);
                }
            })
            .WithName("UploadFile")
            .WithTags("Files - Upload")
            .WithDescription("Upload file chứng từ lên AWS S3 (ảnh, PDF, Word, video)")
            .Produces<object>(StatusCodes.Status200OK)
            .Produces<object>(StatusCodes.Status400BadRequest)
            .DisableAntiforgery() // Disable antiforgery cho file upload
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
            ".bmp" => "image/bmp",
            ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".mp4" => "video/mp4",
            ".avi" => "video/x-msvideo",
            ".mov" => "video/quicktime",
            ".wmv" => "video/x-ms-wmv",
            _ => "application/octet-stream"
        };
    }
}
