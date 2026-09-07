using EnterpriseApp.Domain.Common;

namespace EnterpriseApp.Application.Common.Errors;

public static class AuthErrors
{
    public static readonly Error InvalidCredentials  = new("Auth.InvalidCredentials",  "Invalid credentials.");
    public static readonly Error AccountLocked       = new("Auth.AccountLocked",       "Account is temporarily locked.");
    public static readonly Error AccountInactive     = new("Auth.AccountInactive",     "Account is inactive.");
    public static readonly Error InvalidRefreshToken = new("Auth.InvalidRefreshToken", "Refresh token is invalid or expired.");
    public static readonly Error RefreshTokenReused  = new("Auth.RefreshTokenReused",  "Refresh token reuse detected. All sessions revoked.");
    public static readonly Error EmailAlreadyExists  = new("User.EmailAlreadyExists",  "A user with that email already exists.");
    public static readonly Error UserNotFound        = new("User.NotFound",            "User not found.");
}
