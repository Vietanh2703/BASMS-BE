using Dapper;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Attendances.API.AttendanceHandler.RegisterFaceWithFiles;

/// <summary>
/// Command để đăng ký khuôn mặt guard với 6 file ảnh (form-data)
/// Xử lý tuần tự từng ảnh: validate → convert → upload
/// </summary>
public record RegisterFaceWithFilesCommand(
    Guid GuardId,
    IFormFile FrontImage,
    IFormFile LeftImage,
    IFormFile RightImage,
    IFormFile UpImage,
    IFormFile DownImage,
    IFormFile SmileImage
) : ICommand<RegisterFaceWithFilesResult>;

/// <summary>
/// Result chứa thông tin đăng ký khuôn mặt
/// </summary>
public record RegisterFaceWithFilesResult
{
    public bool Success { get; init; }
    public Guid? BiometricLogId { get; init; }
    public string? TemplateUrl { get; init; }
    public List<ImageProcessingStatus> ProcessingSteps { get; init; } = new();
    public List<float> QualityScores { get; init; } = new();
    public float AverageQuality { get; init; }
    public string? ErrorMessage { get; init; }
    public string Message { get; init; } = string.Empty;
}

/// <summary>
/// Trạng thái xử lý từng ảnh
/// </summary>
public record ImageProcessingStatus(
    string PoseType,
    string Status, // "processing", "completed", "failed"
    string? Message = null
);

/// <summary>
/// Face Registration API Models
/// </summary>
internal record ImageDataDto
{
    [JsonPropertyName("image_base64")]
    public string ImageBase64 { get; init; } = string.Empty;

    [JsonPropertyName("pose_type")]
    public string PoseType { get; init; } = string.Empty;

    [JsonPropertyName("angle")]
    public float Angle { get; init; }
}

internal record FaceRegistrationApiRequest
{
    [JsonPropertyName("guard_id")]
    public string GuardId { get; init; } = string.Empty;

    [JsonPropertyName("employee_code")]
    public string EmployeeCode { get; init; } = string.Empty;

    [JsonPropertyName("images")]
    public List<ImageDataDto> Images { get; init; } = new();
}

internal record FaceRegistrationApiResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; init; }

    [JsonPropertyName("guard_id")]
    public string GuardId { get; init; } = string.Empty;

    [JsonPropertyName("template_url")]
    public string? TemplateUrl { get; init; }

    [JsonPropertyName("quality_scores")]
    public List<float> QualityScores { get; init; } = new();

    [JsonPropertyName("average_quality")]
    public float AverageQuality { get; init; }

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;
}

/// <summary>
/// Handler xử lý đăng ký khuôn mặt với files (form-data)
/// Xử lý tuần tự: quay trái → quay phải → ngẩn → cúi → cười → hoàn tất
/// </summary>
internal class RegisterFaceWithFilesHandler(
    IDbConnectionFactory dbFactory,
    ILogger<RegisterFaceWithFilesHandler> logger,
    IConfiguration configuration,
    IHttpClientFactory httpClientFactory)
    : ICommandHandler<RegisterFaceWithFilesCommand, RegisterFaceWithFilesResult>
{
    private readonly string? _faceApiBaseUrl = configuration["FaceRecognitionApi__BaseUrl"]
                                              ?? configuration["FACEID_API_BASE_URL"];
    private readonly string? _faceApiKey = configuration["FaceRecognitionApi__ApiKey"];

    public async Task<RegisterFaceWithFilesResult> Handle(
        RegisterFaceWithFilesCommand request,
        CancellationToken cancellationToken)
    {
        var processingSteps = new List<ImageProcessingStatus>();

        try
        {
            logger.LogInformation(
                "🚀 Starting face registration for Guard={GuardId} with sequential processing",
                request.GuardId);

            // ================================================================
            // STEP 1: VALIDATE AND PROCESS IMAGES SEQUENTIALLY
            // ================================================================

            var imageFiles = new List<(IFormFile file, string poseType, float angle)>
            {
                (request.FrontImage, "front", 0),
                (request.LeftImage, "left", -45),
                (request.RightImage, "right", 45),
                (request.UpImage, "up", 20),
                (request.DownImage, "down", -20),
                (request.SmileImage, "smile", 0)
            };

            var processedImages = new List<ImageDataDto>();

            foreach (var (file, poseType, angle) in imageFiles)
            {
                processingSteps.Add(new ImageProcessingStatus(poseType, "processing", "Đang xử lý..."));

                logger.LogInformation(
                    "📸 Processing {PoseType} image: {FileName} ({Size} bytes)",
                    poseType,
                    file.FileName,
                    file.Length);

                // Validate file
                if (file == null || file.Length == 0)
                {
                    processingSteps[^1] = new ImageProcessingStatus(
                        poseType,
                        "failed",
                        $"File {poseType} không hợp lệ");

                    return new RegisterFaceWithFilesResult
                    {
                        Success = false,
                        ProcessingSteps = processingSteps,
                        ErrorMessage = $"Image {poseType} is invalid or empty"
                    };
                }

                // Validate file size (max 10MB)
                if (file.Length > 10 * 1024 * 1024)
                {
                    processingSteps[^1] = new ImageProcessingStatus(
                        poseType,
                        "failed",
                        "File quá lớn (max 10MB)");

                    return new RegisterFaceWithFilesResult
                    {
                        Success = false,
                        ProcessingSteps = processingSteps,
                        ErrorMessage = $"Image {poseType} is too large (max 10MB)"
                    };
                }

                // Validate file type
                var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png" };
                if (!allowedTypes.Contains(file.ContentType.ToLower()))
                {
                    processingSteps[^1] = new ImageProcessingStatus(
                        poseType,
                        "failed",
                        "Chỉ hỗ trợ JPG/PNG");

                    return new RegisterFaceWithFilesResult
                    {
                        Success = false,
                        ProcessingSteps = processingSteps,
                        ErrorMessage = $"Image {poseType} must be JPG or PNG"
                    };
                }

                // Convert to base64
                string base64Image;
                using (var memoryStream = new MemoryStream())
                {
                    await file.CopyToAsync(memoryStream, cancellationToken);
                    var imageBytes = memoryStream.ToArray();
                    base64Image = Convert.ToBase64String(imageBytes);
                }

                processedImages.Add(new ImageDataDto
                {
                    ImageBase64 = base64Image,
                    PoseType = poseType,
                    Angle = angle
                });

                processingSteps[^1] = new ImageProcessingStatus(
                    poseType,
                    "completed",
                    $"✓ Đã chuyển đổi ({file.Length / 1024}KB)");

                logger.LogInformation(
                    "✓ {PoseType} image processed successfully",
                    poseType);
            }

            // ================================================================
            // STEP 2: CALL FACE RECOGNITION API (Python)
            // Gửi tất cả 6 ảnh đã xử lý tới Python API
            // ================================================================

            if (string.IsNullOrWhiteSpace(_faceApiBaseUrl))
            {
                return new RegisterFaceWithFilesResult
                {
                    Success = false,
                    ProcessingSteps = processingSteps,
                    ErrorMessage = "Face Recognition API URL not configured"
                };
            }

            logger.LogInformation(
                "🔄 Sending all 6 images to Python Face Recognition API...");

            var registrationResult = await CallFaceRegistrationApiAsync(
                request.GuardId,
                processedImages,
                cancellationToken);

            if (registrationResult == null || !registrationResult.Success)
            {
                return new RegisterFaceWithFilesResult
                {
                    Success = false,
                    ProcessingSteps = processingSteps,
                    ErrorMessage = registrationResult?.Message ?? "Failed to call Face Recognition API"
                };
            }

            logger.LogInformation(
                "✓ Python API processed successfully. Template URL: {TemplateUrl}",
                registrationResult.TemplateUrl);

            // ================================================================
            // STEP 3: CREATE BIOMETRIC LOG
            // ================================================================

            var biometricLogId = await CreateBiometricLogAsync(
                request.GuardId,
                registrationResult.TemplateUrl,
                registrationResult.AverageQuality,
                cancellationToken);

            logger.LogInformation(
                "✅ Face registration completed for Guard={GuardId}, BiometricLogId={LogId}, AvgQuality={Quality}",
                request.GuardId,
                biometricLogId,
                registrationResult.AverageQuality);

            return new RegisterFaceWithFilesResult
            {
                Success = true,
                BiometricLogId = biometricLogId,
                TemplateUrl = registrationResult.TemplateUrl,
                ProcessingSteps = processingSteps,
                QualityScores = registrationResult.QualityScores,
                AverageQuality = registrationResult.AverageQuality,
                Message = "Face registered successfully with sequential processing"
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Error registering face for Guard={GuardId}", request.GuardId);

            return new RegisterFaceWithFilesResult
            {
                Success = false,
                ProcessingSteps = processingSteps,
                ErrorMessage = $"Failed to register face: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Call Python Face Recognition API
    /// </summary>
    private async Task<FaceRegistrationApiResponse?> CallFaceRegistrationApiAsync(
        Guid guardId,
        List<ImageDataDto> images,
        CancellationToken cancellationToken)
    {
        try
        {
            var httpClient = httpClientFactory.CreateClient();
            httpClient.BaseAddress = new Uri(_faceApiBaseUrl!);
            httpClient.Timeout = TimeSpan.FromSeconds(90); // Longer timeout for 6 images

            if (!string.IsNullOrWhiteSpace(_faceApiKey))
            {
                httpClient.DefaultRequestHeaders.Add("X-API-Key", _faceApiKey);
            }

            var requestBody = new FaceRegistrationApiRequest
            {
                GuardId = guardId.ToString(),
                EmployeeCode = guardId.ToString(),
                Images = images
            };

            var jsonContent = JsonSerializer.Serialize(requestBody);
            var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            logger.LogInformation(
                "Calling Face Registration API: {Url}",
                $"{_faceApiBaseUrl}/api/v1/faces/register");

            var response = await httpClient.PostAsync("/api/v1/faces/register", httpContent, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                logger.LogError(
                    "Face Registration API failed: {StatusCode}, {Error}",
                    response.StatusCode,
                    errorContent);
                return null;
            }

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<FaceRegistrationApiResponse>(responseContent);

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error calling Face Registration API");
            return null;
        }
    }

    /// <summary>
    /// Tạo BiometricLog
    /// </summary>
    private async Task<Guid> CreateBiometricLogAsync(
        Guid guardId,
        string? templateUrl,
        float averageQuality,
        CancellationToken cancellationToken)
    {
        try
        {
            using var connection = await dbFactory.CreateConnectionAsync();

            var biometricLogId = Guid.NewGuid();
            var now = DateTime.UtcNow;

            var insertSql = @"
                INSERT INTO biometric_logs (
                    Id, DeviceId, DeviceType, GuardId, BiometricUserId,
                    AuthenticationMethod, RegisteredFaceTemplateUrl,
                    FaceQualityScore, DeviceTimestamp, ReceivedAt,
                    EventType, IsVerified, VerificationStatus,
                    IsProcessed, ProcessingStatus,
                    CreatedAt
                ) VALUES (
                    @Id, @DeviceId, @DeviceType, @GuardId, @BiometricUserId,
                    @AuthenticationMethod, @RegisteredFaceTemplateUrl,
                    @FaceQualityScore, @DeviceTimestamp, @ReceivedAt,
                    @EventType, @IsVerified, @VerificationStatus,
                    @IsProcessed, @ProcessingStatus,
                    @CreatedAt
                )";

            await connection.ExecuteAsync(insertSql, new
            {
                Id = biometricLogId,
                DeviceId = "REGISTRATION_SYSTEM",
                DeviceType = "FACE_RECOGNITION",
                GuardId = guardId,
                BiometricUserId = guardId.ToString(),
                AuthenticationMethod = "FACE",
                RegisteredFaceTemplateUrl = templateUrl,
                FaceQualityScore = (decimal)averageQuality,
                DeviceTimestamp = now,
                ReceivedAt = now,
                EventType = "REGISTRATION",
                IsVerified = true,
                VerificationStatus = "SUCCESS",
                IsProcessed = true,
                ProcessingStatus = "COMPLETED",
                CreatedAt = now
            });

            return biometricLogId;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating BiometricLog");
            return Guid.Empty;
        }
    }
}
