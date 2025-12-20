namespace Users.API.UsersHandler.ActivateUser;

public record ActivateUserRequest
{
    public Guid UserId { get; init; }
}


public class ActivateUserEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/users/{userId}/activate", async (
                Guid userId,
                ISender sender,
                HttpContext httpContext,
                ILogger<ActivateUserEndpoint> logger) =>
            {
                try
                {
                    logger.LogInformation("Received activate user request for UserId: {UserId}", userId);
                    
                    Guid? activatedBy = null;
                    if (httpContext.Items.TryGetValue("UserId", out var userIdObj) && userIdObj is Guid currentUserId)
                    {
                        activatedBy = currentUserId;
                    }
                    
                    var command = new ActivateUserCommand(
                        UserId: userId,
                        ActivatedBy: activatedBy
                    );
                    
                    var result = await sender.Send(command);

                    if (result.Success)
                    {
                        logger.LogInformation(
                            "Successfully activated user {UserId} - {Email}",
                            result.UserId,
                            result.Email);

                        return Results.Ok(new
                        {
                            success = true,
                            message = result.Message,
                            data = new
                            {
                                userId = result.UserId,
                                email = result.Email,
                                fullName = result.FullName,
                                isActive = true
                            }
                        });
                    }
                    else
                    {
                        logger.LogWarning(
                            "Failed to activate user {UserId}: {Message}",
                            userId,
                            result.Message);

                        return Results.BadRequest(new
                        {
                            success = false,
                            message = result.Message
                        });
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error in activate user endpoint for UserId: {UserId}", userId);
                    return Results.Problem(
                        title: "Error activating user",
                        detail: ex.Message,
                        statusCode: StatusCodes.Status500InternalServerError
                    );
                }
            })
            .WithTags("Users")
            .WithName("ActivateUser")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
        
        app.MapPost("/api/users/activate", async (
                [FromBody] ActivateUserRequest request,
                ISender sender,
                HttpContext httpContext,
                ILogger<ActivateUserEndpoint> logger) =>
            {
                try
                {
                    logger.LogInformation("Received activate user request for UserId: {UserId}", request.UserId);
                    
                    Guid? activatedBy = null;
                    if (httpContext.Items.TryGetValue("UserId", out var userIdObj) && userIdObj is Guid currentUserId)
                    {
                        activatedBy = currentUserId;
                    }

                    var command = new ActivateUserCommand(
                        UserId: request.UserId,
                        ActivatedBy: activatedBy
                    );

                    var result = await sender.Send(command);

                    if (result.Success)
                    {
                        return Results.Ok(new
                        {
                            success = true,
                            message = result.Message,
                            data = new
                            {
                                userId = result.UserId,
                                email = result.Email,
                                fullName = result.FullName,
                                isActive = true
                            }
                        });
                    }
                    else
                    {
                        return Results.BadRequest(new
                        {
                            success = false,
                            message = result.Message
                        });
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error activating user");
                    return Results.Problem(
                        title: "Error activating user",
                        detail: ex.Message,
                        statusCode: StatusCodes.Status500InternalServerError
                    );
                }
            })
            .WithTags("Users")
            .WithName("ActivateUserWithBody")
            .Accepts<ActivateUserRequest>("application/json")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
