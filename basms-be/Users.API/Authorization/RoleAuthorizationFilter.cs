namespace Users.API.Authorization;

public class RoleAuthorizationFilter : IEndpointFilter
{
    private readonly string[] _allowedRoleIds;

    public RoleAuthorizationFilter(params string[] allowedRoleIds)
    {
        _allowedRoleIds = allowedRoleIds;
    }

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;
        var roleIdClaim = httpContext.User.FindFirst("roleId")?.Value;

        if (string.IsNullOrEmpty(roleIdClaim))
        {
            return Results.Unauthorized();
        }

        if (_allowedRoleIds.Length > 0 && !_allowedRoleIds.Contains(roleIdClaim, StringComparer.OrdinalIgnoreCase))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Forbidden",
                detail: "You don't have permission to access this resource"
            );
        }

        return await next(context);
    }
}

