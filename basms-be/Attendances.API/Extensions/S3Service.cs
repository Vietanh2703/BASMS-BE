namespace Attendances.API.Extensions;


public interface IS3Service
{
    Task<(bool Success, string? FileUrl, string? ErrorMessage)> UploadFileAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default);


    Task<(bool Success, string? FileUrl, string? ErrorMessage)> UploadFileWithCustomKeyAsync(
        Stream fileStream,
        string s3Key,
        string contentType,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteFileAsync(string fileUrl, CancellationToken cancellationToken = default);

    Task<(bool Success, Stream? FileStream, string? ErrorMessage)> DownloadFileAsync(
        string fileUrl,
        CancellationToken cancellationToken = default);


    string GetPresignedUrl(string fileUrlOrKey, int expirationMinutes = 30, string? downloadFileName = null);
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
            var fileKey = $"{_settings.FolderPrefix}/{DateTime.UtcNow:yyyy/MM/dd}/{Guid.NewGuid()}_{fileName}";

            _logger.LogInformation("Uploading file to S3: {FileKey}", fileKey);

            var uploadRequest = new TransferUtilityUploadRequest
            {
                InputStream = fileStream,
                Key = fileKey,
                BucketName = _settings.BucketName,
                ContentType = contentType,
                CannedACL = S3CannedACL.Private
            };

            var transferUtility = new TransferUtility(_s3Client);
            await transferUtility.UploadAsync(uploadRequest, cancellationToken);


            var fileUrl = $"https://{_settings.BucketName}.s3.{_settings.Region}.amazonaws.com/{fileKey}";

            _logger.LogInformation("File uploaded successfully to S3: {FileUrl}", fileUrl);

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

    public async Task<(bool Success, string? FileUrl, string? ErrorMessage)> UploadFileWithCustomKeyAsync(
        Stream fileStream,
        string s3Key,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Uploading file to S3 - Bucket: {BucketName}, Region: {Region}, Key: {S3Key}",
                _settings.BucketName, _settings.Region, s3Key);

            var uploadRequest = new TransferUtilityUploadRequest
            {
                InputStream = fileStream,
                Key = s3Key,
                BucketName = _settings.BucketName,
                ContentType = contentType,
                CannedACL = S3CannedACL.Private
            };

            var transferUtility = new TransferUtility(_s3Client);
            await transferUtility.UploadAsync(uploadRequest, cancellationToken);
            
            var fileUrl = $"https://{_settings.BucketName}.s3.{_settings.Region}.amazonaws.com/{s3Key}";

            _logger.LogInformation("File uploaded successfully to S3: {FileUrl}", fileUrl);

            return (true, fileUrl, null);
        }
        catch (AmazonS3Exception s3Ex)
        {
            _logger.LogError(s3Ex, "S3 error uploading file - Bucket: {BucketName}, Region: {Region}, Key: {S3Key}, ErrorCode: {ErrorCode}, StatusCode: {StatusCode}",
                _settings.BucketName, _settings.Region, s3Key, s3Ex.ErrorCode, s3Ex.StatusCode);
            return (false, null, $"S3 upload failed: {s3Ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file with custom key {S3Key} to S3", s3Key);
            return (false, null, $"Upload failed: {ex.Message}");
        }
    }

    public async Task<bool> DeleteFileAsync(string fileUrlOrKey, CancellationToken cancellationToken = default)
    {
        try
        {
            string fileKey;
            if (fileUrlOrKey.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                fileUrlOrKey.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var decodedUrl = Uri.UnescapeDataString(fileUrlOrKey);
                    var uri = new Uri(decodedUrl);
                    fileKey = uri.AbsolutePath.TrimStart('/');
                }
                catch (UriFormatException)
                {
                    var parts = fileUrlOrKey.Split(new[] { ".amazonaws.com/" }, StringSplitOptions.None);
                    if (parts.Length >= 2)
                    {
                        fileKey = Uri.UnescapeDataString(parts[1]);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to parse S3 URL, using as-is: {FileUrl}", fileUrlOrKey);
                        fileKey = fileUrlOrKey;
                    }
                }
            }
            else
            {
                fileKey = fileUrlOrKey;
            }

            _logger.LogInformation("Deleting file from S3: {FileKey}", fileKey);

            var deleteRequest = new DeleteObjectRequest
            {
                BucketName = _settings.BucketName,
                Key = fileKey
            };

            await _s3Client.DeleteObjectAsync(deleteRequest, cancellationToken);

            _logger.LogInformation("File deleted from S3: {FileKey}", fileKey);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting file from S3: {FileUrlOrKey}", fileUrlOrKey);
            return false;
        }
    }

    public async Task<(bool Success, Stream? FileStream, string? ErrorMessage)> DownloadFileAsync(
        string fileUrlOrKey,
        CancellationToken cancellationToken = default)
    {
        try
        {
            string fileKey;
            if (fileUrlOrKey.StartsWith("http://") || fileUrlOrKey.StartsWith("https://"))
            {
                var uri = new Uri(fileUrlOrKey);
                fileKey = uri.AbsolutePath.TrimStart('/');
            }
            else
            {
                fileKey = fileUrlOrKey;
            }

            _logger.LogInformation("Downloading file from S3: {FileKey}", fileKey);

            var getRequest = new GetObjectRequest
            {
                BucketName = _settings.BucketName,
                Key = fileKey
            };

            var response = await _s3Client.GetObjectAsync(getRequest, cancellationToken);
            var memoryStream = new MemoryStream();
            await response.ResponseStream.CopyToAsync(memoryStream, cancellationToken);
            memoryStream.Position = 0;

            _logger.LogInformation("File downloaded successfully from S3: {FileKey}", fileKey);

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
    
    public string GetPresignedUrl(string fileUrlOrKey, int expirationMinutes = 15, string? downloadFileName = null)
    {
        try
        {
            string fileKey;
            if (fileUrlOrKey.StartsWith("http://") || fileUrlOrKey.StartsWith("https://"))
            {
                var uri = new Uri(fileUrlOrKey);
                fileKey = uri.AbsolutePath.TrimStart('/');
            }
            else
            {
                fileKey = fileUrlOrKey;
            }

            _logger.LogInformation("Generating pre-signed URL for {FileKey}, expires in {Minutes} minutes",
                fileKey, expirationMinutes);
            
            string contentDisposition;
            if (string.IsNullOrEmpty(downloadFileName))
            {
                contentDisposition = "attachment";
            }
            else
            {
                var sanitizedFileName = SanitizeFileName(downloadFileName);
                var encodedFileName = Uri.EscapeDataString(sanitizedFileName);
                contentDisposition = $"attachment; filename=\"{sanitizedFileName}\"; filename*=UTF-8''{encodedFileName}";
            }

            var request = new GetPreSignedUrlRequest
            {
                BucketName = _settings.BucketName,
                Key = fileKey,
                Expires = DateTime.UtcNow.AddMinutes(expirationMinutes),
                Protocol = Protocol.HTTPS,
                ResponseHeaderOverrides = new ResponseHeaderOverrides
                {
                    ContentDisposition = contentDisposition
                }
            };

            var presignedUrl = _s3Client.GetPreSignedURL(request);

            _logger.LogInformation("Pre-signed URL generated successfully for {FileKey} with filename: {FileName}",
                fileKey, downloadFileName ?? "default");

            return presignedUrl;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating pre-signed URL for {FileUrlOrKey}", fileUrlOrKey);
            throw;
        }
    }


    private static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
            return fileName;


        var sanitized = fileName
            .Replace("\"", "") 
            .Replace("\\", "") 
            .Replace("/", "_") 
            .Replace(":", "_") 
            .Replace("*", "_") 
            .Replace("?", "_") 
            .Replace("<", "_") 
            .Replace(">", "_") 
            .Replace("|", "_"); 
        
        sanitized = sanitized.Trim();
        
        if (string.IsNullOrEmpty(sanitized))
        {
            sanitized = "image.jpg";
        }

        return sanitized;
    }
}
