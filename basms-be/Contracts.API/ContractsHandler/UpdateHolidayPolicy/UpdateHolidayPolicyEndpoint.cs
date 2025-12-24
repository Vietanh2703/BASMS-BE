namespace Contracts.API.ContractsHandler.UpdateHolidayPolicy;

public class UpdateHolidayPolicyEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/contracts/holidays/{holidayId}", async (
            Guid holidayId,
            UpdateHolidayPolicyRequest request,
            ISender sender,
            ILogger<UpdateHolidayPolicyEndpoint> logger) =>
        {
            try
            {
                logger.LogInformation("Update holiday policy request for ID: {HolidayId}", holidayId);
                
                var command = new UpdateHolidayPolicyCommand
                {
                    HolidayId = holidayId,
                    ContractId = request.ContractId,
                    HolidayDate = request.HolidayDate,
                    HolidayName = request.HolidayName,
                    HolidayNameEn = request.HolidayNameEn,
                    HolidayCategory = request.HolidayCategory,
                    IsTetPeriod = request.IsTetPeriod,
                    IsTetHoliday = request.IsTetHoliday,
                    TetDayNumber = request.TetDayNumber,
                    HolidayStartDate = request.HolidayStartDate,
                    HolidayEndDate = request.HolidayEndDate,
                    TotalHolidayDays = request.TotalHolidayDays,
                    IsOfficialHoliday = request.IsOfficialHoliday,
                    IsObserved = request.IsObserved,
                    OriginalDate = request.OriginalDate,
                    ObservedDate = request.ObservedDate,
                    AppliesNationwide = request.AppliesNationwide,
                    AppliesToRegions = request.AppliesToRegions,
                    StandardWorkplacesClosed = request.StandardWorkplacesClosed,
                    EssentialServicesOperating = request.EssentialServicesOperating,
                    Description = request.Description,
                    Year = request.Year
                };

                var result = await sender.Send(command);

                if (!result.Success)
                {
                    logger.LogError("Failed to update holiday policy: {ErrorMessage}", result.ErrorMessage);
                    return Results.Problem(
                        title: "Error updating holiday policy",
                        detail: result.ErrorMessage,
                        statusCode: StatusCodes.Status400BadRequest
                    );
                }

                logger.LogInformation(
                    "Successfully updated holiday {HolidayName} (ID: {HolidayId})",
                    result.HolidayName, result.HolidayId);

                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing update holiday policy request for ID: {HolidayId}", holidayId);
                return Results.Problem(
                    title: "Error updating holiday policy",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status500InternalServerError
                );
            }
        })
        .RequireAuthorization()
        .WithTags("Contracts - Holidays")
        .WithName("UpdateHolidayPolicy")
        .Produces<UpdateHolidayPolicyResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Update thông tin public holiday policy");
    }
}


public record UpdateHolidayPolicyRequest
{
    public Guid? ContractId { get; init; }
    public DateTime HolidayDate { get; init; }
    public string HolidayName { get; init; } = string.Empty;
    public string? HolidayNameEn { get; init; }
    public string HolidayCategory { get; init; } = string.Empty;
    public bool IsTetPeriod { get; init; }
    public bool IsTetHoliday { get; init; }
    public int? TetDayNumber { get; init; }
    public DateTime? HolidayStartDate { get; init; }
    public DateTime? HolidayEndDate { get; init; }
    public int? TotalHolidayDays { get; init; }
    public bool IsOfficialHoliday { get; init; }
    public bool IsObserved { get; init; }
    public DateTime? OriginalDate { get; init; }
    public DateTime? ObservedDate { get; init; }
    public bool AppliesNationwide { get; init; }
    public string? AppliesToRegions { get; init; }
    public bool StandardWorkplacesClosed { get; init; }
    public bool EssentialServicesOperating { get; init; }
    public string? Description { get; init; }
    public int Year { get; init; }
}
