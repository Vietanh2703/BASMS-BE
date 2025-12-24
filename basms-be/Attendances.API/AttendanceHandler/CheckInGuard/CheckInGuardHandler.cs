using Attendances.API.Extensions;


namespace Attendances.API.AttendanceHandler.CheckInGuard;

public record CheckInGuardCommand(
    Guid GuardId,
    Guid ShiftAssignmentId,
    Guid ShiftId,
    IFormFile CheckInImage,
    double CheckInLatitude,
    double CheckInLongitude,
    float? CheckInLocationAccuracy
) : ICommand<CheckInGuardResult>;

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

internal record FaceVerificationApiRequest
{
    [JsonPropertyName("guard_id")]
    public string GuardId { get; init; } = string.Empty;

    [JsonPropertyName("check_image_base64")]
    public string CheckImageBase64 { get; init; } = string.Empty;

    [JsonPropertyName("template_url")]
    public string? TemplateUrl { get; init; }

    [JsonPropertyName("event_type")]
    public string EventType { get; init; } = "check_in";
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

internal record ShiftLocationInfo
{
    public double LocationLatitude { get; init; }
    public double LocationLongitude { get; init; }
    public DateTime ScheduledStartTime { get; init; }
}


internal class CheckInGuardHandler(
    IDbConnectionFactory dbFactory,
    ILogger<CheckInGuardHandler> logger,
    IConfiguration configuration,
    IHttpClientFactory httpClientFactory,
    IS3Service s3Service,
    IPublishEndpoint publishEndpoint,
    IRequestClient<GetShiftLocationRequest> shiftLocationClient)
    : ICommandHandler<CheckInGuardCommand, CheckInGuardResult>
{
    private readonly string? _faceApiBaseUrl = configuration["FaceRecognitionApi:BaseUrl"]
                                              ?? configuration["FaceRecognitionApi__BaseUrl"]
                                              ?? configuration["FACEID_API_BASE_URL"];

    private const double MaxDistanceMeters = 500.0;
    private const float MinFaceMatchScore = 70.0f;

    public async Task<CheckInGuardResult> Handle(
        CheckInGuardCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation(
                "Starting check-in for Guard={GuardId}, ShiftAssignment={ShiftAssignmentId}, Shift={ShiftId}",
                request.GuardId, request.ShiftAssignmentId, request.ShiftId);
            
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
                    "Attendance record not found - Guard={GuardId}, ShiftAssignment={ShiftAssignmentId}, Shift={ShiftId}",
                    request.GuardId, request.ShiftAssignmentId, request.ShiftId);

                return new CheckInGuardResult
                {
                    Success = false,
                    ErrorMessage = "Attendance record không tồn tại. Vui lòng kiểm tra lại thông tin ca làm việc."
                };
            }
            
            if (attendanceRecord.Status == "CHECKED_IN")
            {
                logger.LogWarning(
                    "Guard already checked in - AttendanceRecord={RecordId}",
                    attendanceRecord.Id);

                return new CheckInGuardResult
                {
                    Success = false,
                    ErrorMessage = "Bạn đã check-in cho ca làm việc này rồi."
                };
            }

            if (attendanceRecord.Status != "PENDING")
            {
                logger.LogWarning(
                    "Invalid attendance record status - Status={Status}, Expected=PENDING",
                    attendanceRecord.Status);

                return new CheckInGuardResult
                {
                    Success = false,
                    ErrorMessage = $"Trạng thái attendance record không hợp lệ: {attendanceRecord.Status}"
                };
            }

            logger.LogInformation("Found AttendanceRecord={RecordId}, Status={Status}",
                attendanceRecord.Id, attendanceRecord.Status);


            var registeredFaceDataJson = await GetRegisteredFaceTemplateUrlAsync(
                connection,
                request.GuardId,
                cancellationToken);

            if (string.IsNullOrWhiteSpace(registeredFaceDataJson))
            {
                logger.LogWarning(
                    "No registered face template found for Guard={GuardId}",
                    request.GuardId);

                return new CheckInGuardResult
                {
                    Success = false,
                    ErrorMessage = "Chưa đăng ký khuôn mặt. Vui lòng đăng ký khuôn mặt trước khi check-in."
                };
            }

            logger.LogInformation("Found face data: {Data}", registeredFaceDataJson.Length > 100 ? registeredFaceDataJson.Substring(0, 100) + "..." : registeredFaceDataJson);


            logger.LogInformation("Verifying face with Python API...");

            var faceVerificationResult = await VerifyFaceAsync(
                request.GuardId,
                request.CheckInImage,
                registeredFaceDataJson,
                cancellationToken);

            if (faceVerificationResult == null)
            {
                logger.LogError(
                    "Face verification failed - API returned null response. " +
                    "Check logs above for specific error (config issue, network error, or API failure)");

                return new CheckInGuardResult
                {
                    Success = false,
                    ErrorMessage = "Không thể xác minh khuôn mặt. Vui lòng kiểm tra: " +
                                 "(1) Kết nối mạng, (2) Chất lượng ảnh đăng ký, (3) Liên hệ quản trị viên nếu vấn đề tiếp diễn."
                };
            }

            if (!faceVerificationResult.FaceDetected)
            {
                logger.LogWarning(
                    "No face detected in check-in image - Guard={GuardId}",
                    request.GuardId);

                return new CheckInGuardResult
                {
                    Success = false,
                    FaceMatchScore = 0,
                    ErrorMessage = "Không phát hiện khuôn mặt trong ảnh. Vui lòng chụp ảnh rõ ràng hơn."
                };
            }

            if (faceVerificationResult.FaceQuality < 50)
            {
                logger.LogWarning(
                    "Low face quality detected - Quality={Quality}",
                    faceVerificationResult.FaceQuality);

                return new CheckInGuardResult
                {
                    Success = false,
                    FaceMatchScore = faceVerificationResult.Confidence,
                    ErrorMessage = $"Chất lượng ảnh khuôn mặt thấp ({faceVerificationResult.FaceQuality:F1}/100). Vui lòng chụp ảnh trong điều kiện sáng tốt hơn."
                };
            }

            if (!faceVerificationResult.IsMatch || faceVerificationResult.Confidence < MinFaceMatchScore)
            {
                logger.LogWarning(
                    "Face verification failed - IsMatch={IsMatch}, Confidence={Confidence}, Required={Required}",
                    faceVerificationResult.IsMatch,
                    faceVerificationResult.Confidence,
                    MinFaceMatchScore);

                return new CheckInGuardResult
                {
                    Success = false,
                    FaceMatchScore = faceVerificationResult.Confidence,
                    ErrorMessage = $"Khuôn mặt không khớp với dữ liệu đã đăng ký. Độ chính xác: {faceVerificationResult.Confidence:F1}% (yêu cầu >= {MinFaceMatchScore}%)"
                };
            }

            logger.LogInformation(
                "Face verified successfully - Confidence={Confidence}%, Quality={Quality}%",
                faceVerificationResult.Confidence,
                faceVerificationResult.FaceQuality);
            
            logger.LogInformation("Uploading check-in image to S3...");

            var checkInImageUrl = await UploadCheckInImageToS3Async(
                request.GuardId,
                request.ShiftId,
                request.CheckInImage,
                cancellationToken);

            logger.LogInformation("✓ Image uploaded: {ImageUrl}", checkInImageUrl);

            logger.LogInformation("Getting shift location from Shifts.API via MassTransit...");

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

            var distanceFromSite = GeoLocationHelper.CalculateDistanceInMeters(
                request.CheckInLatitude,
                request.CheckInLongitude,
                shiftLocation.LocationLatitude,
                shiftLocation.LocationLongitude);

            logger.LogInformation(
                "Distance from site: {Distance:F2}m (max: {MaxDistance}m)",
                distanceFromSite,
                MaxDistanceMeters);

            if (distanceFromSite > MaxDistanceMeters)
            {
                logger.LogWarning(
                    "Guard too far from site - Distance={Distance:F0}m, Max={MaxDistance}m",
                    distanceFromSite,
                    MaxDistanceMeters);

                return new CheckInGuardResult
                {
                    Success = false,
                    DistanceFromSite = distanceFromSite,
                    FaceMatchScore = faceVerificationResult.Confidence,
                    ErrorMessage = $"Quá xa công trường. Khoảng cách hiện tại: {distanceFromSite:F0}m (tối đa cho phép: {MaxDistanceMeters}m)"
                };
            }

            logger.LogInformation("Location validated - Distance within acceptable range");
            
            var checkInTime = DateTimeHelper.VietnamNow;
            var scheduledStartTime = shiftLocation.ScheduledStartTime;

            var isLate = checkInTime > scheduledStartTime;
            var lateMinutes = isLate
                ? (int)Math.Ceiling((checkInTime - scheduledStartTime).TotalMinutes)
                : 0;

            if (isLate)
            {
                logger.LogWarning(
                    "Guard is late by {LateMinutes} minutes",
                    lateMinutes);
            }
            else
            {
                logger.LogInformation("Guard is on time");
            }

            logger.LogInformation("Updating attendance record...");

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

            logger.LogInformation("Attendance record updated");


            logger.LogInformation("Publishing integration events...");

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

            logger.LogInformation("GuardCheckedInEvent published successfully");
            
            logger.LogInformation(
                "Check-in completed successfully for Guard={GuardId}",
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
            logger.LogError(ex, "Error during check-in for Guard={GuardId}", request.GuardId);

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
        string registeredFaceTemplateUrl,
        CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_faceApiBaseUrl))
            {
                logger.LogError(
                    "Face Recognition API URL not configured. Please set one of these environment variables: " +
                    "FaceRecognitionApi:BaseUrl, FaceRecognitionApi__BaseUrl, or FACEID_API_BASE_URL");
                return null;
            }

            logger.LogInformation("Using Face API URL: {Url}", _faceApiBaseUrl);


            string base64Image;
            using (var memoryStream = new MemoryStream())
            {
                await checkInImage.CopyToAsync(memoryStream, cancellationToken);
                var imageBytes = memoryStream.ToArray();
                base64Image = Convert.ToBase64String(imageBytes);
            }
            
            var httpClient = httpClientFactory.CreateClient();
            httpClient.BaseAddress = new Uri(_faceApiBaseUrl);
            httpClient.Timeout = TimeSpan.FromSeconds(60);

            var requestBody = new FaceVerificationApiRequest
            {
                GuardId = guardId.ToString(),
                CheckImageBase64 = base64Image,
                TemplateUrl = registeredFaceTemplateUrl
            };

            var jsonContent = JsonSerializer.Serialize(requestBody);
            var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            logger.LogInformation(
                "Calling Face Verification API: {Url}",
                $"{_faceApiBaseUrl}/api/v1/faces/verify");
            logger.LogInformation(
                "Request: GuardId={GuardId}, ImageSize={ImageSize}KB, TemplateUrlLength={TemplateUrlLength}",
                guardId,
                base64Image.Length / 1024,
                registeredFaceTemplateUrl?.Length ?? 0);

            var response = await httpClient.PostAsync("/api/v1/faces/verify", httpContent, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                logger.LogError(
                    "Face Verification API failed - StatusCode={StatusCode}, Error={Error}",
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
            var result = JsonSerializer.Deserialize<FaceVerificationApiResponse>(responseContent, options);

            if (result == null)
            {
                logger.LogError(
                    "Failed to deserialize verification response. Response: {Response}",
                    responseContent);
                return null;
            }

            logger.LogInformation(
                "Verification result: IsMatch={IsMatch}, Confidence={Confidence}%, FaceDetected={FaceDetected}, Quality={Quality}%",
                result.IsMatch,
                result.Confidence,
                result.FaceDetected,
                result.FaceQuality);

            return result;
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(
                ex,
                "HTTP error when calling Face Verification API at {Url}. Check if the service is running and accessible.",
                _faceApiBaseUrl);
            return null;
        }
        catch (TaskCanceledException ex)
        {
            logger.LogError(
                ex,
                "Face Verification API request timeout after {Timeout} seconds",
                60);
            return null;
        }
        catch (JsonException ex)
        {
            logger.LogError(
                ex,
                "Failed to parse JSON when calling Face Verification API");
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Unexpected error when calling Face Verification API: {Message}",
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
        
        var (success, fileUrl, errorMessage) = await s3Service.UploadFileWithCustomKeyAsync(
            stream,
            key,
            "image/jpeg",
            cancellationToken);

        if (!success || string.IsNullOrEmpty(fileUrl))
        {
            logger.LogError("Failed to upload check-in image to S3: {ErrorMessage}", errorMessage);
            throw new InvalidOperationException($"Failed to upload image to S3: {errorMessage}");
        }

        logger.LogInformation("Check-in image uploaded successfully: {FileUrl}", fileUrl);
        
        return s3Service.GetPresignedUrl(fileUrl, expirationMinutes: 10080);
    }

    private async Task<ShiftLocationInfo?> GetShiftLocationAsync(
        Guid shiftId,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Requesting shift location from Shifts.API via MassTransit for ShiftId: {ShiftId}", shiftId);
            
            var response = await shiftLocationClient.GetResponse<GetShiftLocationResponse>(
                new GetShiftLocationRequest { ShiftId = shiftId },
                cancellationToken,
                timeout: RequestTimeout.After(s: 10));

            var shiftLocationResponse = response.Message;

            if (!shiftLocationResponse.Success || shiftLocationResponse.Location == null)
            {
                logger.LogError(
                    "Failed to get shift location: {ErrorMessage}",
                    shiftLocationResponse.ErrorMessage ?? "Unknown error");
                return null;
            }

            logger.LogInformation(
                "Received shift location from Shifts.API: Lat={Lat}, Lon={Lon}",
                shiftLocationResponse.Location.LocationLatitude,
                shiftLocationResponse.Location.LocationLongitude);
            
            return new ShiftLocationInfo
            {
                LocationLatitude = shiftLocationResponse.Location.LocationLatitude,
                LocationLongitude = shiftLocationResponse.Location.LocationLongitude,
                ScheduledStartTime = shiftLocationResponse.Location.ScheduledStartTime
            };
        }
        catch (RequestTimeoutException)
        {
            logger.LogError("Timeout getting shift location from Shifts.API for ShiftId: {ShiftId}", shiftId);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting shift location from Shifts.API for ShiftId: {ShiftId}", shiftId);
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
                "Published GuardCheckedInEvent: ShiftAssignment={ShiftAssignmentId}, Shift={ShiftId}, Guard={GuardId}",
                shiftAssignmentId,
                shiftId,
                guardId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error publishing GuardCheckedInEvent");
        }
    }

    private async Task PublishShiftUpdateAsync(
        Guid shiftId,
        CancellationToken cancellationToken)
    {
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
