using Carter;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Attendances.API.AttendanceHandler.GetAttendanceStatusByGuardId;


public class GetAttendanceStatusByGuardIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/attendances/status", async (
            [FromQuery] Guid guardId,
            [FromQuery] Guid shiftId,
            ISender sender,
            ILogger<GetAttendanceStatusByGuardIdEndpoint> logger,
            CancellationToken cancellationToken) =>
        {
            logger.LogInformation(
                "GET /api/attendances/status - Getting attendance status for Guard {GuardId} and Shift {ShiftId}",
                guardId,
                shiftId);

            var query = new GetAttendanceStatusByGuardIdQuery(
                GuardId: guardId,
                ShiftId: shiftId
            );

            var result = await sender.Send(query, cancellationToken);

            if (!result.Success)
            {
                logger.LogWarning(
                    "Failed to get attendance status for Guard {GuardId} and Shift {ShiftId}: {Error}",
                    guardId,
                    shiftId,
                    result.ErrorMessage);

                return Results.BadRequest(new
                {
                    success = false,
                    error = result.ErrorMessage
                });
            }
            
            if (result.Status == null)
            {
                logger.LogInformation(
                    "No attendance record found for Guard {GuardId} and Shift {ShiftId}",
                    guardId,
                    shiftId);

                return Results.Ok(new
                {
                    success = true,
                    data = new
                    {
                        attendanceId = (Guid?)null,
                        guardId = result.GuardId,
                        shiftId = result.ShiftId,
                        status = (string?)null,
                        verificationStatus = (string?)null
                    },
                    message = "No attendance record found"
                });
            }

            logger.LogInformation(
                "✓ Found attendance status: {Status} for Guard {GuardId} and Shift {ShiftId}",
                result.Status,
                guardId,
                shiftId);

            return Results.Ok(new
            {
                success = true,
                data = new
                {
                    attendanceId = result.AttendanceId,
                    guardId = result.GuardId,
                    shiftId = result.ShiftId,
                    status = result.Status,
                    verificationStatus = result.VerificationStatus
                }
            });
        })
        .RequireAuthorization()
        .WithName("GetAttendanceStatusByGuardId")
        .WithTags("Attendances")
        .Produces(200)
        .Produces(400)
        .Produces(401)
        .WithSummary("Lấy status của attendance record theo GuardId và ShiftId");
    }
}
