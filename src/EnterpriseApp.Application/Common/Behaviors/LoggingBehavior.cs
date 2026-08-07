using MediatR;
using Microsoft.Extensions.Logging;

namespace EnterpriseApp.Application.Common.Behaviors;

/// <summary>
/// Logs the start and successful completion of every MediatR request.
/// Warnings are emitted for slow requests (> 500 ms) to surface N+1 queries
/// and other performance issues during development.
/// </summary>
public sealed class LoggingBehavior<TRequest, TResponse>(
    ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private const int SlowRequestThresholdMs = 500;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;

        logger.LogInformation("[MediatR] Handling {RequestName}", requestName);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var response = await next();
            sw.Stop();

            if (sw.ElapsedMilliseconds > SlowRequestThresholdMs)
                logger.LogWarning(
                    "[MediatR] Slow request detected — {RequestName} took {ElapsedMs} ms",
                    requestName, sw.ElapsedMilliseconds);
            else
                logger.LogInformation(
                    "[MediatR] Handled {RequestName} in {ElapsedMs} ms",
                    requestName, sw.ElapsedMilliseconds);

            return response;
        }
        catch (Exception ex)
        {
            sw.Stop();
            logger.LogError(ex,
                "[MediatR] Request {RequestName} failed after {ElapsedMs} ms",
                requestName, sw.ElapsedMilliseconds);
            throw;
        }
    }
}
