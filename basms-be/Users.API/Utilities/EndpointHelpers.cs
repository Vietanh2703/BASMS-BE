using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Users.API.Utilities;

/// <summary>
/// Helper methods để setup endpoints một cách clean và consistent
/// </summary>
public static class EndpointHelpers
{
    /// <summary>
    /// Add standard endpoint documentation với common status codes
    /// </summary>
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

    /// <summary>
    /// Add standard POST endpoint documentation (201 Created)
    /// </summary>
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

    /// <summary>
    /// Add standard GET endpoint documentation (200 OK)
    /// </summary>
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

    /// <summary>
    /// Add standard PUT endpoint documentation (200 OK)
    /// </summary>
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

    /// <summary>
    /// Add standard DELETE endpoint documentation (200 OK)
    /// </summary>
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
}
