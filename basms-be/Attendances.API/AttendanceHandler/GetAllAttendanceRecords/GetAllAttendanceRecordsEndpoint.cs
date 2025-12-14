namespace Attendances.API.AttendanceHandler.GetAllAttendanceRecords;

/// <summary>
/// Endpoint để lấy danh sách tất cả attendance records với filtering
/// </summary>
public class GetAllAttendanceRecordsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/attendances/get-all", async (
    [FromQuery] Guid? shiftId,
    [FromQuery] Guid? guardId,
    [FromQuery] Guid? shiftAssignmentId,
    [FromQuery] DateTime? fromDate,
    [FromQuery] DateTime? toDate,
    [FromQuery] string? status,
    [FromQuery] bool? isLate,
    [FromQuery] bool? isEarlyLeave,
    [FromQuery] bool? hasOvertime,
    [FromQuery] bool? isVerified,
    [FromQuery] string? verificationStatus,
    ISender sender,
    ILogger<GetAllAttendanceRecordsEndpoint> logger,
    CancellationToken cancellationToken) =>
{
    logger.LogInformation(
        "GET /api/attendances/get-all - Getting all attendance records");

    var query = new GetAllAttendanceRecordsQuery(
        ShiftId: shiftId,
        GuardId: guardId,
        ShiftAssignmentId: shiftAssignmentId,
        FromDate: fromDate,
        ToDate: toDate,
        Status: status,
        IsLate: isLate,
        IsEarlyLeave: isEarlyLeave,
        HasOvertime: hasOvertime,
        IsVerified: isVerified,
        VerificationStatus: verificationStatus
    );

    var result = await sender.Send(query, cancellationToken);

    if (!result.Success)
    {
        logger.LogWarning(
            "Failed to get attendance records: {Error}",
            result.ErrorMessage);

        return Results.BadRequest(new
        {
            success = false,
            error = result.ErrorMessage
        });
    }

    logger.LogInformation(
        "✓ Retrieved {Count} attendance records",
        result.Records.Count);

    return Results.Ok(new
    {
        success = true,
        data = result.Records,
        totalCount = result.TotalCount,
        message = "Attendance records sorted by check-in time (newest first)",
        filters = new
        {
            shiftId = shiftId?.ToString() ?? "all",
            guardId = guardId?.ToString() ?? "all",
            shiftAssignmentId = shiftAssignmentId?.ToString() ?? "all",
            fromDate = fromDate?.ToString("yyyy-MM-dd") ?? "all",
            toDate = toDate?.ToString("yyyy-MM-dd") ?? "all",
            status = status ?? "all",
            isLate = isLate?.ToString() ?? "all",
            isEarlyLeave = isEarlyLeave?.ToString() ?? "all",
            hasOvertime = hasOvertime?.ToString() ?? "all",
            isVerified = isVerified?.ToString() ?? "all",
            verificationStatus = verificationStatus ?? "all"
        }
    });
})
        // .RequireAuthorization()
        .WithName("GetAllAttendanceRecords")
        .WithTags("Attendances")
        .Produces(200)
        .Produces(400)
        .WithSummary("Get all attendance records with filtering")
        .WithDescription(@"
            Returns all attendance records sorted by check-in time (newest first).

            Query Parameters:
            - shiftId (optional): Filter by shift ID
            - guardId (optional): Filter by guard ID
            - shiftAssignmentId (optional): Filter by shift assignment ID
            - fromDate (optional): Filter records from this date (yyyy-MM-dd)
            - toDate (optional): Filter records until this date (yyyy-MM-dd)
            - status (optional): Filter by status (CHECKED_IN, CHECKED_OUT, INCOMPLETE, LATE_CHECKIN, EARLY_CHECKOUT)
            - isLate (optional): Filter by late check-in (true/false)
            - isEarlyLeave (optional): Filter by early leave (true/false)
            - hasOvertime (optional): Filter by overtime (true/false)
            - isVerified (optional): Filter by verification status (true/false)
            - verificationStatus (optional): Filter by verification status (PENDING, APPROVED, REJECTED)

            Examples:
            GET /api/attendances/get-all
            GET /api/attendances/get-all?shiftId={guid}
            GET /api/attendances/get-all?guardId={guid}
            GET /api/attendances/get-all?fromDate=2025-01-01&toDate=2025-01-31
            GET /api/attendances/get-all?status=CHECKED_OUT
            GET /api/attendances/get-all?isLate=true
            GET /api/attendances/get-all?verificationStatus=PENDING
        ");
    }
}
