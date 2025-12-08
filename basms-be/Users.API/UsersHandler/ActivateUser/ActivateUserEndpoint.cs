using Microsoft.AspNetCore.Mvc;

namespace Users.API.UsersHandler.ActivateUser;

/// <summary>
/// Request để activate user
/// </summary>
public record ActivateUserRequest
{
    public Guid UserId { get; init; }
}

/// <summary>
/// Endpoint để kích hoạt user (set IsActive = true)
/// Chỉ admin/manager mới có quyền gọi endpoint này
/// </summary>
public class ActivateUserEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // Route: PUT /api/users/{userId}/activate
        app.MapPut("/api/users/{userId}/activate", async (
                Guid userId,
                ISender sender,
                HttpContext httpContext,
                ILogger<ActivateUserEndpoint> logger) =>
            {
                try
                {
                    logger.LogInformation("Received activate user request for UserId: {UserId}", userId);

                    // Lấy ID của user đang thực hiện activate (từ JWT token)
                    Guid? activatedBy = null;
                    if (httpContext.Items.TryGetValue("UserId", out var userIdObj) && userIdObj is Guid currentUserId)
                    {
                        activatedBy = currentUserId;
                    }

                    // Tạo command
                    var command = new ActivateUserCommand(
                        UserId: userId,
                        ActivatedBy: activatedBy
                    );

                    // Gửi command đến handler
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
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .WithSummary("Kích hoạt user (set IsActive = true)")
            .WithDescription("Endpoint này cho phép admin/manager kích hoạt user đã được tạo nhưng chưa active. " +
                           "User cần được kích hoạt trước khi có thể login vào hệ thống.");
            // .RequireAuthorization("AdminOnly"); // Uncomment để yêu cầu admin role

        // Route bổ sung: POST /api/users/activate (với body)
        app.MapPost("/api/users/activate", async (
                [FromBody] ActivateUserRequest request,
                ISender sender,
                HttpContext httpContext,
                ILogger<ActivateUserEndpoint> logger) =>
            {
                try
                {
                    logger.LogInformation("Received activate user request for UserId: {UserId}", request.UserId);

                    // Lấy ID của user đang thực hiện activate
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
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .WithSummary("Kích hoạt user (POST với body)")
            .WithDescription("Alternative endpoint để activate user bằng POST request với body JSON");
            // .RequireAuthorization("AdminOnly");
    }
}
