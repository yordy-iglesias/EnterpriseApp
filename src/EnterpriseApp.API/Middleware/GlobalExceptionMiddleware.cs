using System.Net;
using System.Text.Json;
using EnterpriseApp.Application.Common.Exceptions;
using EnterpriseApp.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace EnterpriseApp.API.Middleware;

/// <summary>
/// Catches all unhandled exceptions and converts them to RFC 7807 ProblemDetails responses.
/// Keeps controllers clean — they never need try/catch blocks.
/// </summary>
public sealed class GlobalExceptionMiddleware(
    RequestDelegate                    next,
    ILogger<GlobalExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, detail, errors) = exception switch
        {
            ValidationException ve => (
                HttpStatusCode.BadRequest,
                "One or more validation errors occurred.",
                (IDictionary<string, string[]>?)ve.Errors),

            NotFoundException nfe => (
                HttpStatusCode.NotFound,
                nfe.Message,
                (IDictionary<string, string[]>?)null),

            DomainException de => (
                HttpStatusCode.UnprocessableEntity,
                de.Message,
                (IDictionary<string, string[]>?)null),

            UnauthorizedAccessException => (
                HttpStatusCode.Unauthorized,
                "Unauthorized.",
                (IDictionary<string, string[]>?)null),

            _ => (
                HttpStatusCode.InternalServerError,
                "An unexpected error occurred.",
                (IDictionary<string, string[]>?)null)
        };

        var problem = new ProblemDetails
        {
            Status = (int)statusCode,
            Title  = statusCode.ToString(),
            Detail = detail,
        };

        if (errors is not null)
            problem.Extensions["errors"] = errors;

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode  = (int)statusCode;

        return context.Response.WriteAsync(
            JsonSerializer.Serialize(problem, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }));
    }
}
