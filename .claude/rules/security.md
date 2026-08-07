# Reglas de Seguridad — ASP.NET Core 9

## Autenticación y Autorización

### JWT Bearer
```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = config["Jwt:Issuer"],
            ValidAudience = config["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(config["Jwt:SecretKey"]!)),
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });
```

### Authorization Policies
```csharp
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", p => p.RequireRole("Admin"));
    options.AddPolicy("CanManageProducts", p =>
        p.RequireAuthenticatedUser()
         .RequireClaim("permission", "products:write"));
    // Fallback: todos los endpoints requieren auth por defecto
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});
```

## Rate Limiting (built-in .NET 7+)
```csharp
builder.Services.AddRateLimiter(options =>
{
    // Global: 100 req/min por IP
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
        RateLimitPartition.GetFixedWindowLimiter(
            ctx.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1)
            }));

    // Endpoint específico: login — 10 intentos/min
    options.AddPolicy("login", ctx =>
        RateLimitPartition.GetSlidingWindowLimiter(
            ctx.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = 6
            }));
});
```

## Seguridad HTTP Headers
```csharp
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.Append("Permissions-Policy", "camera=(), microphone=()");
    await next();
});
app.UseHsts(); // HTTPS en producción
```

## CORS
```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
        policy.WithOrigins(config["AllowedOrigins"]!.Split(','))
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials());
});
// Nunca: AllowAnyOrigin() + AllowCredentials() — error de seguridad
```

## Validación y sanitización
- **FluentValidation** en todos los Commands y Queries con `AddFluentValidationAutoValidation()`
- **Nunca** usar `[FromBody]` sin validar; registrar `ValidationExceptionMiddleware`
- SQL: EF Core parameteriza automáticamente; con Dapper usar `@param` obligatorio
- **Nunca** exponer stack traces en producción (usar Problem Details genérico)

## Secrets management
```csharp
// Desarrollo: User Secrets
// dotnet user-secrets set "ConnectionStrings:Default" "..."

// Producción: Azure Key Vault
builder.Configuration.AddAzureKeyVault(
    new Uri(config["KeyVault:Url"]!),
    new DefaultAzureCredential());

// NUNCA en appsettings.json de producción:
// ❌ "SecretKey": "mi-clave-secreta"
// ✅ "SecretKey": "" (vacío, se inyecta desde Key Vault)
```

## OWASP Top 10 checklist
- ✅ A01 Broken Access Control: fallback policy + resource-based auth
- ✅ A02 Cryptographic Failures: HTTPS forzado, secrets en Key Vault
- ✅ A03 Injection: EF Core parameterizado, FluentValidation
- ✅ A04 Insecure Design: Result pattern, no excepciones de negocio expuestas
- ✅ A05 Security Misconfiguration: headers seguros, CORS restrictivo
- ✅ A06 Vulnerable Components: `dotnet list package --vulnerable` en CI
- ✅ A07 Auth failures: JWT con validación completa, rate limiting en login
- ✅ A09 Logging failures: Serilog con datos sensibles excluidos
