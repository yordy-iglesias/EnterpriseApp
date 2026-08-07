using EnterpriseApp.Application.Common.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EnterpriseApp.Application.Common.Behaviors;

/// <summary>
/// Emits a structured warning when a request exceeds the configured threshold,
/// including the current user so slow requests can be correlated to load patterns.
/// </summary>
public sealed class PerformanceBehavior<TRequest, TResponse>(
    ILogger<PerformanceBehavior<TRequest, TResponse>> logger,
    ICurrentUserService currentUser)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    // Separate, lower threshold — PerformanceBehavior is intentionally distinct
    // from LoggingBehavior so each concern can be toggled independently.
    private const int ThresholdMs = 1_000;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var response = await next();
        sw.Stop();

        if (sw.ElapsedMilliseconds > ThresholdMs)
            logger.LogWarning(
                "[Performance] Long-running request: {RequestName} ({ElapsedMs} ms) — User: {UserId}",
                typeof(TRequest).Name,
                sw.ElapsedMilliseconds,
                currentUser.UserId ?? "anonymous");

        return response;
    }
}
