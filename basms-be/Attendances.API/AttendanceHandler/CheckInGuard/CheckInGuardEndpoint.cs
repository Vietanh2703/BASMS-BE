namespace Attendances.API.AttendanceHandler.CheckInGuard;

/// <summary>
/// Endpoint để guard check-in với face verification và location validation
/// </summary>
public class CheckInGuardEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/attendance/check-in", async (
                HttpRequest httpRequest,
                ISender sender,
                ILogger<CheckInGuardEndpoint> logger,
                CancellationToken cancellationToken) =>
            {
                logger.LogInformation("POST /api/attendance/check-in - Processing check-in with face verification");

                // ================================================================
                // PARSE MULTIPART FORM-DATA
                // ================================================================

                if (!httpRequest.HasFormContentType)
                {
                    return Results.BadRequest(new
                    {
                        success = false,
                        error = "Request must be multipart/form-data"
                    });
                }

                var form = await httpRequest.ReadFormAsync(cancellationToken);

                // ================================================================
                // VALIDATE REQUIRED FIELDS
                // ================================================================

                // Get GuardId
                if (!Guid.TryParse(form["guardId"].ToString(), out var guardId) || guardId == Guid.Empty)
                {
                    return Results.BadRequest(new
                    {
                        success = false,
                        error = "GuardId is required and must be a valid GUID"
                    });
                }

                // Get ShiftAssignmentId
                if (!Guid.TryParse(form["shiftAssignmentId"].ToString(), out var shiftAssignmentId) || shiftAssignmentId == Guid.Empty)
                {
                    return Results.BadRequest(new
                    {
                        success = false,
                        error = "ShiftAssignmentId is required and must be a valid GUID"
                    });
                }

                // Get ShiftId
                if (!Guid.TryParse(form["shiftId"].ToString(), out var shiftId) || shiftId == Guid.Empty)
                {
                    return Results.BadRequest(new
                    {
                        success = false,
                        error = "ShiftId is required and must be a valid GUID"
                    });
                }

                // Get CheckInLatitude
                if (!double.TryParse(form["checkInLatitude"].ToString(), out var checkInLatitude))
                {
                    return Results.BadRequest(new
                    {
                        success = false,
                        error = "CheckInLatitude is required and must be a valid number"
                    });
                }

                // Get CheckInLongitude
                if (!double.TryParse(form["checkInLongitude"].ToString(), out var checkInLongitude))
                {
                    return Results.BadRequest(new
                    {
                        success = false,
                        error = "CheckInLongitude is required and must be a valid number"
                    });
                }

                // Validate GPS coordinates range
                if (checkInLatitude < -90 || checkInLatitude > 90)
                {
                    return Results.BadRequest(new
                    {
                        success = false,
                        error = "Latitude must be between -90 and 90"
                    });
                }

                if (checkInLongitude < -180 || checkInLongitude > 180)
                {
                    return Results.BadRequest(new
                    {
                        success = false,
                        error = "Longitude must be between -180 and 180"
                    });
                }

                // Get optional CheckInLocationAccuracy
                float? checkInLocationAccuracy = null;
                if (float.TryParse(form["checkInLocationAccuracy"].ToString(), out var accuracy))
                {
                    checkInLocationAccuracy = accuracy;
                }

                // Get CheckInImage file
                var checkInImage = form.Files.GetFile("checkInImage");
                if (checkInImage == null || checkInImage.Length == 0)
                {
                    return Results.BadRequest(new
                    {
                        success = false,
                        error = "CheckInImage is required",
                        hint = "Required field: checkInImage (JPG or PNG, max 10MB)"
                    });
                }

                // Validate file size (max 10MB)
                if (checkInImage.Length > 10 * 1024 * 1024)
                {
                    return Results.BadRequest(new
                    {
                        success = false,
                        error = "Image file too large. Maximum size: 10MB"
                    });
                }

                // Validate file type
                var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png" };
                if (!allowedTypes.Contains(checkInImage.ContentType.ToLower()))
                {
                    return Results.BadRequest(new
                    {
                        success = false,
                        error = "Invalid image format. Only JPG and PNG are allowed"
                    });
                }

                // ================================================================
                // CREATE COMMAND
                // ================================================================

                var command = new CheckInGuardCommand(
                    GuardId: guardId,
                    ShiftAssignmentId: shiftAssignmentId,
                    ShiftId: shiftId,
                    CheckInImage: checkInImage!,
                    CheckInLatitude: checkInLatitude,
                    CheckInLongitude: checkInLongitude,
                    CheckInLocationAccuracy: checkInLocationAccuracy
                );

                var result = await sender.Send(command, cancellationToken);

                if (!result.Success)
                {
                    logger.LogWarning(
                        "Failed to check-in: {Error}",
                        result.ErrorMessage);

                    return Results.BadRequest(new
                    {
                        success = false,
                        error = result.ErrorMessage,
                        data = new
                        {
                            faceMatchScore = result.FaceMatchScore > 0 ? result.FaceMatchScore : (float?)null,
                            distanceFromSite = result.DistanceFromSite > 0 ? Math.Round(result.DistanceFromSite, 2) : (double?)null
                        }
                    });
                }

                logger.LogInformation(
                    "✓ Check-in successful: GuardId={GuardId}, AttendanceRecord={AttendanceRecordId}, Late={IsLate}, FaceMatch={FaceMatchScore}%",
                    guardId,
                    result.AttendanceRecordId,
                    result.IsLate,
                    result.FaceMatchScore);

                return Results.Ok(new
                {
                    success = true,
                    data = new
                    {
                        guardId = guardId,
                        attendanceRecordId = result.AttendanceRecordId,
                        checkInTime = result.CheckInTime?.ToString("yyyy-MM-dd HH:mm:ss"),
                        isLate = result.IsLate,
                        lateMinutes = result.LateMinutes,
                        faceMatchScore = result.FaceMatchScore,
                        distanceFromSite = Math.Round(result.DistanceFromSite, 2),
                        checkInImageUrl = result.CheckInImageUrl
                    },
                    message = result.Message
                });
            })
            .DisableAntiforgery() // Required for form-data
            // .RequireAuthorization()
            .WithName("CheckInGuard")
            .WithTags("Attendance")
            .Produces(200)
            .Produces(400)
            .WithSummary("Guard check-in with face verification and location validation")
            .WithDescription(@"
            Guard check-in endpoint using multipart/form-data with face verification and GPS location validation.
            Validates face match, location distance, and updates attendance record with integration to Shifts.API.

            ** POSTMAN USAGE **

            1. Set request type: POST
            2. URL: http://localhost:5004/api/attendance/check-in
            3. Go to 'Body' tab
            4. Select 'form-data'
            5. Add the following fields:

            | KEY                      | TYPE | VALUE                          |
            |--------------------------|------|--------------------------------|
            | guardId                  | Text | {your-guard-uuid}              |
            | shiftAssignmentId        | Text | {shift-assignment-uuid}        |
            | shiftId                  | Text | {shift-uuid}                   |
            | checkInImage             | File | [Select check-in photo]        |
            | checkInLatitude          | Text | 10.762622                      |
            | checkInLongitude         | Text | 106.660172                     |
            | checkInLocationAccuracy  | Text | 5.0 (optional)                 |

            ** CHECK-IN PROCESSING **

            The handler processes check-in in these steps:
            1. ✓ Validate attendance record exists (status = PENDING)
            2. ✓ Validate face template registered
            3. ✓ Verify face with Python Face Recognition API
            4. ✓ Upload check-in image to AWS S3
            5. ✓ Get shift location from Shifts.API
            6. ✓ Validate distance from site (<= 200m)
            7. ✓ Calculate late status
            8. ✓ Update attendance record
            9. ✓ Publish GuardCheckedInEvent to Shifts.API
            10. ✅ Complete

            ** RESPONSE **

            {
                ""success"": true,
                ""data"": {
                    ""guardId"": ""550e8400-e29b-41d4-a716-446655440000"",
                    ""attendanceRecordId"": ""guid"",
                    ""checkInTime"": ""2025-12-19 14:30:00"",
                    ""isLate"": false,
                    ""lateMinutes"": 0,
                    ""faceMatchScore"": 95.5,
                    ""distanceFromSite"": 45.2,
                    ""checkInImageUrl"": ""https://s3.amazonaws.com/basms-faces/check-in/...""
                },
                ""message"": ""Check-in completed successfully""
            }

            ** ERROR RESPONSE **

            {
                ""success"": false,
                ""error"": ""❌ Khuôn mặt không khớp với dữ liệu đã đăng ký. Độ chính xác: 65.5% (yêu cầu >= 70%)"",
                ""data"": {
                    ""faceMatchScore"": 65.5,
                    ""distanceFromSite"": null
                }
            }

            ** VALIDATION REQUIREMENTS **

            - ✅ Attendance record must exist with status PENDING
            - ✅ Face template must be registered
            - ✅ Face must be detected in image
            - ✅ Face quality >= 50/100
            - ✅ Face match >= 70%
            - ✅ Distance from site <= 200m
            - ✅ Valid GPS coordinates (Lat: -90 to 90, Lon: -180 to 180)

            ** IMAGE REQUIREMENTS **

            - Format: JPG or PNG
            - Max size: 10MB per image
            - Must clearly show face from front
            - Good lighting condition recommended

            ** CURL EXAMPLE **

            curl -X POST http://localhost:5004/api/attendance/check-in \
              -F ""guardId=550e8400-e29b-41d4-a716-446655440000"" \
              -F ""shiftAssignmentId=650e8400-e29b-41d4-a716-446655440001"" \
              -F ""shiftId=750e8400-e29b-41d4-a716-446655440002"" \
              -F ""checkInImage=@checkin.jpg"" \
              -F ""checkInLatitude=10.762622"" \
              -F ""checkInLongitude=106.660172"" \
              -F ""checkInLocationAccuracy=5.0""
        ");
    }
}
