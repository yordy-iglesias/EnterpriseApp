using System.Security.Claims;
using EnterpriseApp.Application.Common.Interfaces;

namespace EnterpriseApp.API.Services;

/// <summary>
/// Reads identity from the current HTTP request's ClaimsPrincipal.
/// Registered as <see cref="ICurrentUserService"/> in the DI container.
/// </summary>
public sealed class CurrentUserService(IHttpContextAccessor httpContextAccessor)
    : ICurrentUserService
{
    private ClaimsPrincipal? Principal =>
        httpContextAccessor.HttpContext?.User;

    public string? UserId =>
        Principal?.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? Principal?.FindFirstValue("sub");

    public string? UserName =>
        Principal?.FindFirstValue(ClaimTypes.Name)
        ?? Principal?.FindFirstValue("name");

    public bool IsAuthenticated =>
        Principal?.Identity?.IsAuthenticated == true;
}
