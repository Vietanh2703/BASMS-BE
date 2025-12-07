// Endpoint API để lấy danh sách tất cả wage rates
// Trả về mức tiền công chuẩn theo cấp bậc từ database
namespace Shifts.API.WageRatesHandler.GetAllWageRates;

public class GetAllWageRatesEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/shifts/wage-rates", async (ISender sender) =>
        {
            var query = new GetAllWageRatesQuery();
            var result = await sender.Send(query);
            return Results.Ok(result);
        })
        .RequireAuthorization()
        .WithTags("WageRates")
        .WithName("GetAllWageRates")
        .Produces<GetAllWageRatesResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Get all wage rates")
        .WithDescription("Retrieves all active wage rates (mức tiền công chuẩn theo cấp bậc bảo vệ) from the database");
    }
}
