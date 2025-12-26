namespace Attendances.API.AttendanceHandler.RegisterFaceWithFiles;

public record RegisterFaceWithFilesCommand(
    Guid GuardId,
    IFormFile FrontImage,
    IFormFile LeftImage,
    IFormFile RightImage,
    IFormFile UpImage,
    IFormFile DownImage,
    IFormFile SmileImage
) : ICommand<RegisterFaceWithFilesResult>;

public record ProcessingLog
{
    public string Timestamp { get; init; } = string.Empty;
    public string Step { get; init; } = string.Empty;
    public string? PoseType { get; init; }
    public string Status { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public Dictionary<string, object>? Details { get; init; }
}

public record RegisterFaceWithFilesResult
{
    public bool Success { get; init; }
    public Guid? BiometricLogId { get; init; }
    public string? TemplateUrl { get; init; }
    public List<ImageProcessingStatus> ProcessingSteps { get; init; } = new();
    public List<ProcessingLog> DetailedLogs { get; init; } = new();
    public List<float> QualityScores { get; init; } = new();
    public float AverageQuality { get; init; }
    public string? ErrorMessage { get; init; }
    public string Message { get; init; } = string.Empty;
}


public record ImageProcessingStatus(
    string PoseType,
    string Status,
    string? Message = null
);


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

    [JsonPropertyName("images")]
    public List<ImageDataDto> Images { get; init; } = new();
}

internal record ProcessingLogDto
{
    [JsonPropertyName("timestamp")]
    public string Timestamp { get; init; } = string.Empty;

    [JsonPropertyName("step")]
    public string Step { get; init; } = string.Empty;

    [JsonPropertyName("pose_type")]
    public string? PoseType { get; init; }

    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    [JsonPropertyName("details")]
    public Dictionary<string, object>? Details { get; init; }
}

internal record FaceRegistrationApiResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; init; }

    [JsonPropertyName("guard_id")]
    public string GuardId { get; init; } = string.Empty;

    [JsonPropertyName("template_url")]
    public string? TemplateUrl { get; init; }

    [JsonPropertyName("image_urls")]
    public Dictionary<string, string> ImageUrls { get; init; } = new();

    [JsonPropertyName("quality_scores")]
    public List<float> QualityScores { get; init; } = new();

    [JsonPropertyName("average_quality")]
    public float AverageQuality { get; init; }

    [JsonPropertyName("processing_logs")]
    public List<ProcessingLogDto> ProcessingLogs { get; init; } = new();

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;
}


internal class RegisterFaceWithFilesHandler(
    IDbConnectionFactory dbFactory,
    ILogger<RegisterFaceWithFilesHandler> logger,
    IConfiguration configuration,
    IHttpClientFactory httpClientFactory)
    : ICommandHandler<RegisterFaceWithFilesCommand, RegisterFaceWithFilesResult>
{
    private readonly string? _faceApiBaseUrl = configuration["FaceRecognitionApi:BaseUrl"]
                                              ?? configuration["FaceRecognitionApi__BaseUrl"]
                                              ?? configuration["FACEID_API_BASE_URL"];

    public async Task<RegisterFaceWithFilesResult> Handle(
        RegisterFaceWithFilesCommand request,
        CancellationToken cancellationToken)
    {
        var processingSteps = new List<ImageProcessingStatus>();

        try
        {
            logger.LogInformation(
                "Starting face registration for Guard={GuardId} with sequential processing",
                request.GuardId);

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
                    "Processing {PoseType} image: {FileName} ({Size} bytes)",
                    poseType,
                    file.FileName,
                    file.Length);
                
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
                "Sending all 6 images to Python Face Recognition API...");

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
                    DetailedLogs = registrationResult?.ProcessingLogs
                        .Select(log => new ProcessingLog
                        {
                            Timestamp = log.Timestamp,
                            Step = log.Step,
                            PoseType = log.PoseType,
                            Status = log.Status,
                            Message = log.Message,
                            Details = log.Details
                        })
                        .ToList() ?? new(),
                    ErrorMessage = registrationResult?.Message ?? "Failed to call Face Recognition API"
                };
            }

            logger.LogInformation(
                "Python API processed successfully. Template URL: {TemplateUrl}",
                registrationResult.TemplateUrl);
            
            var registeredFaceData = new
            {
                template_url = registrationResult.TemplateUrl,
                image_urls = registrationResult.ImageUrls
            };
            var registeredFaceDataJson = JsonSerializer.Serialize(registeredFaceData);

            var biometricLogId = await CreateOrUpdateBiometricLogAsync(
                request.GuardId,
                registeredFaceDataJson,
                registrationResult.AverageQuality,
                cancellationToken);

            logger.LogInformation(
                "Face registration completed for Guard={GuardId}, BiometricLogId={LogId}, AvgQuality={Quality}",
                request.GuardId,
                biometricLogId,
                registrationResult.AverageQuality);
            
            foreach (var log in registrationResult.ProcessingLogs)
            {
                var logLevel = log.Status.ToLower() switch
                {
                    "error" => LogLevel.Error,
                    "warning" => LogLevel.Warning,
                    "success" => LogLevel.Information,
                    _ => LogLevel.Debug
                };

                var logMessage = $"[{log.Status.ToUpper()}] [{log.Step}] {log.Message}";
                if (log.PoseType != null)
                {
                    logMessage += $" (pose: {log.PoseType})";
                }

                if (log.Details != null && log.Details.Count > 0)
                {
                    if (log.Step == "quality_assessment" && log.Details.ContainsKey("resolution"))
                    {
                        logMessage += $"\nResolution: {log.Details["resolution"]}, " +
                                     $"Confidence: {log.Details["confidence"]}%, " +
                                     $"Sharpness raw: {log.Details["sharpness_raw"]} (/{log.Details["sharpness_divisor"]}), " +
                                     $"Sharpness score: {log.Details["sharpness_score"]}, " +
                                     $"Brightness: {log.Details["brightness"]}, " +
                                     $"Brightness score: {log.Details["brightness_score"]}, " +
                                     $"Min threshold: {log.Details["min_threshold"]}";
                    }
                }

                logger.Log(logLevel, logMessage);
            }

            return new RegisterFaceWithFilesResult
            {
                Success = true,
                BiometricLogId = biometricLogId,
                TemplateUrl = registrationResult.TemplateUrl,
                ProcessingSteps = processingSteps,
                DetailedLogs = registrationResult.ProcessingLogs
                    .Select(log => new ProcessingLog
                    {
                        Timestamp = log.Timestamp,
                        Step = log.Step,
                        PoseType = log.PoseType,
                        Status = log.Status,
                        Message = log.Message,
                        Details = log.Details
                    })
                    .ToList(),
                QualityScores = registrationResult.QualityScores,
                AverageQuality = registrationResult.AverageQuality,
                Message = "Face registered successfully with sequential processing"
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error registering face for Guard={GuardId}", request.GuardId);

            return new RegisterFaceWithFilesResult
            {
                Success = false,
                ProcessingSteps = processingSteps,
                ErrorMessage = $"Failed to register face: {ex.Message}"
            };
        }
    }
    
    private async Task<FaceRegistrationApiResponse?> CallFaceRegistrationApiAsync(
        Guid guardId,
        List<ImageDataDto> images,
        CancellationToken cancellationToken)
    {
        try
        {
            var httpClient = httpClientFactory.CreateClient();
            httpClient.BaseAddress = new Uri(_faceApiBaseUrl!);
            httpClient.Timeout = TimeSpan.FromSeconds(90);

            var requestBody = new FaceRegistrationApiRequest
            {
                GuardId = guardId.ToString(),
                Images = images
            };

            var jsonContent = JsonSerializer.Serialize(requestBody);
            var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            logger.LogInformation(
                "Calling Face Registration API: {Url}",
                $"{_faceApiBaseUrl}/api/v1/faces/register");

            logger.LogInformation("Request JSON: {Json}", jsonContent);

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


    private async Task<Guid> CreateOrUpdateBiometricLogAsync(
        Guid guardId,
        string? registeredFaceDataJson,
        float averageQuality,
        CancellationToken cancellationToken)
    {
        try
        {
            using var connection = await dbFactory.CreateConnectionAsync();

            var now = DateTimeHelper.VietnamNow;
            
            var existingSql = @"
                SELECT Id
                FROM biometric_logs
                WHERE GuardId = @GuardId
                  AND EventType = 'REGISTRATION'
                LIMIT 1";

            var existingLogId = await connection.QueryFirstOrDefaultAsync<Guid?>(
                existingSql,
                new { GuardId = guardId });

            if (existingLogId.HasValue && existingLogId.Value != Guid.Empty)
            {
                logger.LogInformation(
                    "Updating existing BiometricLog for Guard={GuardId}, LogId={LogId}",
                    guardId,
                    existingLogId.Value);

                var updateSql = @"
                    UPDATE biometric_logs
                    SET RegisteredFaceTemplateUrl = @RegisteredFaceTemplateUrl,
                        FaceQualityScore = @FaceQualityScore,
                        DeviceTimestamp = @DeviceTimestamp,
                        ReceivedAt = @ReceivedAt,
                        IsVerified = @IsVerified,
                        VerificationStatus = @VerificationStatus,
                        IsProcessed = @IsProcessed,
                        ProcessingStatus = @ProcessingStatus,
                        UpdatedAt = @UpdatedAt
                    WHERE Id = @Id";

                await connection.ExecuteAsync(updateSql, new
                {
                    Id = existingLogId.Value,
                    RegisteredFaceTemplateUrl = registeredFaceDataJson,
                    FaceQualityScore = (decimal)averageQuality,
                    DeviceTimestamp = now,
                    ReceivedAt = now,
                    IsVerified = true,
                    VerificationStatus = "SUCCESS",
                    IsProcessed = true,
                    ProcessingStatus = "COMPLETED",
                    UpdatedAt = now
                });

                return existingLogId.Value;
            }
            else
            {
                logger.LogInformation(
                    "Creating new BiometricLog for Guard={GuardId}",
                    guardId);

                var biometricLogId = Guid.NewGuid();

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
                    RegisteredFaceTemplateUrl = registeredFaceDataJson,
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
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating/updating BiometricLog for Guard={GuardId}", guardId);
            return Guid.Empty;
        }
    }
}
