using EnterpriseApp.API.Middleware;
using EnterpriseApp.API.Services;
using EnterpriseApp.Application.Common.Authorization;
using EnterpriseApp.Application.Common.Interfaces;
using EnterpriseApp.Application.DependencyInjection;
using EnterpriseApp.Infrastructure.DependencyInjection;
using EnterpriseApp.Infrastructure.Persistence;
using EnterpriseApp.Infrastructure.Persistence.Seeds;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Trace;
using Scalar.AspNetCore;
using Serilog;
using System.Text;

// ── Bootstrap logger (available before DI is ready) ─────────────────────────
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // ── Aspire ServiceDefaults ────────────────────────────────────────────────
    // Configura OTel (métricas, trazas, logs), service discovery y resiliencia HTTP.
    // Cuando se ejecuta via AppHost, el exporter OTLP apunta al Aspire Dashboard.
    builder.AddServiceDefaults();

    // ── Serilog ──────────────────────────────────────────────────────────────
    builder.Host.UseSerilog((ctx, services, cfg) => cfg
        .ReadFrom.Configuration(ctx.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console());

    // ── Application + Infrastructure services ────────────────────────────────
    builder.Services.AddApplicationServices();
    builder.Services.AddInfrastructureServices(builder.Configuration);

    // ── HTTP / MVC ───────────────────────────────────────────────────────────
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
    // Rich principal abstraction (permissions, tenant, AMR) — see .claude/rules/authorization.md
    builder.Services.AddScoped<ICurrentUser,        CurrentUser>();

    builder.Services.AddControllers()
        .AddJsonOptions(opts =>
        {
            opts.JsonSerializerOptions.Converters.Add(
                new System.Text.Json.Serialization.JsonStringEnumConverter());
        });

    // ── OpenAPI (ASP.NET Core 9 native + Scalar UI) ──────────────────────────
    builder.Services.AddOpenApi();

    // ── JWT Authentication ────────────────────────────────────────────────────
    var jwtSection = builder.Configuration.GetSection("Jwt");
    var jwtKey     = jwtSection["Key"] ?? throw new InvalidOperationException("Jwt:Key is not configured.");

    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(opts =>
        {
            opts.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer           = true,
                ValidateAudience         = true,
                ValidateLifetime         = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer              = jwtSection["Issuer"],
                ValidAudience            = jwtSection["Audience"],
                IssuerSigningKey         = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(jwtKey)),
                ClockSkew = TimeSpan.Zero,
            };
        });

    // Authorization — DB-driven RBAC + Permissions. See .claude/rules/authorization.md
    builder.Services.AddAuthorization(options =>
    {
        options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build();
    });
    // PermissionPolicyProvider materialises the perm:{code} policies on demand —
    // registered inside InfrastructureServiceExtensions so it can replace the default.

    // ── CORS ──────────────────────────────────────────────────────────────────
    builder.Services.AddCors(opts =>
    {
        opts.AddPolicy("AllowFrontend", policy =>
            policy.WithOrigins(
                    builder.Configuration.GetSection("AllowedOrigins")
                                         .Get<string[]>() ?? ["http://localhost:4200"])
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials());
    });

    // ── Health Checks de infraestructura ─────────────────────────────────────
    // El check "self" (liveness) ya fue agregado por AddServiceDefaults().
    // Aquí agregamos readiness checks condicionados al provider activo.
    var dbOptions = builder.Configuration
        .GetSection(DatabaseOptions.SectionName)
        .Get<DatabaseOptions>() ?? new DatabaseOptions();

    var healthChecksBuilder = builder.Services.AddHealthChecks();

    if (dbOptions.Provider == DatabaseProvider.PostgreSQL)
    {
        healthChecksBuilder.AddNpgSql(
            builder.Configuration.GetConnectionString("DefaultConnection")!,
            name: "postgres",
            tags: ["db", "ready"]);
    }

    var redisConn = builder.Configuration.GetConnectionString("Redis");
    if (!string.IsNullOrWhiteSpace(redisConn) && dbOptions.Provider != DatabaseProvider.SQLite)
    {
        healthChecksBuilder.AddRedis(redisConn, name: "redis", tags: ["cache", "ready"]);
    }

    // ── OpenTelemetry: instrumentación específica de infraestructura ──────────
    // OTel base (AspNetCore, Http, Runtime, OTLP) ya configurado por AddServiceDefaults().
    // Aquí solo agregamos EF Core y Redis que son específicos de este servicio.
    builder.Services.AddOpenTelemetry()
        .WithTracing(tracing =>
            tracing.AddEntityFrameworkCoreInstrumentation()
                   .AddRedisInstrumentation());

    // ── Rate Limiting ─────────────────────────────────────────────────────────
    builder.Services.AddRateLimiter(options =>
    {
        options.AddPolicy("login", ctx =>
            System.Threading.RateLimiting.RateLimitPartition.GetSlidingWindowLimiter(
                ctx.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
                _ => new System.Threading.RateLimiting.SlidingWindowRateLimiterOptions
                {
                    PermitLimit       = 10,
                    Window            = TimeSpan.FromMinutes(1),
                    SegmentsPerWindow = 6,
                }));
    });

    // ── Build ─────────────────────────────────────────────────────────────────
    var app = builder.Build();

    // ── Middleware pipeline ───────────────────────────────────────────────────
    app.UseMiddleware<GlobalExceptionMiddleware>();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi().AllowAnonymous();
        app.MapScalarApiReference().AllowAnonymous();
        app.MapGet("/", () => Results.Redirect("/scalar/v1")).AllowAnonymous()
            .ExcludeFromDescription();
    }

    app.UseSerilogRequestLogging();
    app.UseHttpsRedirection();
    app.UseCors("AllowFrontend");
    app.UseAuthentication();
    app.UseRateLimiter();
    app.UseAuthorization();

    app.MapControllers();
    // Mapea /health (readiness) y /alive (liveness) — definidos en ServiceDefaults
    app.MapDefaultEndpoints();

    // ── Database migration + seed (idempotent) ───────────────────────────────
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
        await seeder.SeedAsync();
    }

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
     Log.Fatal(ex, "Application terminated unexpectedly.");
}
finally
{
    Log.CloseAndFlush();
}

// Expose Program class to test assemblies (e.g., ArchitectureTests, WebApplicationFactory).
public partial class Program { }
