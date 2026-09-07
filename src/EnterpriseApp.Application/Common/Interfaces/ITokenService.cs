namespace EnterpriseApp.Application.Common.Interfaces;

public sealed record TokenSubject(
    string              UserId,
    string              Email,
    string              FullName,
    Guid?               TenantId,
    IEnumerable<string> RoleNames,
    IEnumerable<string> PermissionCodes,
    string              SnapshotVersion,
    DateTimeOffset      AuthTime,
    IEnumerable<string> AuthMethods);

public sealed record AccessTokenResult(string Token, DateTimeOffset ExpiresAt);
public sealed record RefreshTokenResult(string Plain, string Hash, DateTimeOffset ExpiresAt);

public interface ITokenService
{
    AccessTokenResult  GenerateAccessToken(TokenSubject subject);
    RefreshTokenResult GenerateRefreshToken();
}
