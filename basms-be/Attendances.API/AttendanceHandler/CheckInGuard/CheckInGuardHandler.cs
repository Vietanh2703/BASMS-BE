using System.Text;
using System.Text.Json.Serialization;
using Attendances.API.Helpers;

namespace Attendances.API.AttendanceHandler.CheckInGuard;

/// <summary>
/// Command để check-in guard với face verification
/// </summary>
public record CheckInGuardCommand(
    Guid GuardId,
    Guid ShiftAssignmentId,
    Guid ShiftId,
    IFormFile CheckInImage,
    double CheckInLatitude,
    double CheckInLongitude,
    float? CheckInLocationAccuracy
) : ICommand<CheckInGuardResult>;

/// <summary>
/// Result của check-in operation
/// </summary>
public record CheckInGuardResult
{
    public bool Success { get; init; }
    public Guid? AttendanceRecordId { get; init; }
    public DateTime? CheckInTime { get; init; }
    public bool IsLate { get; init; }
    public int LateMinutes { get; init; }
    public float FaceMatchScore { get; init; }
    public double DistanceFromSite { get; init; }
    public string? CheckInImageUrl { get; init; }
    public string? ErrorMessage { get; init; }
    public string Message { get; init; } = string.Empty;
}

/// <summary>
/// Face Verification API Models
/// </summary>
internal record FaceVerificationApiRequest
{
    [JsonPropertyName("guard_id")]
    public string GuardId { get; init; } = string.Empty;

    [JsonPropertyName("check_image_base64")]
    public string CheckImageBase64 { get; init; } = string.Empty;

    [JsonPropertyName("template_url")]
    public string? TemplateUrl { get; init; }
}

internal record FaceVerificationWithImagesApiRequest
{
    [JsonPropertyName("guard_id")]
    public string GuardId { get; init; } = string.Empty;

    [JsonPropertyName("check_image_base64")]
    public string CheckImageBase64 { get; init; } = string.Empty;

    [JsonPropertyName("registered_image_urls")]
    public Dictionary<string, string> RegisteredImageUrls { get; init; } = new();
}

internal record ImageVerificationResultDto
{
    [JsonPropertyName("pose_type")]
    public string PoseType { get; init; } = string.Empty;

    [JsonPropertyName("is_match")]
    public bool IsMatch { get; init; }

    [JsonPropertyName("confidence")]
    public float Confidence { get; init; }

    [JsonPropertyName("quality_score")]
    public float QualityScore { get; init; }
}

internal record FaceVerificationApiResponse
{
    [JsonPropertyName("is_match")]
    public bool IsMatch { get; init; }

    [JsonPropertyName("confidence")]
    public float Confidence { get; init; }

    [JsonPropertyName("face_detected")]
    public bool FaceDetected { get; init; }

    [JsonPropertyName("face_quality")]
    public float FaceQuality { get; init; }

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;
}

internal record FaceVerificationWithImagesApiResponse
{
    [JsonPropertyName("is_match")]
    public bool IsMatch { get; init; }

    [JsonPropertyName("confidence")]
    public float Confidence { get; init; }

    [JsonPropertyName("best_match_pose")]
    public string? BestMatchPose { get; init; }

    [JsonPropertyName("face_detected")]
    public bool FaceDetected { get; init; }

    [JsonPropertyName("face_quality")]
    public float FaceQuality { get; init; }

    [JsonPropertyName("individual_results")]
    public List<ImageVerificationResultDto> IndividualResults { get; init; } = new();

    [JsonPropertyName("model_version")]
    public string ModelVersion { get; init; } = string.Empty;

    [JsonPropertyName("processing_time_ms")]
    public float ProcessingTimeMs { get; init; }

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;
}

internal record RegisteredFaceData
{
    [JsonPropertyName("template_url")]
    public string? TemplateUrl { get; init; }

    [JsonPropertyName("image_urls")]
    public Dictionary<string, string> ImageUrls { get; init; } = new();
}

/// <summary>
/// Shift Location Info từ Shifts.API
/// </summary>
internal record ShiftLocationInfo
{
    public double LocationLatitude { get; init; }
    public double LocationLongitude { get; init; }
    public DateTime ScheduledStartTime { get; init; }
}

/// <summary>
/// Handler xử lý check-in guard với face verification và location validation
/// </summary>
internal class CheckInGuardHandler(
    IDbConnectionFactory dbFactory,
    ILogger<CheckInGuardHandler> logger,
    IConfiguration configuration,
    IHttpClientFactory httpClientFactory,
    IAmazonS3 s3Client,
    IPublishEndpoint publishEndpoint)
    : ICommandHandler<CheckInGuardCommand, CheckInGuardResult>
{
    private readonly string? _faceApiBaseUrl = configuration["FaceRecognitionApi:BaseUrl"]
                                              ?? configuration["FaceRecognitionApi__BaseUrl"]
                                              ?? configuration["FACEID_API_BASE_URL"];

    private readonly string? _shiftsApiBaseUrl = configuration["ShiftsApi:BaseUrl"]
                                                ?? configuration["ShiftsApi__BaseUrl"];

    private readonly string _s3BucketName = configuration["AWS:S3:BucketName"]
                                           ?? configuration["AWS__S3__BucketName"]
                                           ?? "basms-faces";

    private const double MaxDistanceMeters = 200.0;
    private const float MinFaceMatchScore = 70.0f;

    public async Task<CheckInGuardResult> Handle(
        CheckInGuardCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation(
                "🚀 Starting check-in for Guard={GuardId}, ShiftAssignment={ShiftAssignmentId}, Shift={ShiftId}",
                request.GuardId, request.ShiftAssignmentId, request.ShiftId);

            // ================================================================
            // STEP 1: VALIDATE ATTENDANCE RECORD EXISTS
            // ================================================================
            using var connection = await dbFactory.CreateConnectionAsync();

            var attendanceRecord = await GetAttendanceRecordAsync(
                connection,
                request.GuardId,
                request.ShiftAssignmentId,
                request.ShiftId,
                cancellationToken);

            if (attendanceRecord == null)
            {
                logger.LogWarning(
                    "❌ Attendance record not found - Guard={GuardId}, ShiftAssignment={ShiftAssignmentId}, Shift={ShiftId}",
                    request.GuardId, request.ShiftAssignmentId, request.ShiftId);

                return new CheckInGuardResult
                {
                    Success = false,
                    ErrorMessage = "❌ Attendance record không tồn tại. Vui lòng kiểm tra lại thông tin ca làm việc."
                };
            }

            // Validate attendance record status
            if (attendanceRecord.Status == "CHECKED_IN")
            {
                logger.LogWarning(
                    "❌ Guard already checked in - AttendanceRecord={RecordId}",
                    attendanceRecord.Id);

                return new CheckInGuardResult
                {
                    Success = false,
                    ErrorMessage = "❌ Bạn đã check-in cho ca làm việc này rồi."
                };
            }

            if (attendanceRecord.Status != "PENDING")
            {
                logger.LogWarning(
                    "❌ Invalid attendance record status - Status={Status}, Expected=PENDING",
                    attendanceRecord.Status);

                return new CheckInGuardResult
                {
                    Success = false,
                    ErrorMessage = $"❌ Trạng thái attendance record không hợp lệ: {attendanceRecord.Status}"
                };
            }

            logger.LogInformation("✓ Found AttendanceRecord={RecordId}, Status={Status}",
                attendanceRecord.Id, attendanceRecord.Status);

            // ================================================================
            // STEP 2: VALIDATE REGISTERED FACE TEMPLATE EXISTS
            // ================================================================
            var registeredFaceDataJson = await GetRegisteredFaceTemplateUrlAsync(
                connection,
                request.GuardId,
                cancellationToken);

            if (string.IsNullOrWhiteSpace(registeredFaceDataJson))
            {
                logger.LogWarning(
                    "❌ No registered face template found for Guard={GuardId}",
                    request.GuardId);

                return new CheckInGuardResult
                {
                    Success = false,
                    ErrorMessage = "❌ Chưa đăng ký khuôn mặt. Vui lòng đăng ký khuôn mặt trước khi check-in."
                };
            }

            logger.LogInformation("✓ Found face data: {Data}", registeredFaceDataJson.Length > 100 ? registeredFaceDataJson.Substring(0, 100) + "..." : registeredFaceDataJson);

            // ================================================================
            // STEP 3: VALIDATE FACE WITH PYTHON API
            // ================================================================
            logger.LogInformation("🔍 Verifying face with Python API...");

            var faceVerificationResult = await VerifyFaceAsync(
                request.GuardId,
                request.CheckInImage,
                registeredFaceDataJson,
                cancellationToken);

            if (faceVerificationResult == null)
            {
                logger.LogError(
                    "❌ Face verification failed - API returned null response. " +
                    "Check logs above for specific error (config issue, network error, or API failure)");

                return new CheckInGuardResult
                {
                    Success = false,
                    ErrorMessage = "❌ Không thể xác minh khuôn mặt. Vui lòng kiểm tra: " +
                                 "(1) Kết nối mạng, (2) Chất lượng ảnh đăng ký, (3) Liên hệ quản trị viên nếu vấn đề tiếp diễn."
                };
            }

            if (!faceVerificationResult.FaceDetected)
            {
                logger.LogWarning(
                    "❌ No face detected in check-in image - Guard={GuardId}",
                    request.GuardId);

                return new CheckInGuardResult
                {
                    Success = false,
                    FaceMatchScore = 0,
                    ErrorMessage = "❌ Không phát hiện khuôn mặt trong ảnh. Vui lòng chụp ảnh rõ ràng hơn."
                };
            }

            if (faceVerificationResult.FaceQuality < 50)
            {
                logger.LogWarning(
                    "❌ Low face quality detected - Quality={Quality}",
                    faceVerificationResult.FaceQuality);

                return new CheckInGuardResult
                {
                    Success = false,
                    FaceMatchScore = faceVerificationResult.Confidence,
                    ErrorMessage = $"❌ Chất lượng ảnh khuôn mặt thấp ({faceVerificationResult.FaceQuality:F1}/100). Vui lòng chụp ảnh trong điều kiện sáng tốt hơn."
                };
            }

            if (!faceVerificationResult.IsMatch || faceVerificationResult.Confidence < MinFaceMatchScore)
            {
                logger.LogWarning(
                    "❌ Face verification failed - IsMatch={IsMatch}, Confidence={Confidence}, Required={Required}",
                    faceVerificationResult.IsMatch,
                    faceVerificationResult.Confidence,
                    MinFaceMatchScore);

                return new CheckInGuardResult
                {
                    Success = false,
                    FaceMatchScore = faceVerificationResult.Confidence,
                    ErrorMessage = $"❌ Khuôn mặt không khớp với dữ liệu đã đăng ký. Độ chính xác: {faceVerificationResult.Confidence:F1}% (yêu cầu >= {MinFaceMatchScore}%)"
                };
            }

            logger.LogInformation(
                "✓ Face verified successfully - Confidence={Confidence}%, Quality={Quality}%",
                faceVerificationResult.Confidence,
                faceVerificationResult.FaceQuality);

            // ================================================================
            // STEP 4: UPLOAD CHECK-IN IMAGE TO S3
            // ================================================================
            logger.LogInformation("📤 Uploading check-in image to S3...");

            var checkInImageUrl = await UploadCheckInImageToS3Async(
                request.GuardId,
                request.ShiftId,
                request.CheckInImage,
                cancellationToken);

            logger.LogInformation("✓ Image uploaded: {ImageUrl}", checkInImageUrl);

            // ================================================================
            // STEP 5: GET SHIFT LOCATION INFO FROM SHIFTS.API
            // ================================================================
            logger.LogInformation("📍 Getting shift location from Shifts.API...");

            var shiftLocation = await GetShiftLocationAsync(
                request.ShiftId,
                cancellationToken);

            if (shiftLocation == null)
            {
                return new CheckInGuardResult
                {
                    Success = false,
                    ErrorMessage = "Failed to retrieve shift location information"
                };
            }

            logger.LogInformation(
                "✓ Shift location: Lat={Lat}, Lon={Lon}",
                shiftLocation.LocationLatitude,
                shiftLocation.LocationLongitude);

            // ================================================================
            // STEP 6: VALIDATE LOCATION DISTANCE
            // ================================================================
            var distanceFromSite = GeoLocationHelper.CalculateDistanceInMeters(
                request.CheckInLatitude,
                request.CheckInLongitude,
                shiftLocation.LocationLatitude,
                shiftLocation.LocationLongitude);

            logger.LogInformation(
                "📏 Distance from site: {Distance:F2}m (max: {MaxDistance}m)",
                distanceFromSite,
                MaxDistanceMeters);

            if (distanceFromSite > MaxDistanceMeters)
            {
                logger.LogWarning(
                    "❌ Guard too far from site - Distance={Distance:F0}m, Max={MaxDistance}m",
                    distanceFromSite,
                    MaxDistanceMeters);

                return new CheckInGuardResult
                {
                    Success = false,
                    DistanceFromSite = distanceFromSite,
                    FaceMatchScore = faceVerificationResult.Confidence,
                    ErrorMessage = $"❌ Quá xa công trường. Khoảng cách hiện tại: {distanceFromSite:F0}m (tối đa cho phép: {MaxDistanceMeters}m)"
                };
            }

            logger.LogInformation("✓ Location validated - Distance within acceptable range");

            // ================================================================
            // STEP 7: CALCULATE LATE STATUS
            // ================================================================
            var checkInTime = DateTimeHelper.VietnamNow;
            var scheduledStartTime = shiftLocation.ScheduledStartTime;

            var isLate = checkInTime > scheduledStartTime;
            var lateMinutes = isLate
                ? (int)Math.Ceiling((checkInTime - scheduledStartTime).TotalMinutes)
                : 0;

            if (isLate)
            {
                logger.LogWarning(
                    "⏰ Guard is late by {LateMinutes} minutes",
                    lateMinutes);
            }
            else
            {
                logger.LogInformation("✓ Guard is on time");
            }

            // ================================================================
            // STEP 8: UPDATE ATTENDANCE RECORD
            // ================================================================
            logger.LogInformation("💾 Updating attendance record...");

            await UpdateAttendanceRecordAsync(
                connection,
                attendanceRecord.Id,
                checkInTime,
                request.CheckInLatitude,
                request.CheckInLongitude,
                request.CheckInLocationAccuracy,
                checkInImageUrl,
                faceVerificationResult.Confidence,
                distanceFromSite,
                isLate,
                lateMinutes,
                cancellationToken);

            logger.LogInformation("✓ Attendance record updated");

            // ================================================================
            // STEP 9: PUBLISH EVENTS TO UPDATE SHIFTS.API
            // ================================================================
            logger.LogInformation("📨 Publishing integration events...");

            await PublishShiftAssignmentUpdateAsync(
                request.ShiftAssignmentId,
                request.ShiftId,
                request.GuardId,
                checkInTime,
                isLate,
                lateMinutes,
                faceVerificationResult.Confidence,
                distanceFromSite,
                cancellationToken);

            logger.LogInformation("✓ GuardCheckedInEvent published successfully");

            // ================================================================
            // STEP 10: RETURN SUCCESS RESULT
            // ================================================================
            logger.LogInformation(
                "✅ Check-in completed successfully for Guard={GuardId}",
                request.GuardId);

            return new CheckInGuardResult
            {
                Success = true,
                AttendanceRecordId = attendanceRecord.Id,
                CheckInTime = checkInTime,
                IsLate = isLate,
                LateMinutes = lateMinutes,
                FaceMatchScore = faceVerificationResult.Confidence,
                DistanceFromSite = distanceFromSite,
                CheckInImageUrl = checkInImageUrl,
                Message = "Check-in completed successfully"
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Error during check-in for Guard={GuardId}", request.GuardId);

            return new CheckInGuardResult
            {
                Success = false,
                ErrorMessage = $"Check-in failed: {ex.Message}"
            };
        }
    }

    private async Task<AttendanceRecordDto?> GetAttendanceRecordAsync(
        IDbConnection connection,
        Guid guardId,
        Guid shiftAssignmentId,
        Guid shiftId,
        CancellationToken cancellationToken)
    {
        var sql = @"
            SELECT Id, GuardId, ShiftAssignmentId, ShiftId, ScheduledStartTime, Status
            FROM attendance_records
            WHERE GuardId = @GuardId
              AND ShiftAssignmentId = @ShiftAssignmentId
              AND ShiftId = @ShiftId
            LIMIT 1";

        return await connection.QueryFirstOrDefaultAsync<AttendanceRecordDto>(
            sql,
            new { GuardId = guardId, ShiftAssignmentId = shiftAssignmentId, ShiftId = shiftId });
    }

    private async Task<string?> GetRegisteredFaceTemplateUrlAsync(
        IDbConnection connection,
        Guid guardId,
        CancellationToken cancellationToken)
    {
        var sql = @"
            SELECT RegisteredFaceTemplateUrl
            FROM biometric_logs
            WHERE GuardId = @GuardId
              AND EventType = 'REGISTRATION'
              AND IsVerified = true
              AND RegisteredFaceTemplateUrl IS NOT NULL
            ORDER BY CreatedAt DESC
            LIMIT 1";

        return await connection.QueryFirstOrDefaultAsync<string>(sql, new { GuardId = guardId });
    }

    private async Task<FaceVerificationApiResponse?> VerifyFaceAsync(
        Guid guardId,
        IFormFile checkInImage,
        string registeredFaceDataJson,
        CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_faceApiBaseUrl))
            {
                logger.LogError(
                    "❌ Face Recognition API URL not configured. Please set one of these environment variables: " +
                    "FaceRecognitionApi:BaseUrl, FaceRecognitionApi__BaseUrl, or FACEID_API_BASE_URL");
                return null;
            }

            logger.LogInformation("Using Face API URL: {Url}", _faceApiBaseUrl);

            // Convert check-in image to base64
            string base64Image;
            using (var memoryStream = new MemoryStream())
            {
                await checkInImage.CopyToAsync(memoryStream, cancellationToken);
                var imageBytes = memoryStream.ToArray();
                base64Image = Convert.ToBase64String(imageBytes);
            }

            // Parse JSON to get image URLs
            // Support both old format (simple template URL) and new format (JSON with image URLs)
            RegisteredFaceData? registeredFaceData = null;

            // Check if it's JSON format
            if (registeredFaceDataJson.TrimStart().StartsWith("{"))
            {
                try
                {
                    var jsonOptions = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };
                    registeredFaceData = JsonSerializer.Deserialize<RegisteredFaceData>(registeredFaceDataJson, jsonOptions);
                }
                catch (JsonException ex)
                {
                    logger.LogError(
                        ex,
                        "❌ Failed to parse RegisteredFaceTemplateUrl JSON. JSON length: {Length}, Start: {Start}",
                        registeredFaceDataJson.Length,
                        registeredFaceDataJson.Length > 100 ? registeredFaceDataJson.Substring(0, 100) : registeredFaceDataJson);
                    return null;
                }
                catch (Exception ex)
                {
                    logger.LogError(
                        ex,
                        "❌ Unexpected error parsing RegisteredFaceTemplateUrl JSON: {Message}",
                        ex.Message);
                    return null;
                }
            }
            else
            {
                // Old format - simple template URL
                logger.LogWarning(
                    "Guard {GuardId} using old registration format (template URL only). Please re-register for multi-image verification.",
                    guardId);

                // Fallback to old verification endpoint
                return await VerifyFaceWithTemplateAsync(
                    guardId,
                    checkInImage,
                    registeredFaceDataJson,
                    cancellationToken);
            }

            if (registeredFaceData == null || registeredFaceData.ImageUrls == null || !registeredFaceData.ImageUrls.Any())
            {
                logger.LogError(
                    "❌ No registered images found in RegisteredFaceTemplateUrl. Data: {Data}",
                    registeredFaceData == null ? "NULL" :
                    registeredFaceData.ImageUrls == null ? "ImageUrls is NULL" :
                    $"ImageUrls count: {registeredFaceData.ImageUrls.Count}");
                return null;
            }

            logger.LogInformation(
                "✓ Found {Count} registered face images: {PoseTypes}",
                registeredFaceData.ImageUrls.Count,
                string.Join(", ", registeredFaceData.ImageUrls.Keys));

            // Generate presigned URLs for registered images from S3
            var presignedUrls = new Dictionary<string, string>();
            foreach (var (poseType, s3Url) in registeredFaceData.ImageUrls)
            {
                // Parse s3://bucket/key format
                if (!s3Url.StartsWith("s3://"))
                {
                    logger.LogWarning("Invalid S3 URL format for {PoseType}: {Url}", poseType, s3Url);
                    continue;
                }

                var parts = s3Url.Replace("s3://", "").Split('/', 2);
                if (parts.Length != 2)
                {
                    logger.LogWarning("Cannot parse S3 URL for {PoseType}: {Url}", poseType, s3Url);
                    continue;
                }

                var bucketName = parts[0];
                var key = parts[1];

                // Generate presigned URL (valid for 15 minutes - enough for verification)
                var urlRequest = new GetPreSignedUrlRequest
                {
                    BucketName = bucketName,
                    Key = key,
                    Expires = DateTime.UtcNow.AddMinutes(15)
                };

                var presignedUrl = s3Client.GetPreSignedURL(urlRequest);
                presignedUrls[poseType] = presignedUrl;

                logger.LogInformation("Generated presigned URL for {PoseType}: {Key}", poseType, key);
            }

            if (!presignedUrls.Any())
            {
                logger.LogError(
                    "❌ Failed to generate any presigned URLs for registered images. " +
                    "Original images count: {OriginalCount}",
                    registeredFaceData.ImageUrls.Count);
                return null;
            }

            logger.LogInformation(
                "✓ Generated {Count} presigned URLs for face verification",
                presignedUrls.Count);

            // Call new verification endpoint with multiple images
            var httpClient = httpClientFactory.CreateClient();
            httpClient.BaseAddress = new Uri(_faceApiBaseUrl);
            httpClient.Timeout = TimeSpan.FromSeconds(60); // Longer timeout for multi-image verification

            var requestBody = new FaceVerificationWithImagesApiRequest
            {
                GuardId = guardId.ToString(),
                CheckImageBase64 = base64Image,
                RegisteredImageUrls = presignedUrls
            };

            var jsonContent = JsonSerializer.Serialize(requestBody);
            var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            logger.LogInformation(
                "Calling Face Verification API with {Count} registered images: {Url}",
                presignedUrls.Count,
                $"{_faceApiBaseUrl}/api/v1/faces/verify-with-images");

            var response = await httpClient.PostAsync("/api/v1/faces/verify-with-images", httpContent, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                logger.LogError(
                    "❌ Face Verification API failed - StatusCode={StatusCode}, Error={Error}",
                    response.StatusCode,
                    errorContent);
                return null;
            }

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogInformation("Face API Response: {Response}", responseContent);

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            var detailedResult = JsonSerializer.Deserialize<FaceVerificationWithImagesApiResponse>(responseContent, options);

            if (detailedResult == null)
            {
                logger.LogError(
                    "❌ Failed to deserialize verification response. Response: {Response}",
                    responseContent);
                return null;
            }

            // Log detailed results
            logger.LogInformation(
                "Verification results: IsMatch={IsMatch}, BestConfidence={Confidence}%, BestPose={BestPose}, ProcessingTime={Time}ms",
                detailedResult.IsMatch,
                detailedResult.Confidence,
                detailedResult.BestMatchPose,
                detailedResult.ProcessingTimeMs);

            foreach (var individualResult in detailedResult.IndividualResults)
            {
                logger.LogInformation(
                    "  - {PoseType}: Match={IsMatch}, Confidence={Confidence}%, Quality={Quality}%",
                    individualResult.PoseType,
                    individualResult.IsMatch,
                    individualResult.Confidence,
                    individualResult.QualityScore);
            }

            // Convert to simple response format for backward compatibility
            return new FaceVerificationApiResponse
            {
                IsMatch = detailedResult.IsMatch,
                Confidence = detailedResult.Confidence,
                FaceDetected = detailedResult.FaceDetected,
                FaceQuality = detailedResult.FaceQuality,
                Message = detailedResult.Message
            };
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(
                ex,
                "❌ HTTP error when calling Face Verification API at {Url}. Check if the service is running and accessible.",
                _faceApiBaseUrl);
            return null;
        }
        catch (TaskCanceledException ex)
        {
            logger.LogError(
                ex,
                "❌ Face Verification API request timeout after {Timeout} seconds",
                60);
            return null;
        }
        catch (JsonException ex)
        {
            logger.LogError(
                ex,
                "❌ Failed to parse JSON when calling Face Verification API");
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "❌ Unexpected error when calling Face Verification API: {Message}",
                ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Fallback method for old registration format (template URL only)
    /// </summary>
    private async Task<FaceVerificationApiResponse?> VerifyFaceWithTemplateAsync(
        Guid guardId,
        IFormFile checkInImage,
        string templateUrl,
        CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_faceApiBaseUrl))
            {
                logger.LogError("Face Recognition API URL not configured");
                return null;
            }

            // Convert image to base64
            string base64Image;
            using (var memoryStream = new MemoryStream())
            {
                await checkInImage.CopyToAsync(memoryStream, cancellationToken);
                var imageBytes = memoryStream.ToArray();
                base64Image = Convert.ToBase64String(imageBytes);
            }

            // Generate presigned URL if template URL is S3 URI
            string? presignedTemplateUrl = templateUrl;
            if (templateUrl.StartsWith("s3://"))
            {
                logger.LogInformation("Converting S3 URI to presigned URL: {S3Uri}", templateUrl);

                var parts = templateUrl.Replace("s3://", "").Split('/', 2);
                if (parts.Length == 2)
                {
                    var bucketName = parts[0];
                    var key = parts[1];

                    var urlRequest = new GetPreSignedUrlRequest
                    {
                        BucketName = bucketName,
                        Key = key,
                        Expires = DateTime.UtcNow.AddMinutes(15)
                    };

                    presignedTemplateUrl = s3Client.GetPreSignedURL(urlRequest);
                    logger.LogInformation("Generated presigned URL for template");
                }
                else
                {
                    logger.LogWarning("Invalid S3 URI format for template URL: {TemplateUrl}", templateUrl);
                }
            }

            var httpClient = httpClientFactory.CreateClient();
            httpClient.BaseAddress = new Uri(_faceApiBaseUrl);
            httpClient.Timeout = TimeSpan.FromSeconds(30);

            var requestBody = new FaceVerificationApiRequest
            {
                GuardId = guardId.ToString(),
                CheckImageBase64 = base64Image,
                TemplateUrl = presignedTemplateUrl
            };

            var jsonContent = JsonSerializer.Serialize(requestBody);
            var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            logger.LogInformation(
                "Calling old Face Verification API (template-based): {Url}",
                $"{_faceApiBaseUrl}/api/v1/faces/verify");
            logger.LogInformation(
                "Request body: GuardId={GuardId}, TemplateUrl={TemplateUrl} (Original: {Original}), ImageSize={ImageSize}KB",
                guardId,
                presignedTemplateUrl == templateUrl ? templateUrl : "presigned URL",
                templateUrl,
                base64Image.Length / 1024);

            var response = await httpClient.PostAsync("/api/v1/faces/verify", httpContent, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                logger.LogError(
                    "❌ Face Verification API (template-based) failed - StatusCode={StatusCode}, Error={Error}",
                    response.StatusCode,
                    errorContent);
                return null;
            }

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogInformation("Face API Response (template-based): {Response}", responseContent);

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            var result = JsonSerializer.Deserialize<FaceVerificationApiResponse>(responseContent, options);

            if (result == null)
            {
                logger.LogError(
                    "❌ Failed to deserialize verification response (template-based). Response: {Response}",
                    responseContent);
            }

            return result;
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(
                ex,
                "❌ HTTP error when calling old Face Verification API at {Url}. Check if the service is running and accessible.",
                _faceApiBaseUrl);
            return null;
        }
        catch (TaskCanceledException ex)
        {
            logger.LogError(
                ex,
                "❌ Face Verification API (template-based) request timeout after {Timeout} seconds",
                30);
            return null;
        }
        catch (JsonException ex)
        {
            logger.LogError(
                ex,
                "❌ Failed to parse JSON when calling old Face Verification API");
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "❌ Unexpected error when calling old Face Verification API: {Message}",
                ex.Message);
            return null;
        }
    }

    private async Task<string> UploadCheckInImageToS3Async(
        Guid guardId,
        Guid shiftId,
        IFormFile image,
        CancellationToken cancellationToken)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var key = $"check-in/{guardId}/{shiftId}/{timestamp}.jpg";

        using var stream = image.OpenReadStream();

        var putRequest = new PutObjectRequest
        {
            BucketName = _s3BucketName,
            Key = key,
            InputStream = stream,
            ContentType = "image/jpeg",
            CannedACL = S3CannedACL.Private
        };

        await s3Client.PutObjectAsync(putRequest, cancellationToken);

        // Generate pre-signed URL (valid for 7 days)
        var urlRequest = new GetPreSignedUrlRequest
        {
            BucketName = _s3BucketName,
            Key = key,
            Expires = DateTime.UtcNow.AddDays(7)
        };

        return s3Client.GetPreSignedURL(urlRequest);
    }

    private async Task<ShiftLocationInfo?> GetShiftLocationAsync(
        Guid shiftId,
        CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_shiftsApiBaseUrl))
            {
                logger.LogError("Shifts API URL not configured");
                return null;
            }

            var httpClient = httpClientFactory.CreateClient();
            httpClient.BaseAddress = new Uri(_shiftsApiBaseUrl);

            var response = await httpClient.GetAsync($"/api/shifts/{shiftId}/location", cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("Failed to get shift location: {StatusCode}", response.StatusCode);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            return JsonSerializer.Deserialize<ShiftLocationInfo>(content);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting shift location");
            return null;
        }
    }

    private async Task UpdateAttendanceRecordAsync(
        IDbConnection connection,
        Guid attendanceRecordId,
        DateTime checkInTime,
        double checkInLatitude,
        double checkInLongitude,
        float? checkInLocationAccuracy,
        string checkInImageUrl,
        float faceMatchScore,
        double distanceFromSite,
        bool isLate,
        int lateMinutes,
        CancellationToken cancellationToken)
    {
        var sql = @"
            UPDATE attendance_records
            SET CheckInTime = @CheckInTime,
                CheckInLatitude = @CheckInLatitude,
                CheckInLongitude = @CheckInLongitude,
                CheckInLocationAccuracy = @CheckInLocationAccuracy,
                CheckInFaceImageUrl = @CheckInFaceImageUrl,
                CheckInFaceMatchScore = @CheckInFaceMatchScore,
                CheckInDistanceFromSite = @CheckInDistanceFromSite,
                IsLate = @IsLate,
                LateMinutes = @LateMinutes,
                Status = 'CHECKED_IN',
                UpdatedAt = @UpdatedAt
            WHERE Id = @Id";

        await connection.ExecuteAsync(sql, new
        {
            Id = attendanceRecordId,
            CheckInTime = checkInTime,
            CheckInLatitude = checkInLatitude,
            CheckInLongitude = checkInLongitude,
            CheckInLocationAccuracy = checkInLocationAccuracy,
            CheckInFaceImageUrl = checkInImageUrl,
            CheckInFaceMatchScore = (decimal)faceMatchScore,
            CheckInDistanceFromSite = (decimal)distanceFromSite,
            IsLate = isLate,
            LateMinutes = lateMinutes,
            UpdatedAt = DateTimeHelper.VietnamNow
        });
    }

    private async Task PublishShiftAssignmentUpdateAsync(
        Guid shiftAssignmentId,
        Guid shiftId,
        Guid guardId,
        DateTime checkInTime,
        bool isLate,
        int lateMinutes,
        float faceMatchScore,
        double distanceFromSite,
        CancellationToken cancellationToken)
    {
        try
        {
            var guardCheckedInEvent = new GuardCheckedInEvent
            {
                ShiftAssignmentId = shiftAssignmentId,
                ShiftId = shiftId,
                GuardId = guardId,
                CheckInTime = checkInTime,
                ConfirmedAt = checkInTime,
                IsLate = isLate,
                LateMinutes = lateMinutes,
                FaceMatchScore = faceMatchScore,
                DistanceFromSite = distanceFromSite
            };

            await publishEndpoint.Publish(guardCheckedInEvent, cancellationToken);

            logger.LogInformation(
                "📨 Published GuardCheckedInEvent: ShiftAssignment={ShiftAssignmentId}, Shift={ShiftId}, Guard={GuardId}",
                shiftAssignmentId,
                shiftId,
                guardId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error publishing GuardCheckedInEvent");
            // Don't throw - check-in already completed successfully
        }
    }

    private async Task PublishShiftUpdateAsync(
        Guid shiftId,
        CancellationToken cancellationToken)
    {
        // The GuardCheckedInEvent already contains ShiftId
        // Shifts.API consumer will handle updating the counters
        await Task.CompletedTask;
    }
}

internal record AttendanceRecordDto
{
    public Guid Id { get; init; }
    public Guid GuardId { get; init; }
    public Guid ShiftAssignmentId { get; init; }
    public Guid ShiftId { get; init; }
    public DateTime ScheduledStartTime { get; init; }
    public string Status { get; init; } = string.Empty;
}
