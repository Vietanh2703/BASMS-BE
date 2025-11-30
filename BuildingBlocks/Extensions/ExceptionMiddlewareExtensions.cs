using BuildingBlocks.Middleware;
using Microsoft.AspNetCore.Builder;

namespace BuildingBlocks.Extensions;

/// <summary>
/// Extension methods for registering exception handling middleware
/// </summary>
public static class ExceptionMiddlewareExtensions
{
    /// <summary>
    /// Adds global exception handling middleware to the application pipeline.
    /// This should be added early in the pipeline to catch all exceptions.
    /// </summary>
    /// <param name="app">The application builder</param>
    /// <returns>The application builder for chaining</returns>
    public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder app)
    {
        app.UseMiddleware<GlobalExceptionMiddleware>();
        return app;
    }
}
