namespace Contracts.API.ContractsHandler.CreatePublicHoliday;


public class CreatePublicHolidayEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/contracts/holidays", async (
            CreatePublicHolidayRequest request,
            ISender sender,
            ILogger<CreatePublicHolidayEndpoint> logger) =>
        {
            try
            {
                logger.LogInformation("Create public holiday request: {HolidayName}", request.HolidayName);

                var command = new CreatePublicHolidayCommand
                {
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
                    logger.LogError("Failed to create holiday: {ErrorMessage}", result.ErrorMessage);
                    return Results.Problem(
                        title: "Error creating holiday",
                        detail: result.ErrorMessage,
                        statusCode: StatusCodes.Status400BadRequest
                    );
                }

                logger.LogInformation(
                    "Successfully created holiday {HolidayName} (ID: {HolidayId})",
                    result.HolidayName, result.HolidayId);

                return Results.Created(
                    $"/api/contracts/holidays/{result.HolidayId}",
                    result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing create holiday request");
                return Results.Problem(
                    title: "Error creating holiday",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status500InternalServerError
                );
            }
        })
        .RequireAuthorization()
        .WithTags("Contracts - Holidays")
        .WithName("CreatePublicHoliday")
        .Produces<CreatePublicHolidayResult>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Tạo mới public holiday");
    }
}


public record CreatePublicHolidayRequest
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
    public bool IsOfficialHoliday { get; init; } = true;
    public bool IsObserved { get; init; } = true;
    public DateTime? OriginalDate { get; init; }
    public DateTime? ObservedDate { get; init; }
    public bool AppliesNationwide { get; init; } = true;
    public string? AppliesToRegions { get; init; }
    public bool StandardWorkplacesClosed { get; init; } = true;
    public bool EssentialServicesOperating { get; init; } = true;
    public string? Description { get; init; }
    public int Year { get; init; }
}
