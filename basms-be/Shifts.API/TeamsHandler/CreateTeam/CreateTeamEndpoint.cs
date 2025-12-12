using Microsoft.AspNetCore.Mvc;

namespace Shifts.API.TeamsHandler.CreateTeam;

/// <summary>
/// Request DTO cho CreateTeam endpoint
/// </summary>
public record CreateTeamRequest
{
    /// <summary>
    /// Manager quản lý team
    /// </summary>
    public Guid ManagerId { get; init; }

    /// <summary>
    /// Tên team: "Đội Bảo Vệ Khu A - Ca Ngày"
    /// </summary>
    public string TeamName { get; init; } = string.Empty;

    /// <summary>
    /// Chuyên môn: RESIDENTIAL | COMMERCIAL | EVENT | VIP | INDUSTRIAL
    /// </summary>
    public string? Specialization { get; init; }

    /// <summary>
    /// Mô tả team
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Số guards tối thiểu (mặc định: 1)
    /// </summary>
    public int MinMembers { get; init; } = 1;

    /// <summary>
    /// Số guards tối đa (optional)
    /// </summary>
    public int? MaxMembers { get; init; }
}

/// <summary>
/// Response DTO cho CreateTeam endpoint
/// </summary>
public record CreateTeamResponse
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public TeamData? Team { get; init; }
}

public record TeamData
{
    public Guid TeamId { get; init; }
    public string TeamCode { get; init; } = string.Empty;
    public string TeamName { get; init; } = string.Empty;
    public Guid ManagerId { get; init; }
    public string? Specialization { get; init; }
    public int MinMembers { get; init; }
    public int? MaxMembers { get; init; }
    public int CurrentMemberCount { get; init; }
}

public class CreateTeamEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/shifts/teams", CreateTeamAsync)
            .WithName("CreateTeam")
            .WithTags("Teams")
            .WithSummary("Tạo team mới")
            .WithDescription(@"
Tạo team mới với các thông tin:
- Team code tự động generate: T-xxxxxx (6 số random)
- Manager quản lý team
- Số lượng members min/max
- Chuyên môn (optional)

**Validation Rules:**
- Manager phải tồn tại và active
- TeamName không được rỗng
- MinMembers >= 1
- MaxMembers >= MinMembers (nếu có)
- Specialization phải thuộc: RESIDENTIAL, COMMERCIAL, EVENT, VIP, INDUSTRIAL

**Example Request:**
```json
{
  ""managerId"": ""3fa85f64-5717-4562-b3fc-2c963f66afa6"",
  ""teamName"": ""Đội Bảo Vệ Khu A - Ca Ngày"",
  ""specialization"": ""RESIDENTIAL"",
  ""description"": ""Team chuyên trách khu dân cư, ca ngày"",
  ""minMembers"": 2,
  ""maxMembers"": 10
}
```
            ")
            .Produces<CreateTeamResponse>(StatusCodes.Status201Created)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);
    }

    private static async Task<IResult> CreateTeamAsync(
        [FromBody] CreateTeamRequest request,
        [FromHeader(Name = "X-User-Id")] Guid? userId,
        ISender sender,
        ILogger<CreateTeamEndpoint> logger)
    {
        try
        {
            // Validate user authentication
            if (!userId.HasValue)
            {
                logger.LogWarning("CreateTeam request without authenticated user");
                return Results.BadRequest(new CreateTeamResponse
                {
                    Success = false,
                    Message = "User authentication required (X-User-Id header missing)"
                });
            }

            logger.LogInformation(
                "CreateTeam request received from user {UserId}: {TeamName}",
                userId.Value,
                request.TeamName);

            // Create command
            var command = new CreateTeamCommand(
                ManagerId: request.ManagerId,
                TeamName: request.TeamName,
                Specialization: request.Specialization,
                Description: request.Description,
                MinMembers: request.MinMembers,
                MaxMembers: request.MaxMembers,
                CreatedBy: userId.Value
            );

            // Execute command
            var result = await sender.Send(command);

            logger.LogInformation(
                "✓ Team created successfully: {TeamCode} ({TeamId})",
                result.TeamCode,
                result.TeamId);

            // Return success response
            var response = new CreateTeamResponse
            {
                Success = true,
                Message = $"Team '{result.TeamName}' được tạo thành công với mã {result.TeamCode}",
                Team = new TeamData
                {
                    TeamId = result.TeamId,
                    TeamCode = result.TeamCode,
                    TeamName = result.TeamName,
                    ManagerId = request.ManagerId,
                    Specialization = request.Specialization,
                    MinMembers = request.MinMembers,
                    MaxMembers = request.MaxMembers,
                    CurrentMemberCount = 0
                }
            };

            return Results.Created($"/teams/{result.TeamId}", response);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Validation error creating team");
            return Results.BadRequest(new CreateTeamResponse
            {
                Success = false,
                Message = ex.Message
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating team");
            return Results.Problem(
                title: "Error creating team",
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError
            );
        }
    }
}
