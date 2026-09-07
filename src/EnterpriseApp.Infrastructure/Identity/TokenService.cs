using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using EnterpriseApp.Application.Common.Auth;
using EnterpriseApp.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace EnterpriseApp.Infrastructure.Identity;

internal sealed class TokenService(IConfiguration config, IOptions<AuthOptions> opts) : ITokenService
{
    public AccessTokenResult GenerateAccessToken(TokenSubject subject)
    {
        var o        = opts.Value;
        var jwtKey   = config["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key not configured.");
        var issuer   = config["Jwt:Issuer"];
        var audience = config["Jwt:Audience"];
        var key      = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var creds    = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires  = DateTimeOffset.UtcNow.AddMinutes(o.Jwt.ExpiryMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub,   subject.UserId),
            new(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat,   DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new("name",      subject.FullName),
            new("email",     subject.Email),
            new("psv",       subject.SnapshotVersion),
            new("auth_time", subject.AuthTime.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
        };

        foreach (var method in subject.AuthMethods)
            claims.Add(new Claim("amr", method));

        foreach (var perm in subject.PermissionCodes)
            claims.Add(new Claim("perms", perm));

        if (subject.TenantId is { } tid)
            claims.Add(new Claim("tid", tid.ToString()));

        foreach (var role in subject.RoleNames)
            claims.Add(new Claim(ClaimTypes.Role, role));

        var token = new JwtSecurityToken(
            issuer:             issuer,
            audience:           audience,
            claims:             claims,
            notBefore:          DateTime.UtcNow,
            expires:            expires.UtcDateTime,
            signingCredentials: creds);

        return new AccessTokenResult(new JwtSecurityTokenHandler().WriteToken(token), expires);
    }

    public RefreshTokenResult GenerateRefreshToken()
    {
        var o     = opts.Value;
        var bytes = RandomNumberGenerator.GetBytes(64);
        var plain = Base64UrlEncoder.Encode(bytes);
        var hash  = ComputeHash(plain);
        var exp   = DateTimeOffset.UtcNow.AddDays(o.RefreshToken.ExpiryDays);
        return new RefreshTokenResult(plain, hash, exp);
    }

    private static string ComputeHash(string plain)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(plain));
        return Convert.ToBase64String(bytes);
    }
}
