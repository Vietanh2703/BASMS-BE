namespace Attendances.API.AttendanceHandler.CheckInGuard;


public class CheckInGuardEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/attendances/check-in", async (
                HttpRequest httpRequest,
                ISender sender,
                ILogger<CheckInGuardEndpoint> logger,
                CancellationToken cancellationToken) =>
            {
                logger.LogInformation("POST /api/attendance/check-in - Processing check-in with face verification");

                if (!httpRequest.HasFormContentType)
                {
                    return Results.BadRequest(new
                    {
                        success = false,
                        error = "Request must be multipart/form-data"
                    });
                }

                var form = await httpRequest.ReadFormAsync(cancellationToken);

                if (!Guid.TryParse(form["guardId"].ToString(), out var guardId) || guardId == Guid.Empty)
                {
                    return Results.BadRequest(new
                    {
                        success = false,
                        error = "GuardId is required and must be a valid GUID"
                    });
                }

                if (!Guid.TryParse(form["shiftAssignmentId"].ToString(), out var shiftAssignmentId) || shiftAssignmentId == Guid.Empty)
                {
                    return Results.BadRequest(new
                    {
                        success = false,
                        error = "ShiftAssignmentId is required and must be a valid GUID"
                    });
                }
                
                if (!Guid.TryParse(form["shiftId"].ToString(), out var shiftId) || shiftId == Guid.Empty)
                {
                    return Results.BadRequest(new
                    {
                        success = false,
                        error = "ShiftId is required and must be a valid GUID"
                    });
                }
                
                if (!double.TryParse(form["checkInLatitude"].ToString(), out var checkInLatitude))
                {
                    return Results.BadRequest(new
                    {
                        success = false,
                        error = "CheckInLatitude is required and must be a valid number"
                    });
                }

                if (!double.TryParse(form["checkInLongitude"].ToString(), out var checkInLongitude))
                {
                    return Results.BadRequest(new
                    {
                        success = false,
                        error = "CheckInLongitude is required and must be a valid number"
                    });
                }
                
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
                
                float? checkInLocationAccuracy = null;
                if (float.TryParse(form["checkInLocationAccuracy"].ToString(), out var accuracy))
                {
                    checkInLocationAccuracy = accuracy;
                }
                
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
                
                if (checkInImage.Length > 10 * 1024 * 1024)
                {
                    return Results.BadRequest(new
                    {
                        success = false,
                        error = "Image file too large. Maximum size: 10MB"
                    });
                }
                
                var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png" };
                if (!allowedTypes.Contains(checkInImage.ContentType.ToLower()))
                {
                    return Results.BadRequest(new
                    {
                        success = false,
                        error = "Invalid image format. Only JPG and PNG are allowed"
                    });
                }
                
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
                    "Check-in successful: GuardId={GuardId}, AttendanceRecord={AttendanceRecordId}, Late={IsLate}, FaceMatch={FaceMatchScore}%",
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
            .DisableAntiforgery()
            .RequireAuthorization()
            .WithName("CheckInGuard")
            .WithTags("Attendance")
            .Produces(200)
            .Produces(400)
            .WithSummary("Guard check-in with face verification and location validation");
    }
}
