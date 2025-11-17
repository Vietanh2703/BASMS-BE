using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using Microsoft.Extensions.Options;

namespace Contracts.API.Extensions;

/// <summary>
/// Service để tương tác với AWS S3
/// </summary>
public interface IS3Service
{
    Task<(bool Success, string? FileUrl, string? ErrorMessage)> UploadFileAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Upload file với custom S3 key (không thêm prefix/timestamp tự động)
    /// </summary>
    Task<(bool Success, string? FileUrl, string? ErrorMessage)> UploadFileWithCustomKeyAsync(
        Stream fileStream,
        string s3Key,
        string contentType,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteFileAsync(string fileUrl, CancellationToken cancellationToken = default);

    Task<(bool Success, Stream? FileStream, string? ErrorMessage)> DownloadFileAsync(
        string fileUrl,
        CancellationToken cancellationToken = default);
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
            // Tạo key cho file trong S3 (path/to/file.pdf)
            var fileKey = $"{_settings.FolderPrefix}/{DateTime.UtcNow:yyyy/MM/dd}/{Guid.NewGuid()}_{fileName}";

            _logger.LogInformation("Uploading file to S3: {FileKey}", fileKey);

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

            _logger.LogInformation("✓ File uploaded successfully to S3: {FileUrl}", fileUrl);

            return (true, fileUrl, null);
        }
        catch (AmazonS3Exception s3Ex)
        {
            _logger.LogError(s3Ex, "S3 error uploading file {FileName}: {ErrorCode}", fileName, s3Ex.ErrorCode);
            return (false, null, $"S3 upload failed: {s3Ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file {FileName} to S3", fileName);
            return (false, null, $"Upload failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Upload file với custom S3 key - KHÔNG thêm prefix/timestamp
    /// Sử dụng khi muốn kiểm soát hoàn toàn đường dẫn file trên S3
    /// </summary>
    public async Task<(bool Success, string? FileUrl, string? ErrorMessage)> UploadFileWithCustomKeyAsync(
        Stream fileStream,
        string s3Key,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Uploading file to S3 with custom key: {S3Key}", s3Key);

            var uploadRequest = new TransferUtilityUploadRequest
            {
                InputStream = fileStream,
                Key = s3Key,  // Sử dụng key trực tiếp, không thêm prefix
                BucketName = _settings.BucketName,
                ContentType = contentType,
                CannedACL = S3CannedACL.Private
            };

            var transferUtility = new TransferUtility(_s3Client);
            await transferUtility.UploadAsync(uploadRequest, cancellationToken);

            // Tạo file URL từ custom key
            var fileUrl = $"https://{_settings.BucketName}.s3.{_settings.Region}.amazonaws.com/{s3Key}";

            _logger.LogInformation("✓ File uploaded successfully to S3: {FileUrl}", fileUrl);

            return (true, fileUrl, null);
        }
        catch (AmazonS3Exception s3Ex)
        {
            _logger.LogError(s3Ex, "S3 error uploading file with custom key {S3Key}: {ErrorCode}", s3Key, s3Ex.ErrorCode);
            return (false, null, $"S3 upload failed: {s3Ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file with custom key {S3Key} to S3", s3Key);
            return (false, null, $"Upload failed: {ex.Message}");
        }
    }

    public async Task<bool> DeleteFileAsync(string fileUrl, CancellationToken cancellationToken = default)
    {
        try
        {
            // Extract key from URL
            var uri = new Uri(fileUrl);
            var fileKey = uri.AbsolutePath.TrimStart('/');

            _logger.LogInformation("Deleting file from S3: {FileKey}", fileKey);

            var deleteRequest = new DeleteObjectRequest
            {
                BucketName = _settings.BucketName,
                Key = fileKey
            };

            await _s3Client.DeleteObjectAsync(deleteRequest, cancellationToken);

            _logger.LogInformation("✓ File deleted from S3: {FileKey}", fileKey);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting file from S3: {FileUrl}", fileUrl);
            return false;
        }
    }

    public async Task<(bool Success, Stream? FileStream, string? ErrorMessage)> DownloadFileAsync(
        string fileUrlOrKey,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Extract key: support both full URL and S3 key
            string fileKey;
            if (fileUrlOrKey.StartsWith("http://") || fileUrlOrKey.StartsWith("https://"))
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

            _logger.LogInformation("Downloading file from S3: {FileKey}", fileKey);

            var getRequest = new GetObjectRequest
            {
                BucketName = _settings.BucketName,
                Key = fileKey
            };

            var response = await _s3Client.GetObjectAsync(getRequest, cancellationToken);

            // Copy to MemoryStream để có thể sử dụng sau khi dispose response
            var memoryStream = new MemoryStream();
            await response.ResponseStream.CopyToAsync(memoryStream, cancellationToken);
            memoryStream.Position = 0;

            _logger.LogInformation("✓ File downloaded successfully from S3: {FileKey}", fileKey);

            return (true, memoryStream, null);
        }
        catch (AmazonS3Exception s3Ex)
        {
            _logger.LogError(s3Ex, "S3 error downloading file {FileUrlOrKey}: {ErrorCode}", fileUrlOrKey, s3Ex.ErrorCode);
            return (false, null, $"S3 download failed: {s3Ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading file from S3: {FileUrlOrKey}", fileUrlOrKey);
            return (false, null, $"Download failed: {ex.Message}");
        }
    }
}
