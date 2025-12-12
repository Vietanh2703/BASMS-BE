using Microsoft.AspNetCore.Mvc;

namespace Shifts.API.TeamsHandler.AddMemberToTeam;

/// <summary>
/// Request DTO cho AddMemberToTeam endpoint
/// </summary>
public record AddMemberToTeamRequest
{
    /// <summary>
    /// Guard ID được thêm vào team
    /// </summary>
    public Guid GuardId { get; init; }

    /// <summary>
    /// Vai trò: LEADER | DEPUTY | MEMBER
    /// </summary>
    public string Role { get; init; } = "MEMBER";

    /// <summary>
    /// Ghi chú khi gia nhập
    /// </summary>
    public string? JoiningNotes { get; init; }
}

/// <summary>
/// Response DTO cho AddMemberToTeam endpoint
/// </summary>
public record AddMemberToTeamResponse
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public TeamMemberData? Member { get; init; }
}

public record TeamMemberData
{
    public Guid TeamMemberId { get; init; }
    public Guid TeamId { get; init; }
    public string TeamCode { get; init; } = string.Empty;
    public Guid GuardId { get; init; }
    public string GuardName { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
}

public class AddMemberToTeamEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/shifts/teams/{teamId}/members", AddMemberAsync)
            .WithName("AddMemberToTeam")
            .WithTags("Teams")
            .WithSummary("Thêm guard vào team")
            .WithDescription(@"
Thêm guard vào team với vai trò cụ thể.

**Validation Rules:**
1. Team phải tồn tại và active
2. Team chưa vượt quá MaxMembers
3. Guard phải tồn tại, active, và có EmploymentStatus = ACTIVE/PROBATION
4. Guard chưa là thành viên của team này
5. Role phải là: LEADER, DEPUTY, hoặc MEMBER
6. LEADER/DEPUTY nên có CertificationLevel II hoặc III (warning nếu không đạt)

**Example Request:**
```json
{
  ""guardId"": ""3fa85f64-5717-4562-b3fc-2c963f66afa6"",
  ""role"": ""LEADER"",
  ""joiningNotes"": ""Chuyển từ Team B sang Team A""
}
```

**Role Guidelines:**
- LEADER: Chỉ huy team (nên có Level II hoặc III)
- DEPUTY: Phó chỉ huy (nên có Level II hoặc III)
- MEMBER: Thành viên thường (Level I trở lên)
            ")
            .Produces<AddMemberToTeamResponse>(StatusCodes.Status201Created)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);
    }

    private static async Task<IResult> AddMemberAsync(
        Guid teamId,
        [FromBody] AddMemberToTeamRequest request,
        [FromHeader(Name = "X-User-Id")] Guid? userId,
        ISender sender,
        ILogger<AddMemberToTeamEndpoint> logger)
    {
        try
        {
            // Validate user authentication
            if (!userId.HasValue)
            {
                logger.LogWarning("AddMemberToTeam request without authenticated user");
                return Results.BadRequest(new AddMemberToTeamResponse
                {
                    Success = false,
                    Message = "User authentication required (X-User-Id header missing)"
                });
            }

            logger.LogInformation(
                "AddMemberToTeam request from user {UserId}: Guard {GuardId} → Team {TeamId} as {Role}",
                userId.Value,
                request.GuardId,
                teamId,
                request.Role);

            // Create command
            var command = new AddMemberToTeamCommand(
                TeamId: teamId,
                GuardId: request.GuardId,
                Role: request.Role,
                JoiningNotes: request.JoiningNotes,
                CreatedBy: userId.Value
            );

            // Execute command
            var result = await sender.Send(command);

            logger.LogInformation(
                "✓ Guard {GuardName} added to team {TeamCode} as {Role}",
                result.GuardName,
                result.TeamCode,
                result.Role);

            // Return success response
            var response = new AddMemberToTeamResponse
            {
                Success = true,
                Message = $"Guard {result.GuardName} đã được thêm vào team {result.TeamCode} với vai trò {result.Role}",
                Member = new TeamMemberData
                {
                    TeamMemberId = result.TeamMemberId,
                    TeamId = result.TeamId,
                    TeamCode = result.TeamCode,
                    GuardId = result.GuardId,
                    GuardName = result.GuardName,
                    Role = result.Role
                }
            };

            return Results.Created($"/teams/{teamId}/members/{result.TeamMemberId}", response);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Validation error adding member to team");
            return Results.BadRequest(new AddMemberToTeamResponse
            {
                Success = false,
                Message = ex.Message
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error adding member to team");
            return Results.Problem(
                title: "Error adding member to team",
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError
            );
        }
    }
}
