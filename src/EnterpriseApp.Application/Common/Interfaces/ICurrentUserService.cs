namespace EnterpriseApp.Application.Common.Interfaces;

/// <summary>
/// Provides the identity of the authenticated user for audit fields.
/// Implemented in the API layer (HttpContext-based).
/// </summary>
public interface ICurrentUserService
{
    string? UserId   { get; }
    string? UserName { get; }
    bool    IsAuthenticated { get; }
}
