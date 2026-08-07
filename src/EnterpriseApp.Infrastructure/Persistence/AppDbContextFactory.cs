using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace EnterpriseApp.Infrastructure.Persistence;

/// <summary>
/// Design-time factory used by <c>dotnet ef migrations</c>.
/// Reads the same <c>Database</c> section as the runtime DI so the migration
/// is always generated for the currently configured provider.
///
/// Usage (from solution root):
/// <code>
///   # PostgreSQL (default)
///   dotnet ef migrations add &lt;Name&gt; \
///     --project src/EnterpriseApp.Infrastructure \
///     --startup-project src/EnterpriseApp.API
///
///   # SQL Server — override via environment variable before running the command
///   set Database__Provider=SqlServer
///   dotnet ef migrations add &lt;Name&gt; \
///     --project src/EnterpriseApp.Infrastructure \
///     --startup-project src/EnterpriseApp.API
/// </code>
///
/// Tip: use separate migration folders per provider if you need to maintain
/// schemas for more than one provider simultaneously (see DatabaseOptions comments).
/// </summary>
internal sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        // Build a minimal IConfiguration that reads appsettings + environment variables.
        var config = new ConfigurationBuilder()
            .SetBasePath(FindApiProjectDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var dbOptions = config
            .GetSection(DatabaseOptions.SectionName)
            .Get<DatabaseOptions>() ?? new DatabaseOptions();

        var connStr = config.GetConnectionString(dbOptions.ConnectionStringName)
                      ?? throw new InvalidOperationException(
                          $"Connection string '{dbOptions.ConnectionStringName}' not found in configuration.");

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        ApplyProvider(optionsBuilder, dbOptions, connStr);

        // Design-time context does not need interceptors.
        return new AppDbContext(optionsBuilder.Options, null!, null!);
    }

    internal static void ApplyProvider(
        DbContextOptionsBuilder opts,
        DatabaseOptions         dbOptions,
        string                  connStr)
    {
        var migrationsAssembly = typeof(AppDbContext).Assembly.FullName!;

        switch (dbOptions.Provider)
        {
            case DatabaseProvider.SqlServer:
                opts.UseSqlServer(connStr, sql =>
                {
                    sql.MigrationsAssembly(migrationsAssembly);
                    sql.EnableRetryOnFailure(dbOptions.MaxRetryCount);
                    if (dbOptions.CommandTimeoutSeconds.HasValue)
                        sql.CommandTimeout(dbOptions.CommandTimeoutSeconds.Value);
                });
                break;

            case DatabaseProvider.SQLite:
                opts.UseSqlite(connStr, sqlite =>
                {
                    sqlite.MigrationsAssembly(migrationsAssembly);
                });
                break;

            case DatabaseProvider.PostgreSQL:
            default:
                opts.UseNpgsql(connStr, npgsql =>
                {
                    npgsql.MigrationsAssembly(migrationsAssembly);
                    npgsql.EnableRetryOnFailure(dbOptions.MaxRetryCount);
                    if (dbOptions.CommandTimeoutSeconds.HasValue)
                        npgsql.CommandTimeout(dbOptions.CommandTimeoutSeconds.Value);
                });
                break;
        }

        if (dbOptions.EnableSensitiveDataLogging)
            opts.EnableSensitiveDataLogging();

        if (dbOptions.EnableDetailedErrors)
            opts.EnableDetailedErrors();
    }

    /// <summary>
    /// Walks up from the Infrastructure project directory to locate the API project,
    /// which owns appsettings.json.
    /// </summary>
    private static string FindApiProjectDirectory()
    {
        // When called by dotnet-ef the current directory is the --startup-project directory.
        var current = Directory.GetCurrentDirectory();

        // If we can find appsettings.json here, use it directly.
        if (File.Exists(Path.Combine(current, "appsettings.json")))
            return current;

        // Walk up two levels to handle running from the solution root.
        var parent = Path.GetFullPath(Path.Combine(current, "..", "..", "src", "EnterpriseApp.API"));
        return Directory.Exists(parent) ? parent : current;
    }
}
