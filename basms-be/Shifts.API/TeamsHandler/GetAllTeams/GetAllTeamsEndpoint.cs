using Microsoft.AspNetCore.Mvc;

namespace Shifts.API.TeamsHandler.GetAllTeams;

public class GetAllTeamsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/shifts/teams", GetAllTeamsAsync)
            .WithName("GetAllTeams")
            .WithTags("Teams")
            .WithSummary("Lấy danh sách teams")
            .WithDescription(@"
Lấy danh sách teams với các filter và pagination:

**Query Parameters:**
- `managerId` (optional): Filter teams theo manager
- `specialization` (optional): Filter theo chuyên môn (RESIDENTIAL, COMMERCIAL, EVENT, VIP, INDUSTRIAL)
- `isActive` (optional): Filter theo trạng thái active (true/false)
- `pageNumber` (default: 1): Trang hiện tại
- `pageSize` (default: 20): Số items mỗi trang

**Example:**
```
GET /teams?managerId=xxx&specialization=RESIDENTIAL&isActive=true&pageNumber=1&pageSize=20
```

**Response includes:**
- List of teams
- Pagination metadata (totalCount, totalPages, etc.)
            ")
            .Produces<GetAllTeamsResult>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);
    }

    private static async Task<IResult> GetAllTeamsAsync(
        ISender sender,
        ILogger<GetAllTeamsEndpoint> logger,
        [FromQuery] Guid? managerId = null,
        [FromQuery] string? specialization = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            // Validate pagination
            if (pageNumber < 1)
            {
                return Results.BadRequest(new ProblemDetails
                {
                    Title = "Invalid pagination",
                    Detail = "pageNumber phải >= 1",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            if (pageSize < 1 || pageSize > 100)
            {
                return Results.BadRequest(new ProblemDetails
                {
                    Title = "Invalid pagination",
                    Detail = "pageSize phải từ 1 đến 100",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            logger.LogInformation(
                "GetAllTeams request: ManagerId={ManagerId}, Specialization={Specialization}, IsActive={IsActive}, Page={PageNumber}/{PageSize}",
                managerId,
                specialization,
                isActive,
                pageNumber,
                pageSize);

            var query = new GetAllTeamsQuery(
                ManagerId: managerId,
                Specialization: specialization,
                IsActive: isActive,
                PageNumber: pageNumber,
                PageSize: pageSize
            );

            var result = await sender.Send(query);

            return Results.Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting teams");
            return Results.Problem(
                title: "Error getting teams",
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError
            );
        }
    }
}
