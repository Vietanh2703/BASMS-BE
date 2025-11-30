namespace Contracts.API.ContractsHandler.CheckPublicHoliday;

public class CheckPublicHolidayEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // Route: GET /api/holidays/check?date=2025-01-01
        app.MapGet("/api/holidays/check", async (DateTime date, ISender sender) =>
        {
            var query = new CheckPublicHolidayQuery(date);
            var result = await sender.Send(query);
            return Results.Ok(result);
        })
        .WithTags("Holidays")
        .WithName("CheckPublicHoliday")
        .Produces<CheckPublicHolidayResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Check if date is public holiday")
        .WithDescription("Returns holiday information if the date is a public holiday");
    }
}
