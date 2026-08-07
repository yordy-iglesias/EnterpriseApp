namespace EnterpriseApp.Infrastructure.Persistence;

/// <summary>
/// Supported relational database providers.
/// Set via <c>Database:Provider</c> in appsettings.json or environment variable
/// <c>Database__Provider</c>.
/// </summary>
public enum DatabaseProvider
{
    /// <summary>PostgreSQL via Npgsql. Default. Recommended for Linux/Docker/cloud.</summary>
    PostgreSQL,

    /// <summary>Microsoft SQL Server / Azure SQL. Recommended for Windows / Azure workloads.</summary>
    SqlServer,

    /// <summary>
    /// SQLite — file-based, zero dependencies.
    /// Use ONLY for local development and integration testing.
    /// Not suitable for production.
    /// </summary>
    SQLite,
}

/// <summary>
/// Strongly-typed options bound from the <c>Database</c> section of appsettings.
/// </summary>
public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>Which database engine to use. Default: PostgreSQL.</summary>
    public DatabaseProvider Provider { get; init; } = DatabaseProvider.PostgreSQL;

    /// <summary>
    /// Connection string name inside <c>ConnectionStrings</c>.
    /// Defaults to <c>"DefaultConnection"</c>.
    /// Override to use a provider-specific entry, e.g. <c>"SqlServerConnection"</c>.
    /// </summary>
    public string ConnectionStringName { get; init; } = "DefaultConnection";

    /// <summary>Max EF Core retry attempts on transient failures (PostgreSQL / SQL Server).</summary>
    public int MaxRetryCount { get; init; } = 3;

    /// <summary>
    /// EF Core command timeout in seconds.
    /// Null = use the provider default (typically 30 s).
    /// </summary>
    public int? CommandTimeoutSeconds { get; init; }

    /// <summary>Enable EF Core sensitive data logging (logs parameter values). NEVER use in production.</summary>
    public bool EnableSensitiveDataLogging { get; init; } = false;

    /// <summary>Enable EF Core detailed errors (adds full stack context). Use in Development only.</summary>
    public bool EnableDetailedErrors { get; init; } = false;
}
