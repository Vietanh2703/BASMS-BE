namespace Shifts.API.GuardsHandler.GetGuardById;

public class GetGuardByIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/guards/{id:guid}", async (Guid id, ISender sender) =>
        {
            var query = new GetGuardByIdQuery(id);
            var result = await sender.Send(query);
            return Results.Ok(result);
        })
        .AddStandardGetDocumentation<GetGuardByIdResult>(
            tag: "Guards",
            name: "GetGuardById",
            summary: "Get guard by ID",
            description: "Retrieves detailed information of a specific guard by their ID from cache");
    }
}