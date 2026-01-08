namespace Shifts.API.Utilities;

public static class EndpointHelpers
{
    public static RouteHandlerBuilder AddStandardEndpointDocumentation(
        this RouteHandlerBuilder builder,
        string tag,
        string name,
        string summary,
        string? description = null,
        bool requiresAuth = true)
    {
        builder
            .WithTags(tag)
            .WithName(name)
            .WithSummary(summary);

        if (!string.IsNullOrEmpty(description))
        {
            builder.WithDescription(description);
        }

        builder
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        if (requiresAuth)
        {
            builder
                .RequireAuthorization()
                .ProducesProblem(StatusCodes.Status401Unauthorized)
                .ProducesProblem(StatusCodes.Status403Forbidden);
        }

        return builder;
    }

    public static RouteHandlerBuilder AddStandardPostDocumentation<TResponse>(
        this RouteHandlerBuilder builder,
        string tag,
        string name,
        string summary,
        string? description = null,
        bool requiresAuth = true)
    {
        builder.AddStandardEndpointDocumentation(tag, name, summary, description, requiresAuth);

        builder
            .Produces<TResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return builder;
    }

    public static RouteHandlerBuilder AddStandardGetDocumentation<TResponse>(
        this RouteHandlerBuilder builder,
        string tag,
        string name,
        string summary,
        string? description = null,
        bool requiresAuth = true,
        bool canReturnNotFound = true)
    {
        builder.AddStandardEndpointDocumentation(tag, name, summary, description, requiresAuth);

        builder.Produces<TResponse>(StatusCodes.Status200OK);

        if (canReturnNotFound)
        {
            builder.ProducesProblem(StatusCodes.Status404NotFound);
        }

        return builder;
    }

    public static RouteHandlerBuilder AddStandardPutDocumentation<TResponse>(
        this RouteHandlerBuilder builder,
        string tag,
        string name,
        string summary,
        string? description = null,
        bool requiresAuth = true)
    {
        builder.AddStandardEndpointDocumentation(tag, name, summary, description, requiresAuth);

        builder
            .Produces<TResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return builder;
    }

    public static RouteHandlerBuilder AddStandardDeleteDocumentation<TResponse>(
        this RouteHandlerBuilder builder,
        string tag,
        string name,
        string summary,
        string? description = null,
        bool requiresAuth = true)
    {
        builder.AddStandardEndpointDocumentation(tag, name, summary, description, requiresAuth);

        builder
            .Produces<TResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return builder;
    }

    public static RouteHandlerBuilder AddStandardPatchDocumentation<TResponse>(
        this RouteHandlerBuilder builder,
        string tag,
        string name,
        string summary,
        string? description = null,
        bool requiresAuth = true)
    {
        builder.AddStandardEndpointDocumentation(tag, name, summary, description, requiresAuth);

        builder
            .Produces<TResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return builder;
    }
    
    public static Guid GetUserIdFromContext(this HttpContext context)
    {
        var userIdClaim = context.User.FindFirst("userId")?.Value;
        if (!string.IsNullOrEmpty(userIdClaim) && Guid.TryParse(userIdClaim, out var userId))
        {
            return userId;
        }

        return Guid.NewGuid();
    }
}
