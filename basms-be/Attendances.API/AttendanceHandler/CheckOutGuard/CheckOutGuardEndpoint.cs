namespace Attendances.API.AttendanceHandler.CheckOutGuard;

/// <summary>
/// Endpoint để guard check-out với face verification và location validation
/// </summary>
public class CheckOutGuardEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/attendances/check-out", async (
                HttpRequest httpRequest,
                ISender sender,
                ILogger<CheckOutGuardEndpoint> logger,
                CancellationToken cancellationToken) =>
            {
                logger.LogInformation("POST /api/attendance/check-out - Processing check-out with face verification");

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

                // Get CheckOutLatitude
                if (!double.TryParse(form["checkOutLatitude"].ToString(), out var checkOutLatitude))
                {
                    return Results.BadRequest(new
                    {
                        success = false,
                        error = "CheckOutLatitude is required and must be a valid number"
                    });
                }

                // Get CheckOutLongitude
                if (!double.TryParse(form["checkOutLongitude"].ToString(), out var checkOutLongitude))
                {
                    return Results.BadRequest(new
                    {
                        success = false,
                        error = "CheckOutLongitude is required and must be a valid number"
                    });
                }

                // Validate GPS coordinates range
                if (checkOutLatitude < -90 || checkOutLatitude > 90)
                {
                    return Results.BadRequest(new
                    {
                        success = false,
                        error = "Latitude must be between -90 and 90"
                    });
                }

                if (checkOutLongitude < -180 || checkOutLongitude > 180)
                {
                    return Results.BadRequest(new
                    {
                        success = false,
                        error = "Longitude must be between -180 and 180"
                    });
                }

                // Get optional CheckOutLocationAccuracy
                float? checkOutLocationAccuracy = null;
                if (float.TryParse(form["checkOutLocationAccuracy"].ToString(), out var accuracy))
                {
                    checkOutLocationAccuracy = accuracy;
                }

                // Get CheckOutImage file
                var checkOutImage = form.Files.GetFile("checkOutImage");
                if (checkOutImage == null || checkOutImage.Length == 0)
                {
                    return Results.BadRequest(new
                    {
                        success = false,
                        error = "CheckOutImage is required",
                        hint = "Required field: checkOutImage (JPG or PNG, max 10MB)"
                    });
                }

                // Validate file size (max 10MB)
                if (checkOutImage.Length > 10 * 1024 * 1024)
                {
                    return Results.BadRequest(new
                    {
                        success = false,
                        error = "Image file too large. Maximum size: 10MB"
                    });
                }

                // Validate file type
                var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png" };
                if (!allowedTypes.Contains(checkOutImage.ContentType.ToLower()))
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

                var command = new CheckOutGuardCommand(
                    GuardId: guardId,
                    ShiftAssignmentId: shiftAssignmentId,
                    ShiftId: shiftId,
                    CheckOutImage: checkOutImage!,
                    CheckOutLatitude: checkOutLatitude,
                    CheckOutLongitude: checkOutLongitude,
                    CheckOutLocationAccuracy: checkOutLocationAccuracy
                );

                var result = await sender.Send(command, cancellationToken);

                if (!result.Success)
                {
                    logger.LogWarning(
                        "Failed to check-out: {Error}",
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
                    "✓ Check-out successful: GuardId={GuardId}, AttendanceRecord={AttendanceRecordId}, EarlyLeave={IsEarlyLeave}, FaceMatch={FaceMatchScore}%",
                    guardId,
                    result.AttendanceRecordId,
                    result.IsEarlyLeave,
                    result.FaceMatchScore);

                return Results.Ok(new
                {
                    success = true,
                    data = new
                    {
                        guardId = guardId,
                        attendanceRecordId = result.AttendanceRecordId,
                        checkOutTime = result.CheckOutTime?.ToString("yyyy-MM-dd HH:mm:ss"),
                        isEarlyLeave = result.IsEarlyLeave,
                        earlyLeaveMinutes = result.EarlyLeaveMinutes,
                        hasOvertime = result.HasOvertime,
                        overtimeMinutes = result.OvertimeMinutes,
                        actualWorkDurationMinutes = result.ActualWorkDurationMinutes,
                        totalHours = result.TotalHours,
                        faceMatchScore = result.FaceMatchScore,
                        distanceFromSite = Math.Round(result.DistanceFromSite, 2),
                        checkOutImageUrl = result.CheckOutImageUrl
                    },
                    message = result.Message
                });
            })
            .DisableAntiforgery()
            .RequireAuthorization()
            .WithName("CheckOutGuard")
            .WithTags("Attendance")
            .Produces(200)
            .Produces(400)
            .WithSummary("Guard check-out with face verification and location validation")
            .WithDescription(@"
            Guard check-out endpoint using multipart/form-data with face verification and GPS location validation.
            Validates face match, location distance, calculates work duration, overtime, and early leave.

            ** POSTMAN USAGE **

            1. Set request type: POST
            2. URL: http://localhost:5004/api/attendance/check-out
            3. Go to 'Body' tab
            4. Select 'form-data'
            5. Add the following fields:

            | KEY                       | TYPE | VALUE                          |
            |---------------------------|------|--------------------------------|
            | guardId                   | Text | {your-guard-uuid}              |
            | shiftAssignmentId         | Text | {shift-assignment-uuid}        |
            | shiftId                   | Text | {shift-uuid}                   |
            | checkOutImage             | File | [Select check-out photo]       |
            | checkOutLatitude          | Text | 10.762622                      |
            | checkOutLongitude         | Text | 106.660172                     |
            | checkOutLocationAccuracy  | Text | 5.0 (optional)                 |

            ** CHECK-OUT PROCESSING **

            The handler processes check-out in these steps:
            1. ✓ Validate attendance record exists (status = CHECKED_IN)
            2. ✓ Validate face template registered
            3. ✓ Verify face with Python Face Recognition API
            4. ✓ Upload check-out image to AWS S3
            5. ✓ Get shift location from Shifts.API
            6. ✓ Validate distance from site (<= 200m)
            7. ✓ Calculate work duration and total hours
            8. ✓ Check early leave status
            9. ✓ Check overtime status
            10. ✓ Update attendance record with CHECKED_OUT status
            11. ✓ Publish GuardCheckedOutEvent to Shifts.API
            12. ✅ Complete

            ** RESPONSE **

            {
                ""success"": true,
                ""data"": {
                    ""guardId"": ""550e8400-e29b-41d4-a716-446655440000"",
                    ""attendanceRecordId"": ""guid"",
                    ""checkOutTime"": ""2025-12-19 17:30:00"",
                    ""isEarlyLeave"": false,
                    ""earlyLeaveMinutes"": 0,
                    ""hasOvertime"": true,
                    ""overtimeMinutes"": 30,
                    ""actualWorkDurationMinutes"": 540,
                    ""totalHours"": 8.50,
                    ""faceMatchScore"": 95.5,
                    ""distanceFromSite"": 45.2,
                    ""checkOutImageUrl"": ""https://s3.amazonaws.com/basms-faces/check-out/...""
                },
                ""message"": ""Check-out completed successfully. Shift completed.""
            }

            ** VALIDATION REQUIREMENTS **

            - ✅ Attendance record must exist with status CHECKED_IN
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

            curl -X POST http://localhost:5004/api/attendance/check-out \
              -F ""guardId=550e8400-e29b-41d4-a716-446655440000"" \
              -F ""shiftAssignmentId=650e8400-e29b-41d4-a716-446655440001"" \
              -F ""shiftId=750e8400-e29b-41d4-a716-446655440002"" \
              -F ""checkOutImage=@checkout.jpg"" \
              -F ""checkOutLatitude=10.762622"" \
              -F ""checkOutLongitude=106.660172"" \
              -F ""checkOutLocationAccuracy=5.0""
        ");
    }
}
