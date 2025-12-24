namespace Attendances.API.AttendanceHandler.CheckOutGuard;

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
                
                if (!double.TryParse(form["checkOutLatitude"].ToString(), out var checkOutLatitude))
                {
                    return Results.BadRequest(new
                    {
                        success = false,
                        error = "CheckOutLatitude is required and must be a valid number"
                    });
                }
                
                if (!double.TryParse(form["checkOutLongitude"].ToString(), out var checkOutLongitude))
                {
                    return Results.BadRequest(new
                    {
                        success = false,
                        error = "CheckOutLongitude is required and must be a valid number"
                    });
                }
                
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

                float? checkOutLocationAccuracy = null;
                if (float.TryParse(form["checkOutLocationAccuracy"].ToString(), out var accuracy))
                {
                    checkOutLocationAccuracy = accuracy;
                }
                
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
                
                if (checkOutImage.Length > 10 * 1024 * 1024)
                {
                    return Results.BadRequest(new
                    {
                        success = false,
                        error = "Image file too large. Maximum size: 10MB"
                    });
                }
                
                var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png" };
                if (!allowedTypes.Contains(checkOutImage.ContentType.ToLower()))
                {
                    return Results.BadRequest(new
                    {
                        success = false,
                        error = "Invalid image format. Only JPG and PNG are allowed"
                    });
                }


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
                    "Check-out successful: GuardId={GuardId}, AttendanceRecord={AttendanceRecordId}, EarlyLeave={IsEarlyLeave}, FaceMatch={FaceMatchScore}%",
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
            .WithSummary("Guard check-out with face verification and location validation");
    }
}
