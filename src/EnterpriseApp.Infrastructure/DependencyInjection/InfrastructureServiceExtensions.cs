using EnterpriseApp.Application.Common.Auth;
using EnterpriseApp.Application.Common.Authorization;
using EnterpriseApp.Application.Common.Interfaces;
using EnterpriseApp.Infrastructure.Identity;
using EnterpriseApp.Domain.Interfaces.Repositories;
using EnterpriseApp.Domain.Interfaces.Repositories.Authorization;
using EnterpriseApp.Domain.Interfaces.Repositories.Identity;
using EnterpriseApp.Infrastructure.Authorization;
using EnterpriseApp.Infrastructure.Caching;
using EnterpriseApp.Infrastructure.Persistence;
using EnterpriseApp.Infrastructure.Persistence.Interceptors;
using EnterpriseApp.Infrastructure.Persistence.Seeds;
using EnterpriseApp.Infrastructure.Repositories;
using EnterpriseApp.Infrastructure.Repositories.Authorization;
using EnterpriseApp.Infrastructure.Repositories.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EnterpriseApp.Infrastructure.DependencyInjection;

/// <summary>
/// Registers all Infrastructure-layer services.
/// Call from Program.cs: <c>builder.Services.AddInfrastructureServices(builder.Configuration);</c>
/// </summary>
public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration          configuration)
    {
        // ── Database options (strongly typed from "Database" section) ────────
        var dbOptions = configuration
            .GetSection(DatabaseOptions.SectionName)
            .Get<DatabaseOptions>() ?? new DatabaseOptions();

        services.Configure<DatabaseOptions>(
            configuration.GetSection(DatabaseOptions.SectionName));

        var connStr = configuration.GetConnectionString(dbOptions.ConnectionStringName)
                      ?? throw new InvalidOperationException(
                          $"Connection string '{dbOptions.ConnectionStringName}' not found. " +
                          $"Check ConnectionStrings:{dbOptions.ConnectionStringName} in appsettings.json.");

        // ── EF Core interceptors (scoped — one per request/SaveChanges) ──────
        services.AddScoped<AuditableEntitySaveChangesInterceptor>();
        services.AddScoped<DomainEventDispatcherInterceptor>();

        // ── AppDbContext — provider selected from configuration ───────────────
        services.AddDbContext<AppDbContext>((sp, opts) =>
        {
            AppDbContextFactory.ApplyProvider(opts, dbOptions, connStr);

            opts.AddInterceptors(
                sp.GetRequiredService<AuditableEntitySaveChangesInterceptor>(),
                sp.GetRequiredService<DomainEventDispatcherInterceptor>());
        });

        services.AddScoped<IApplicationDbContext>(sp =>
            sp.GetRequiredService<AppDbContext>());

        // ── Repositories + Unit of Work ──────────────────────────────────────
        services.AddScoped<ITodoRepository, TodoRepository>();
        services.AddScoped<IRoleRepository,                RoleRepository>();
        services.AddScoped<IPermissionRepository,          PermissionRepository>();
        services.AddScoped<IUserRoleRepository,            UserRoleRepository>();
        services.AddScoped<IPermissionAuditLogRepository,  PermissionAuditLogRepository>();
        services.AddScoped<IUserRepository,                UserRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // ── Authorization — DB-driven RBAC + ABAC ────────────────────────────
        // See .claude/rules/authorization.md
        services.AddScoped<IPermissionService,         PermissionService>();
        services.AddScoped<IAuthorizationHandler,      PermissionAuthorizationHandler>();
        services.AddScoped<IAuthorizationHandler,      StepUpMfaAuthorizationHandler>();
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();

        // ── Identity — password hashing + JWT signing ────────────────────────
        services.Configure<AuthOptions>(configuration.GetSection(AuthOptions.SectionName));
        services.AddScoped<IPasswordHasher, PasswordHasherAdapter>();
        services.AddScoped<ITokenService, TokenService>();

        // ── Database seeder ──────────────────────────────────────────────────
        services.AddScoped<DatabaseSeeder>();

        // ── Caching (HybridCache + Redis) ────────────────────────────────────
        // Redis is skipped when DatabaseProvider is SQLite (local dev scenario).
        var redisConn = configuration.GetConnectionString("Redis");

        if (!string.IsNullOrWhiteSpace(redisConn) && dbOptions.Provider != DatabaseProvider.SQLite)
        {
            services.AddStackExchangeRedisCache(opts => opts.Configuration = redisConn);

#pragma warning disable EXTEXP0018  // HybridCache preview in .NET 9 — stable in .NET 10
            services.AddHybridCache(opts =>
            {
                opts.DefaultEntryOptions = new Microsoft.Extensions.Caching.Hybrid.HybridCacheEntryOptions
                {
                    Expiration           = TimeSpan.FromMinutes(10),
                    LocalCacheExpiration = TimeSpan.FromMinutes(2),
                };
            });
#pragma warning restore EXTEXP0018

            services.AddScoped<ICacheService, RedisCacheService>();
        }
        else
        {
            // Fallback: in-memory only cache for SQLite / no-Redis environments.
            services.AddMemoryCache();

#pragma warning disable EXTEXP0018
            services.AddHybridCache();
#pragma warning restore EXTEXP0018

            services.AddScoped<ICacheService, RedisCacheService>();
        }

        return services;
    }
}
