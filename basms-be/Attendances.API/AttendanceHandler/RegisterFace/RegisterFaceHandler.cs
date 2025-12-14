using Dapper;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Attendances.API.AttendanceHandler.RegisterFace;

/// <summary>
/// Command để đăng ký khuôn mặt guard với 6 ảnh từ các góc độ khác nhau
/// </summary>
public record RegisterFaceCommand(
    Guid GuardId,
    List<FaceImageData> Images
) : ICommand<RegisterFaceResult>;

/// <summary>
/// Data cho từng ảnh khuôn mặt
/// </summary>
public record FaceImageData(
    string ImageBase64,
    string PoseType, // front, left, right, up, down, smile
    float Angle = 0
);

/// <summary>
/// Result chứa thông tin đăng ký khuôn mặt
/// </summary>
public record RegisterFaceResult
{
    public bool Success { get; init; }
    public Guid? BiometricLogId { get; init; }
    public string? TemplateUrl { get; init; }
    public List<float> QualityScores { get; init; } = new();
    public float AverageQuality { get; init; }
    public string? ErrorMessage { get; init; }
    public string Message { get; init; } = string.Empty;
}

/// <summary>
/// Face Registration API Request/Response Models
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
/// Handler để đăng ký khuôn mặt guard qua Face Recognition API
/// Sau khi đăng ký thành công, tạo BiometricLog mới
/// </summary>
internal class RegisterFaceHandler(
    IDbConnectionFactory dbFactory,
    ILogger<RegisterFaceHandler> logger,
    IConfiguration configuration,
    IHttpClientFactory httpClientFactory)
    : ICommandHandler<RegisterFaceCommand, RegisterFaceResult>
{
    private readonly string? _faceApiBaseUrl = configuration["FaceRecognitionApi:BaseUrl"]
                                              ?? configuration["FaceRecognitionApi__BaseUrl"]
                                              ?? configuration["FACEID_API_BASE_URL"];
    private readonly string? _faceApiKey = configuration["FaceRecognitionApi:ApiKey"]
                                          ?? configuration["FaceRecognitionApi__ApiKey"]
                                          ?? configuration["FACEID_API_KEY"];

    public async Task<RegisterFaceResult> Handle(
        RegisterFaceCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation(
                "Registering face for Guard={GuardId}",
                request.GuardId);

            // ================================================================
            // VALIDATE INPUT
            // ================================================================

            if (request.Images.Count != 6)
            {
                return new RegisterFaceResult
                {
                    Success = false,
                    ErrorMessage = $"Expected 6 images (front, left, right, up, down, smile), got {request.Images.Count}"
                };
            }

            // Check required pose types
            var requiredPoses = new[] { "front", "left", "right", "up", "down", "smile" };
            var providedPoses = request.Images.Select(i => i.PoseType.ToLower()).ToList();

            foreach (var pose in requiredPoses)
            {
                if (!providedPoses.Contains(pose))
                {
                    return new RegisterFaceResult
                    {
                        Success = false,
                        ErrorMessage = $"Missing required pose: {pose}"
                    };
                }
            }

            // ================================================================
            // CALL FACE RECOGNITION API (Python)
            // Python sẽ xử lý 6 ảnh, lưu lên S3, và trả về template URL
            // ================================================================

            if (string.IsNullOrWhiteSpace(_faceApiBaseUrl))
            {
                return new RegisterFaceResult
                {
                    Success = false,
                    ErrorMessage = "Face Recognition API URL not configured"
                };
            }

            var registrationResult = await CallFaceRegistrationApiAsync(
                request.GuardId,
                request.Images,
                cancellationToken);

            if (registrationResult == null || !registrationResult.Success)
            {
                return new RegisterFaceResult
                {
                    Success = false,
                    ErrorMessage = registrationResult?.Message ?? "Failed to call Face Recognition API"
                };
            }

            // ================================================================
            // CREATE BIOMETRIC LOG
            // Lưu thông tin đăng ký khuôn mặt vào BiometricLogs
            // ================================================================

            var biometricLogId = await CreateBiometricLogAsync(
                request.GuardId,
                registrationResult.TemplateUrl,
                registrationResult.AverageQuality,
                cancellationToken);

            logger.LogInformation(
                "✓ Face registered successfully for Guard={GuardId}, TemplateUrl={TemplateUrl}, AvgQuality={Quality}, BiometricLogId={LogId}",
                request.GuardId,
                registrationResult.TemplateUrl,
                registrationResult.AverageQuality,
                biometricLogId);

            return new RegisterFaceResult
            {
                Success = true,
                BiometricLogId = biometricLogId,
                TemplateUrl = registrationResult.TemplateUrl,
                QualityScores = registrationResult.QualityScores,
                AverageQuality = registrationResult.AverageQuality,
                Message = "Face registered successfully and biometric log created"
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error registering face for Guard={GuardId}", request.GuardId);

            return new RegisterFaceResult
            {
                Success = false,
                ErrorMessage = $"Failed to register face: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Call Face Recognition API để đăng ký khuôn mặt
    /// Python API sẽ xử lý 6 ảnh, lưu lên S3, và trả về template URL
    /// </summary>
    private async Task<FaceRegistrationApiResponse?> CallFaceRegistrationApiAsync(
        Guid guardId,
        List<FaceImageData> images,
        CancellationToken cancellationToken)
    {
        try
        {
            var httpClient = httpClientFactory.CreateClient();
            httpClient.BaseAddress = new Uri(_faceApiBaseUrl!);
            httpClient.Timeout = TimeSpan.FromSeconds(60); // Registration takes longer

            // Add API Key if configured
            if (!string.IsNullOrWhiteSpace(_faceApiKey))
            {
                httpClient.DefaultRequestHeaders.Add("X-API-Key", _faceApiKey);
            }

            var requestBody = new FaceRegistrationApiRequest
            {
                GuardId = guardId.ToString(),
                EmployeeCode = guardId.ToString(), // Use GuardId as employee code
                Images = images.Select(img => new ImageDataDto
                {
                    ImageBase64 = img.ImageBase64,
                    PoseType = img.PoseType.ToLower(),
                    Angle = img.Angle
                }).ToList()
            };

            var jsonContent = JsonSerializer.Serialize(requestBody);
            var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            logger.LogInformation(
                "Calling Face Registration API: {Url} with {ImageCount} images",
                $"{_faceApiBaseUrl}/api/v1/faces/register",
                images.Count);

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

            logger.LogInformation(
                "Face Registration API response: Success={Success}, TemplateUrl={TemplateUrl}, AvgQuality={Quality}",
                result?.Success,
                result?.TemplateUrl,
                result?.AverageQuality);

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error calling Face Registration API");
            return null;
        }
    }

    /// <summary>
    /// Tạo BiometricLog mới sau khi đăng ký khuôn mặt thành công
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

            logger.LogInformation(
                "✓ Created BiometricLog: {BiometricLogId} for Guard={GuardId}",
                biometricLogId,
                guardId);

            return biometricLogId;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating BiometricLog for Guard={GuardId}", guardId);
            return Guid.Empty;
        }
    }
}
