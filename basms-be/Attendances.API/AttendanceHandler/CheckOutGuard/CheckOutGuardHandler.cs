using Attendances.API.Extensions;

namespace Attendances.API.AttendanceHandler.CheckOutGuard;


public record CheckOutGuardCommand(
    Guid GuardId,
    Guid ShiftAssignmentId,
    Guid ShiftId,
    IFormFile CheckOutImage,
    double CheckOutLatitude,
    double CheckOutLongitude,
    float? CheckOutLocationAccuracy
) : ICommand<CheckOutGuardResult>;


public record CheckOutGuardResult
{
    public bool Success { get; init; }
    public Guid? AttendanceRecordId { get; init; }
    public DateTime? CheckOutTime { get; init; }
    public bool IsEarlyLeave { get; init; }
    public int EarlyLeaveMinutes { get; init; }
    public bool HasOvertime { get; init; }
    public int OvertimeMinutes { get; init; }
    public int ActualWorkDurationMinutes { get; init; }
    public decimal TotalHours { get; init; }
    public float FaceMatchScore { get; init; }
    public double DistanceFromSite { get; init; }
    public string? CheckOutImageUrl { get; init; }
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
    public string EventType { get; init; } = "check_out";
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
    public DateTime ScheduledEndTime { get; init; }
}


internal class CheckOutGuardHandler(
    IDbConnectionFactory dbFactory,
    ILogger<CheckOutGuardHandler> logger,
    IConfiguration configuration,
    IHttpClientFactory httpClientFactory,
    IS3Service s3Service,
    IPublishEndpoint publishEndpoint,
    IRequestClient<GetShiftLocationRequest> shiftLocationClient)
    : ICommandHandler<CheckOutGuardCommand, CheckOutGuardResult>
{
    private readonly string? _faceApiBaseUrl = configuration["FaceRecognitionApi:BaseUrl"]
                                              ?? configuration["FaceRecognitionApi__BaseUrl"]
                                              ?? configuration["FACEID_API_BASE_URL"];

    private const double MaxDistanceMeters = 500.0;
    private const float MinFaceMatchScore = 70.0f;

    public async Task<CheckOutGuardResult> Handle(
        CheckOutGuardCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation(
                "Starting check-out for Guard={GuardId}, ShiftAssignment={ShiftAssignmentId}, Shift={ShiftId}",
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

                return new CheckOutGuardResult
                {
                    Success = false,
                    ErrorMessage = "Attendance record không tồn tại. Vui lòng kiểm tra lại thông tin ca làm việc."
                };
            }
            
            if (attendanceRecord.Status == "CHECKED_OUT")
            {
                logger.LogWarning(
                    "Guard already checked out - AttendanceRecord={RecordId}",
                    attendanceRecord.Id);

                return new CheckOutGuardResult
                {
                    Success = false,
                    ErrorMessage = "Bạn đã check-out cho ca làm việc này rồi."
                };
            }

            if (attendanceRecord.Status != "CHECKED_IN")
            {
                logger.LogWarning(
                    "Invalid attendance record status - Status={Status}, Expected=CHECKED_IN",
                    attendanceRecord.Status);

                return new CheckOutGuardResult
                {
                    Success = false,
                    ErrorMessage = $"Chưa check-in. Vui lòng check-in trước khi check-out. Trạng thái hiện tại: {attendanceRecord.Status}"
                };
            }
            
            if (!attendanceRecord.CheckInTime.HasValue)
            {
                logger.LogWarning(
                    "CheckInTime not found - AttendanceRecord={RecordId}",
                    attendanceRecord.Id);

                return new CheckOutGuardResult
                {
                    Success = false,
                    ErrorMessage = "Không tìm thấy thời gian check-in. Dữ liệu không hợp lệ."
                };
            }

            logger.LogInformation("Found AttendanceRecord={RecordId}, Status={Status}, CheckInTime={CheckInTime}",
                attendanceRecord.Id, attendanceRecord.Status, attendanceRecord.CheckInTime);


            var registeredFaceDataJson = await GetRegisteredFaceTemplateUrlAsync(
                connection,
                request.GuardId,
                cancellationToken);

            if (string.IsNullOrWhiteSpace(registeredFaceDataJson))
            {
                logger.LogWarning(
                    "No registered face template found for Guard={GuardId}",
                    request.GuardId);

                return new CheckOutGuardResult
                {
                    Success = false,
                    ErrorMessage = "Chưa đăng ký khuôn mặt. Vui lòng đăng ký khuôn mặt trước khi check-out."
                };
            }

            logger.LogInformation("Found face data: {Data}", registeredFaceDataJson.Length > 100 ? registeredFaceDataJson.Substring(0, 100) + "..." : registeredFaceDataJson);
            
            logger.LogInformation("Verifying face with Python API...");

            var faceVerificationResult = await VerifyFaceAsync(
                request.GuardId,
                request.CheckOutImage,
                registeredFaceDataJson,
                cancellationToken);

            if (faceVerificationResult == null)
            {
                logger.LogError(
                    "Face verification failed - API returned null response");

                return new CheckOutGuardResult
                {
                    Success = false,
                    ErrorMessage = "Không thể xác minh khuôn mặt. Vui lòng kiểm tra kết nối mạng hoặc liên hệ quản trị viên."
                };
            }

            if (!faceVerificationResult.FaceDetected)
            {
                logger.LogWarning(
                    "No face detected in check-out image - Guard={GuardId}",
                    request.GuardId);

                return new CheckOutGuardResult
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

                return new CheckOutGuardResult
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

                return new CheckOutGuardResult
                {
                    Success = false,
                    FaceMatchScore = faceVerificationResult.Confidence,
                    ErrorMessage = $"Khuôn mặt không khớp với dữ liệu đã đăng ký. Độ chính xác: {faceVerificationResult.Confidence:F1}% (yêu cầu >= {MinFaceMatchScore}%)"
                };
            }

            logger.LogInformation(
                "✓ Face verified successfully - Confidence={Confidence}%, Quality={Quality}%",
                faceVerificationResult.Confidence,
                faceVerificationResult.FaceQuality);

            logger.LogInformation("Uploading check-out image to S3...");

            var checkOutImageUrl = await UploadCheckOutImageToS3Async(
                request.GuardId,
                request.ShiftId,
                request.CheckOutImage,
                cancellationToken);

            logger.LogInformation("Image uploaded: {ImageUrl}", checkOutImageUrl);
            
            logger.LogInformation("Getting shift location from Shifts.API via MassTransit...");

            var shiftLocation = await GetShiftLocationAsync(
                request.ShiftId,
                cancellationToken);

            if (shiftLocation == null)
            {
                return new CheckOutGuardResult
                {
                    Success = false,
                    ErrorMessage = "Failed to retrieve shift location information"
                };
            }

            logger.LogInformation(
                "Shift location: Lat={Lat}, Lon={Lon}, ScheduledEndTime={EndTime}",
                shiftLocation.LocationLatitude,
                shiftLocation.LocationLongitude,
                shiftLocation.ScheduledEndTime);


            var distanceFromSite = GeoLocationHelper.CalculateDistanceInMeters(
                request.CheckOutLatitude,
                request.CheckOutLongitude,
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

                return new CheckOutGuardResult
                {
                    Success = false,
                    DistanceFromSite = distanceFromSite,
                    FaceMatchScore = faceVerificationResult.Confidence,
                    ErrorMessage = $"Quá xa công trường. Khoảng cách hiện tại: {distanceFromSite:F0}m (tối đa cho phép: {MaxDistanceMeters}m)"
                };
            }

            logger.LogInformation("Location validated - Distance within acceptable range");

            var checkOutTime = DateTimeHelper.VietnamNow;
            var checkInTime = attendanceRecord.CheckInTime.Value;
            var scheduledEndTime = shiftLocation.ScheduledEndTime;
            var actualWorkDurationMinutes = (int)Math.Ceiling((checkOutTime - checkInTime).TotalMinutes);
            
            var breakDurationMinutes = attendanceRecord.BreakDurationMinutes;
            var netWorkMinutes = actualWorkDurationMinutes - breakDurationMinutes;
            var totalHours = Math.Round((decimal)netWorkMinutes / 60, 2);

            logger.LogInformation(
                "Work Duration: CheckIn={CheckIn}, CheckOut={CheckOut}, Actual={ActualMinutes}min, Break={BreakMinutes}min, Total={TotalHours}h",
                checkInTime.ToString("yyyy-MM-dd HH:mm:ss"),
                checkOutTime.ToString("yyyy-MM-dd HH:mm:ss"),
                actualWorkDurationMinutes,
                breakDurationMinutes,
                totalHours);
            
            var isEarlyLeave = checkOutTime < scheduledEndTime;
            var earlyLeaveMinutes = isEarlyLeave
                ? (int)Math.Ceiling((scheduledEndTime - checkOutTime).TotalMinutes)
                : 0;

            if (isEarlyLeave)
            {
                logger.LogWarning(
                    "Guard is leaving early by {EarlyLeaveMinutes} minutes (CheckOut={CheckOut}, Scheduled={Scheduled})",
                    earlyLeaveMinutes,
                    checkOutTime.ToString("yyyy-MM-dd HH:mm:ss"),
                    scheduledEndTime.ToString("yyyy-MM-dd HH:mm:ss"));
            }
            else
            {
                logger.LogInformation("Guard is checking out on time or later");
            }
            
            var hasOvertime = checkOutTime > scheduledEndTime;
            var overtimeMinutes = hasOvertime
                ? (int)Math.Floor((checkOutTime - scheduledEndTime).TotalMinutes)
                : 0;

            if (hasOvertime)
            {
                logger.LogInformation(
                    "Guard has overtime: {OvertimeMinutes} minutes (CheckOut={CheckOut}, Scheduled={Scheduled})",
                    overtimeMinutes,
                    checkOutTime.ToString("yyyy-MM-dd HH:mm:ss"),
                    scheduledEndTime.ToString("yyyy-MM-dd HH:mm:ss"));
            }

            logger.LogInformation("Updating attendance record with check-out data...");

            await UpdateAttendanceRecordAsync(
                connection,
                attendanceRecord.Id,
                checkOutTime,
                request.CheckOutLatitude,
                request.CheckOutLongitude,
                request.CheckOutLocationAccuracy,
                checkOutImageUrl,
                faceVerificationResult.Confidence,
                distanceFromSite,
                actualWorkDurationMinutes,
                totalHours,
                isEarlyLeave,
                earlyLeaveMinutes,
                hasOvertime,
                overtimeMinutes,
                cancellationToken);

            logger.LogInformation("Attendance record updated with status CHECKED_OUT");


            logger.LogInformation("Publishing integration events...");

            await PublishShiftAssignmentUpdateAsync(
                request.ShiftAssignmentId,
                request.ShiftId,
                request.GuardId,
                checkOutTime,
                isEarlyLeave,
                earlyLeaveMinutes,
                hasOvertime,
                overtimeMinutes,
                actualWorkDurationMinutes,
                totalHours,
                faceVerificationResult.Confidence,
                distanceFromSite,
                cancellationToken);

            logger.LogInformation("GuardCheckedOutEvent published successfully");
            
            
            logger.LogInformation(
                "Check-out completed successfully for Guard={GuardId}, Duration={TotalHours}h, Overtime={OvertimeMinutes}min, EarlyLeave={EarlyLeaveMinutes}min",
                request.GuardId,
                totalHours,
                overtimeMinutes,
                earlyLeaveMinutes);

            return new CheckOutGuardResult
            {
                Success = true,
                AttendanceRecordId = attendanceRecord.Id,
                CheckOutTime = checkOutTime,
                IsEarlyLeave = isEarlyLeave,
                EarlyLeaveMinutes = earlyLeaveMinutes,
                HasOvertime = hasOvertime,
                OvertimeMinutes = overtimeMinutes,
                ActualWorkDurationMinutes = actualWorkDurationMinutes,
                TotalHours = totalHours,
                FaceMatchScore = faceVerificationResult.Confidence,
                DistanceFromSite = distanceFromSite,
                CheckOutImageUrl = checkOutImageUrl,
                Message = "Check-out completed successfully. Shift completed."
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during check-out for Guard={GuardId}", request.GuardId);

            return new CheckOutGuardResult
            {
                Success = false,
                ErrorMessage = $"Check-out failed: {ex.Message}"
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
            SELECT Id, GuardId, ShiftAssignmentId, ShiftId, CheckInTime, ScheduledEndTime, Status, BreakDurationMinutes
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
        IFormFile checkOutImage,
        string registeredFaceTemplateUrl,
        CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_faceApiBaseUrl))
            {
                logger.LogError("Face Recognition API URL not configured");
                return null;
            }
            
            string base64Image;
            using (var memoryStream = new MemoryStream())
            {
                await checkOutImage.CopyToAsync(memoryStream, cancellationToken);
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
                TemplateUrl = registeredFaceTemplateUrl,
                EventType = "check_out"
            };

            var jsonContent = JsonSerializer.Serialize(requestBody);
            var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            logger.LogInformation("Calling Face Verification API: {Url}", $"{_faceApiBaseUrl}/api/v1/faces/verify");

            var response = await httpClient.PostAsync("/api/v1/faces/verify", httpContent, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                logger.LogError("Face Verification API failed - StatusCode={StatusCode}, Error={Error}",
                    response.StatusCode, errorContent);
                return null;
            }

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var result = JsonSerializer.Deserialize<FaceVerificationApiResponse>(responseContent, options);

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error calling Face Verification API");
            return null;
        }
    }

    private async Task<string> UploadCheckOutImageToS3Async(
        Guid guardId,
        Guid shiftId,
        IFormFile image,
        CancellationToken cancellationToken)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var key = $"check-out/{guardId}/{shiftId}/{timestamp}.jpg";

        using var stream = image.OpenReadStream();

        var (success, fileUrl, errorMessage) = await s3Service.UploadFileWithCustomKeyAsync(
            stream,
            key,
            "image/jpeg",
            cancellationToken);

        if (!success || string.IsNullOrEmpty(fileUrl))
        {
            logger.LogError("Failed to upload check-out image to S3: {ErrorMessage}", errorMessage);
            throw new InvalidOperationException($"Failed to upload image to S3: {errorMessage}");
        }

        logger.LogInformation("Check-out image uploaded successfully: {FileUrl}", fileUrl);
        return s3Service.GetPresignedUrl(fileUrl, expirationMinutes: 10080);
    }

    private async Task<ShiftLocationInfo?> GetShiftLocationAsync(
        Guid shiftId,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await shiftLocationClient.GetResponse<GetShiftLocationResponse>(
                new GetShiftLocationRequest { ShiftId = shiftId },
                cancellationToken,
                timeout: RequestTimeout.After(s: 10));

            var shiftLocationResponse = response.Message;

            if (!shiftLocationResponse.Success || shiftLocationResponse.Location == null)
            {
                logger.LogError("Failed to get shift location: {ErrorMessage}",
                    shiftLocationResponse.ErrorMessage ?? "Unknown error");
                return null;
            }

            return new ShiftLocationInfo
            {
                LocationLatitude = shiftLocationResponse.Location.LocationLatitude,
                LocationLongitude = shiftLocationResponse.Location.LocationLongitude,
                ScheduledEndTime = shiftLocationResponse.Location.ScheduledEndTime
            };
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
        DateTime checkOutTime,
        double checkOutLatitude,
        double checkOutLongitude,
        float? checkOutLocationAccuracy,
        string checkOutImageUrl,
        float faceMatchScore,
        double distanceFromSite,
        int actualWorkDurationMinutes,
        decimal totalHours,
        bool isEarlyLeave,
        int earlyLeaveMinutes,
        bool hasOvertime,
        int overtimeMinutes,
        CancellationToken cancellationToken)
    {
        var sql = @"
            UPDATE attendance_records
            SET CheckOutTime = @CheckOutTime,
                CheckOutLatitude = @CheckOutLatitude,
                CheckOutLongitude = @CheckOutLongitude,
                CheckOutLocationAccuracy = @CheckOutLocationAccuracy,
                CheckOutFaceImageUrl = @CheckOutFaceImageUrl,
                CheckOutFaceMatchScore = @CheckOutFaceMatchScore,
                CheckOutDistanceFromSite = @CheckOutDistanceFromSite,
                ActualWorkDurationMinutes = @ActualWorkDurationMinutes,
                TotalHours = @TotalHours,
                IsEarlyLeave = @IsEarlyLeave,
                EarlyLeaveMinutes = @EarlyLeaveMinutes,
                HasOvertime = @HasOvertime,
                OvertimeMinutes = @OvertimeMinutes,
                Status = 'CHECKED_OUT',
                IsIncomplete = false,
                Notes = 'COMPLETED_SHIFT',
                UpdatedAt = @UpdatedAt
            WHERE Id = @Id";

        await connection.ExecuteAsync(sql, new
        {
            Id = attendanceRecordId,
            CheckOutTime = checkOutTime,
            CheckOutLatitude = checkOutLatitude,
            CheckOutLongitude = checkOutLongitude,
            CheckOutLocationAccuracy = checkOutLocationAccuracy,
            CheckOutFaceImageUrl = checkOutImageUrl,
            CheckOutFaceMatchScore = (decimal)faceMatchScore,
            CheckOutDistanceFromSite = (decimal)distanceFromSite,
            ActualWorkDurationMinutes = actualWorkDurationMinutes,
            TotalHours = totalHours,
            IsEarlyLeave = isEarlyLeave,
            EarlyLeaveMinutes = earlyLeaveMinutes,
            HasOvertime = hasOvertime,
            OvertimeMinutes = overtimeMinutes,
            UpdatedAt = DateTimeHelper.VietnamNow
        });
    }

    private async Task PublishShiftAssignmentUpdateAsync(
        Guid shiftAssignmentId,
        Guid shiftId,
        Guid guardId,
        DateTime checkOutTime,
        bool isEarlyLeave,
        int earlyLeaveMinutes,
        bool hasOvertime,
        int overtimeMinutes,
        int actualWorkDurationMinutes,
        decimal totalHours,
        float faceMatchScore,
        double distanceFromSite,
        CancellationToken cancellationToken)
    {
        try
        {
            var guardCheckedOutEvent = new GuardCheckedOutEvent
            {
                ShiftAssignmentId = shiftAssignmentId,
                ShiftId = shiftId,
                GuardId = guardId,
                CheckOutTime = checkOutTime,
                CompletedAt = checkOutTime,
                IsEarlyLeave = isEarlyLeave,
                EarlyLeaveMinutes = earlyLeaveMinutes,
                HasOvertime = hasOvertime,
                OvertimeMinutes = overtimeMinutes,
                ActualWorkDurationMinutes = actualWorkDurationMinutes,
                TotalHours = totalHours,
                FaceMatchScore = faceMatchScore,
                DistanceFromSite = distanceFromSite
            };

            await publishEndpoint.Publish(guardCheckedOutEvent, cancellationToken);

            logger.LogInformation(
                "Published GuardCheckedOutEvent: ShiftAssignment={ShiftAssignmentId}, Shift={ShiftId}, Guard={GuardId}",
                shiftAssignmentId,
                shiftId,
                guardId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error publishing GuardCheckedOutEvent");
        }
    }
}

internal record AttendanceRecordDto
{
    public Guid Id { get; init; }
    public Guid GuardId { get; init; }
    public Guid ShiftAssignmentId { get; init; }
    public Guid ShiftId { get; init; }
    public DateTime? CheckInTime { get; init; }
    public DateTime? ScheduledEndTime { get; init; }
    public string Status { get; init; } = string.Empty;
    public int BreakDurationMinutes { get; init; } = 60;
}
