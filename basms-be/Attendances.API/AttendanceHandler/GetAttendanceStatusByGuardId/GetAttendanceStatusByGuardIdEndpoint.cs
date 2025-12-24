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
                "GET /api/attendances/status - Getting attendance record for Guard {GuardId} and Shift {ShiftId}",
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
                    "Failed to get attendance record for Guard {GuardId} and Shift {ShiftId}: {Error}",
                    guardId,
                    shiftId,
                    result.ErrorMessage);

                return Results.BadRequest(new
                {
                    success = false,
                    error = result.ErrorMessage
                });
            }

            if (result.Attendance == null)
            {
                logger.LogInformation(
                    "No attendance record found for Guard {GuardId} and Shift {ShiftId}",
                    guardId,
                    shiftId);

                return Results.Ok(new
                {
                    success = true,
                    data = (object?)null,
                    message = "No attendance record found"
                });
            }

            logger.LogInformation(
                "Found attendance record with status: {Status} for Guard {GuardId} and Shift {ShiftId}",
                result.Attendance.Status,
                guardId,
                shiftId);

            return Results.Ok(new
            {
                success = true,
                data = result.Attendance
            });
        })
        .RequireAuthorization()
        .WithName("GetAttendanceStatusByGuardId")
        .WithTags("Attendances")
        .Produces(200)
        .Produces(400)
        .Produces(401)
        .WithSummary("Lấy đầy đủ thông tin attendance record theo GuardId và ShiftId");
    }
}
