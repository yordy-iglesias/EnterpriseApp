// ── EnterpriseApp.AppHost ────────────────────────────────────────────────────
// Orquestador de .NET Aspire.
// Arranca la infraestructura (BD + Redis) y la API en un solo comando: dotnet run
// El provider de BD se configura en appsettings.json → Database:Provider
// Valores soportados: PostgreSQL | SqlServer | SQLite
// El Aspire Dashboard se abre automáticamente en el navegador.
// ─────────────────────────────────────────────────────────────────────────────

var builder = DistributedApplication.CreateBuilder(args);

var dbProvider = builder.Configuration["Database:Provider"] ?? "PostgreSQL";

// ── Cache Redis ──────────────────────────────────────────────────────────────
// Aspire inyecta ConnectionStrings__Redis en la API automáticamente.
// RedisInsight disponible como herramienta visual.
var redis = builder.AddRedis("Redis")
    .WithRedisInsight();

// ── API ───────────────────────────────────────────────────────────────────────
var api = builder.AddProject<Projects.EnterpriseApp_API>("api")
    .WithReference(redis)
    .WaitFor(redis)
    .WithEnvironment("Database__Provider", dbProvider);

// ── Base de datos ────────────────────────────────────────────────────────────
// Aspire levanta el contenedor correspondiente según Database:Provider en appsettings.json.
// SQLite no requiere contenedor: la API lo gestiona directamente.
if (dbProvider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
{
    var sqlserver = builder.AddSqlServer("sqlserver")
        .AddDatabase("DefaultConnection");

    api.WithReference(sqlserver)
       .WaitFor(sqlserver)
       .WithEnvironment("Database__ConnectionStringName", "DefaultConnection");
}
else if (dbProvider.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase))
{
    // pgAdmin disponible en http://localhost:<puerto-asignado>
    var postgres = builder.AddPostgres("postgres")
        .WithPgAdmin()
        .AddDatabase("DefaultConnection");

    api.WithReference(postgres)
       .WaitFor(postgres)
       .WithEnvironment("Database__ConnectionStringName", "DefaultConnection");
}
// SQLite: no hay infraestructura que levantar; la API lo maneja sola.

builder.Build().Run();
