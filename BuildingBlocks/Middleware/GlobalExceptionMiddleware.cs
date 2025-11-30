using System.Net;
using System.Text.Json;
using BuildingBlocks.Exceptions;
using BuildingBlocks.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Middleware;

/// <summary>
/// Global exception handling middleware that catches all unhandled exceptions
/// and converts them to standardized error responses
/// </summary>
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            await HandleExceptionAsync(context, exception);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var errorResponse = CreateErrorResponse(context, exception);

        // Log the exception with appropriate level
        LogException(exception, context);

        // Set response properties
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = errorResponse.StatusCode;

        // Serialize and write response
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = _environment.IsDevelopment()
        };

        var jsonResponse = JsonSerializer.Serialize(errorResponse, jsonOptions);
        await context.Response.WriteAsync(jsonResponse);
    }

    private ErrorResponse CreateErrorResponse(HttpContext context, Exception exception)
    {
        var errorResponse = new ErrorResponse
        {
            Timestamp = DateTime.UtcNow,
            Path = context.Request.Path
        };

        switch (exception)
        {
            case BuildingBlocks.Exceptions.ValidationException validationException:
                errorResponse.StatusCode = (int)HttpStatusCode.BadRequest;
                errorResponse.Message = validationException.Message;
                errorResponse.ErrorCode = validationException.ErrorCode;
                errorResponse.ValidationErrors = validationException.ValidationErrors;
                break;

            case BadRequestException badRequestException:
                errorResponse.StatusCode = (int)HttpStatusCode.BadRequest;
                errorResponse.Message = badRequestException.Message;
                errorResponse.ErrorCode = badRequestException.ErrorCode;
                errorResponse.Details = badRequestException.Details;
                break;

            case UnauthorizedException unauthorizedException:
                errorResponse.StatusCode = (int)HttpStatusCode.Unauthorized;
                errorResponse.Message = unauthorizedException.Message;
                errorResponse.ErrorCode = unauthorizedException.ErrorCode;
                errorResponse.Details = unauthorizedException.Details;
                break;

            case ForbiddenException forbiddenException:
                errorResponse.StatusCode = (int)HttpStatusCode.Forbidden;
                errorResponse.Message = forbiddenException.Message;
                errorResponse.ErrorCode = forbiddenException.ErrorCode;
                errorResponse.Details = forbiddenException.Details;
                break;

            case NotFoundException notFoundException:
                errorResponse.StatusCode = (int)HttpStatusCode.NotFound;
                errorResponse.Message = notFoundException.Message;
                errorResponse.ErrorCode = notFoundException.ErrorCode;
                errorResponse.Details = notFoundException.Details;
                break;

            case ConflictException conflictException:
                errorResponse.StatusCode = (int)HttpStatusCode.Conflict;
                errorResponse.Message = conflictException.Message;
                errorResponse.ErrorCode = conflictException.ErrorCode;
                errorResponse.Details = conflictException.Details;
                break;

            // Handle FluentValidation.ValidationException (different from our custom one)
            case FluentValidation.ValidationException fluentValidationException:
                errorResponse.StatusCode = (int)HttpStatusCode.BadRequest;
                errorResponse.Message = "One or more validation errors occurred";
                errorResponse.ErrorCode = "VALIDATION_ERROR";
                errorResponse.ValidationErrors = fluentValidationException.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(e => e.ErrorMessage).ToArray()
                    );
                break;

            default:
                errorResponse.StatusCode = (int)HttpStatusCode.InternalServerError;
                errorResponse.Message = _environment.IsDevelopment()
                    ? exception.Message
                    : "An internal server error occurred. Please try again later.";
                errorResponse.ErrorCode = "INTERNAL_SERVER_ERROR";
                break;
        }

        // Add detailed error information only in development environment
        if (_environment.IsDevelopment())
        {
            errorResponse.StackTrace = exception.StackTrace;
            errorResponse.InnerException = exception.InnerException?.Message;

            // If not already set, include the actual exception message
            if (errorResponse.StatusCode == (int)HttpStatusCode.InternalServerError)
            {
                errorResponse.Details = new
                {
                    ExceptionType = exception.GetType().Name,
                    Message = exception.Message
                };
            }
        }

        return errorResponse;
    }

    private void LogException(Exception exception, HttpContext context)
    {
        var logLevel = exception switch
        {
            BuildingBlocks.Exceptions.ValidationException => LogLevel.Warning,
            BadRequestException => LogLevel.Warning,
            UnauthorizedException => LogLevel.Warning,
            ForbiddenException => LogLevel.Warning,
            NotFoundException => LogLevel.Information,
            ConflictException => LogLevel.Warning,
            FluentValidation.ValidationException => LogLevel.Warning,
            _ => LogLevel.Error
        };

        _logger.Log(
            logLevel,
            exception,
            "An exception occurred while processing request {Method} {Path}. User: {User}",
            context.Request.Method,
            context.Request.Path,
            context.User?.Identity?.Name ?? "Anonymous"
        );
    }
}
