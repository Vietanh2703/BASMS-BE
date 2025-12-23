namespace Shifts.API.GuardsHandler.GetGuardByEmail;

public record GetGuardByEmailRequest(string Email);

public class GetGuardByEmailEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/shifts/guards/by-email", async (GetGuardByEmailRequest request, ISender sender) =>
        {
            if (string.IsNullOrWhiteSpace(request.Email))
            {
                return Results.BadRequest(new { success = false, error = "Email is required" });
            }
            var query = new GetGuardByEmailQuery(request.Email);
            
            var result = await sender.Send(query);
            
            return Results.Ok(result);
        })
        .RequireAuthorization()
        .WithTags("Guards")
        .WithName("GetGuardByEmail")
        .Produces<GetGuardByEmailResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Get guard by Email");
    }
}
