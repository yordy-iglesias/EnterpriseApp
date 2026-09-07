namespace EnterpriseApp.Application.Common.Auth;

public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    public int AccessTokenMinutes      { get; init; } = 15;
    public int RefreshTokenDays        { get; init; } = 7;
    public int MaxFailedAccessAttempts { get; init; } = 5;
    public int LockoutMinutes          { get; init; } = 15;

    public StepUpMfaOptions StepUpMfa  { get; init; } = new();
    public SeedAdminOptions SeedAdmin  { get; init; } = new();
}

public sealed class StepUpMfaOptions
{
    public bool Enabled       { get; init; }
    public int  MaxAgeMinutes { get; init; } = 5;
}

public sealed class SeedAdminOptions
{
    public bool   Enabled   { get; init; } = true;
    public string Email     { get; init; } = "admin@enterpriseapp.local";
    public string Password  { get; init; } = "Admin123!";
    public string FirstName { get; init; } = "System";
    public string LastName  { get; init; } = "Administrator";
}
