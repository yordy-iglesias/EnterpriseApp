using System.Reflection;
using EnterpriseApp.Application.Common.Behaviors;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace EnterpriseApp.Application.DependencyInjection;

/// <summary>
/// Registers all Application-layer services.
/// Call this from Program.cs: <c>builder.Services.AddApplicationServices();</c>
/// </summary>
public static class ApplicationServiceExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        // AutoMapper — scans this assembly for all Profile subclasses.
        services.AddAutoMapper(assembly);

        // FluentValidation — registers all AbstractValidator<T> from this assembly.
        services.AddValidatorsFromAssembly(assembly);

        // MediatR — registers all IRequestHandler<,> + pipeline behaviors.
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);

            // Order matters: Logging → Validation → Caching → Performance → Handler
            // CachingBehavior only activates for queries that implement ICachedQuery<T>.
            // It sits after Validation (invalid requests never reach the cache) and before
            // Performance (so the perf timer only measures real handler work on cache misses).
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(CachingBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(PerformanceBehavior<,>));
        });

        return services;
    }
}
