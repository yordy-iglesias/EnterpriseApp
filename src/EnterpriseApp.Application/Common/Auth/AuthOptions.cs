namespace EnterpriseApp.Application.Common.Auth;

public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    public JwtOptions Jwt { get; init; } = new();
    public RefreshTokenOptions RefreshToken { get; init; } = new();
    public LockoutOptions Lockout { get; init; } = new();
    public StepUpMfaOptions  StepUpMfa  { get; init; } = new();
    public SeedAdminOptions  SeedAdmin  { get; init; } = new();
}

public sealed class JwtOptions
{
    public string Key { get; init; } = string.Empty;
    public string Issuer { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;
    public int ExpiryMinutes { get; init; } = 15;
}

public sealed class RefreshTokenOptions
{
    public int ExpiryDays { get; init; } = 7;
}

public sealed class LockoutOptions
{
    public int MaxFailedAttempts { get; init; } = 5;
    public int LockoutMinutes { get; init; } = 15;
}

public sealed class StepUpMfaOptions
{
    public bool Enabled { get; init; } = false;
    public int MaxAgeMinutes { get; init; } = 5;
}

public sealed class SeedAdminOptions
{
    public bool   Enabled   { get; init; } = true;
    public string Email     { get; init; } = "admin@enterpriseapp.local";
    public string Password  { get; init; } = "Admin123!";
    public string FirstName { get; init; } = "System";
    public string LastName  { get; init; } = "Administrator";
}
