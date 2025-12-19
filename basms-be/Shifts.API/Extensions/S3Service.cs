using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using Microsoft.Extensions.Options;

namespace Shifts.API.Extensions;

/// <summary>
/// Service để tương tác với AWS S3
/// Hỗ trợ upload file chứng từ nghỉ việc (ảnh, PDF, Word, video)
/// </summary>
public interface IS3Service
{
    Task<(bool Success, string? FileUrl, string? ErrorMessage)> UploadFileAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteFileAsync(string fileUrl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tạo pre-signed URL cho file (URL tạm thời để download trực tiếp từ S3)
    /// </summary>
    string GetPresignedUrl(string fileUrlOrKey, int expirationMinutes = 15);
}

public class S3Service : IS3Service
{
    private readonly IAmazonS3 _s3Client;
    private readonly AwsS3Settings _settings;
    private readonly ILogger<S3Service> _logger;

    public S3Service(
        IAmazonS3 s3Client,
        IOptions<AwsS3Settings> settings,
        ILogger<S3Service> logger)
    {
        _s3Client = s3Client;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<(bool Success, string? FileUrl, string? ErrorMessage)> UploadFileAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Sanitize filename
            var sanitizedFileName = SanitizeFileName(fileName);

            // Tạo key cho file trong S3 (shifts/evidence/2025/12/17/{guid}_{filename})
            var fileKey = $"{_settings.FolderPrefix}/{DateTime.UtcNow:yyyy/MM/dd}/{Guid.NewGuid()}_{sanitizedFileName}";

            _logger.LogInformation("📤 Uploading file to S3: {FileKey}", fileKey);

            var uploadRequest = new TransferUtilityUploadRequest
            {
                InputStream = fileStream,
                Key = fileKey,
                BucketName = _settings.BucketName,
                ContentType = contentType,
                CannedACL = S3CannedACL.Private // File private, chỉ access qua presigned URL
            };

            var transferUtility = new TransferUtility(_s3Client);
            await transferUtility.UploadAsync(uploadRequest, cancellationToken);

            // Tạo file URL
            var fileUrl = $"https://{_settings.BucketName}.s3.{_settings.Region}.amazonaws.com/{fileKey}";

            _logger.LogInformation("✅ File uploaded successfully to S3: {FileUrl}", fileUrl);

            return (true, fileUrl, null);
        }
        catch (AmazonS3Exception s3Ex)
        {
            _logger.LogError(s3Ex, "❌ S3 error uploading file {FileName}: {ErrorCode}", fileName, s3Ex.ErrorCode);
            return (false, null, $"S3 upload failed: {s3Ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error uploading file {FileName} to S3", fileName);
            return (false, null, $"Upload failed: {ex.Message}");
        }
    }

    public async Task<bool> DeleteFileAsync(string fileUrlOrKey, CancellationToken cancellationToken = default)
    {
        try
        {
            // Extract key from URL
            string fileKey;
            if (fileUrlOrKey.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                fileUrlOrKey.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                var uri = new Uri(fileUrlOrKey);
                fileKey = uri.AbsolutePath.TrimStart('/');
            }
            else
            {
                fileKey = fileUrlOrKey;
            }

            _logger.LogInformation("🗑️ Deleting file from S3: {FileKey}", fileKey);

            var deleteRequest = new DeleteObjectRequest
            {
                BucketName = _settings.BucketName,
                Key = fileKey
            };

            await _s3Client.DeleteObjectAsync(deleteRequest, cancellationToken);

            _logger.LogInformation("✅ File deleted from S3: {FileKey}", fileKey);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error deleting file from S3: {FileUrlOrKey}", fileUrlOrKey);
            return false;
        }
    }

    /// <summary>
    /// Tạo pre-signed URL cho file - URL tạm thời cho phép download trực tiếp từ S3
    /// </summary>
    public string GetPresignedUrl(string fileUrlOrKey, int expirationMinutes = 15)
    {
        try
        {
            // Extract key: support both full URL and S3 key
            string fileKey;
            if (fileUrlOrKey.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                fileUrlOrKey.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                // Full URL: extract key from URL
                var uri = new Uri(fileUrlOrKey);
                fileKey = uri.AbsolutePath.TrimStart('/');
            }
            else
            {
                // Already an S3 key
                fileKey = fileUrlOrKey;
            }

            _logger.LogInformation("Generating pre-signed URL for {FileKey}, expires in {Minutes} minutes",
                fileKey, expirationMinutes);

            var request = new GetPreSignedUrlRequest
            {
                BucketName = _settings.BucketName,
                Key = fileKey,
                Expires = DateTime.UtcNow.AddMinutes(expirationMinutes),
                Protocol = Protocol.HTTPS
            };

            var presignedUrl = _s3Client.GetPreSignedURL(request);

            _logger.LogInformation("✅ Pre-signed URL generated successfully for {FileKey}", fileKey);

            return presignedUrl;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error generating pre-signed URL for {FileUrlOrKey}", fileUrlOrKey);
            throw;
        }
    }

    /// <summary>
    /// Sanitize filename để loại bỏ ký tự đặc biệt
    /// </summary>
    private static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
            return "file";

        var sanitized = fileName
            .Replace("\"", "")
            .Replace("\\", "")
            .Replace("/", "_")
            .Replace(":", "_")
            .Replace("*", "_")
            .Replace("?", "_")
            .Replace("<", "_")
            .Replace(">", "_")
            .Replace("|", "_")
            .Trim();

        return string.IsNullOrEmpty(sanitized) ? "file" : sanitized;
    }
}
