namespace Attendances.API.AttendanceHandler.GetAllAttendanceRecords;

/// <summary>
/// Endpoint để lấy danh sách tất cả attendance records
/// </summary>
public class GetAllAttendanceRecordsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/attendances/get-all", async (
    ISender sender,
    ILogger<GetAllAttendanceRecordsEndpoint> logger,
    CancellationToken cancellationToken) =>
{
    logger.LogInformation(
        "GET /api/attendances/get-all - Getting all attendance records");

    var query = new GetAllAttendanceRecordsQuery();

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
        message = "Attendance records sorted by check-in time (newest first)"
    });
})
        // .RequireAuthorization()
        .WithName("GetAllAttendanceRecords")
        .WithTags("Attendances")
        .Produces(200)
        .Produces(400)
        .WithSummary("Get all attendance records")
        .WithDescription(@"
            Returns all attendance records sorted by check-in time (newest first).

            Examples:
            GET /api/attendances/get-all
        ");
    }
}
