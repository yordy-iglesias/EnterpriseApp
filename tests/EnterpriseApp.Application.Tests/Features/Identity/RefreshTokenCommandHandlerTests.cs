using System.Security.Cryptography;
using System.Text;
using EnterpriseApp.Application.Common.Auth;
using EnterpriseApp.Application.Common.Errors;
using EnterpriseApp.Application.Common.Interfaces;
using EnterpriseApp.Application.Features.Identity.Auth.Commands.RefreshToken;
using EnterpriseApp.Application.Common.Authorization;
using EnterpriseApp.Domain.Entities.Identity;
using EnterpriseApp.Domain.Interfaces.Repositories;
using EnterpriseApp.Domain.ValueObjects;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace EnterpriseApp.Application.Tests.Features.Identity;

public sealed class RefreshTokenCommandHandlerTests
{
    private readonly Mock<IUnitOfWork>        _uow    = new();
    private readonly Mock<ITokenService>      _tokens = new();
    private readonly Mock<IPermissionService> _perms  = new();

    private readonly AuthOptions _opts = new();

    private RefreshTokenCommandHandler BuildHandler() =>
        new(_uow.Object, _tokens.Object, _perms.Object, Options.Create(_opts));

    /// <summary>
    /// Mirrors the SHA-256 hash computation in RefreshTokenCommandHandler.
    /// </summary>
    private static string ComputeHash(string plain)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(plain));
        return Convert.ToBase64String(bytes);
    }

    /// <summary>
    /// Creates a user whose stored token hash equals SHA-256(plainToken),
    /// so it will be found correctly when the handler computes the hash.
    /// </summary>
    private static User MakeUserWithToken(string plainToken)
    {
        var hash = ComputeHash(plainToken);
        var user = User.Create(Email.From("u@t.com"), "pw", "U", "T", null, "seed");
        user.ConfirmEmail();
        user.IssueRefreshToken(hash, DateTimeOffset.UtcNow.AddDays(7), "ip");
        user.ClearDomainEvents();
        return user;
    }

    [Fact]
    public async Task Handle_ValidToken_ReturnsNewAuthResponse()
    {
        var user = MakeUserWithToken("validhash_plain");
        _uow.Setup(u => u.Users.GetByRefreshTokenHashAsync(
                It.Is<string>(h => h == ComputeHash("validhash_plain")), default))
            .ReturnsAsync(user);
        _perms.Setup(p => p.GetPermissionsForUserAsync(It.IsAny<string>(), null, default))
            .ReturnsAsync((IReadOnlySet<string>)new HashSet<string>());
        _perms.Setup(p => p.ComputeSnapshotVersion(It.IsAny<IEnumerable<string>>())).Returns("v");
        _uow.Setup(u => u.Roles.GetRoleNamesForUserAsync(It.IsAny<string>(), null, default))
            .ReturnsAsync((IReadOnlyList<string>)new List<string>());
        _tokens.Setup(t => t.GenerateAccessToken(It.IsAny<TokenSubject>()))
            .Returns(new AccessTokenResult("new_access", DateTimeOffset.UtcNow.AddMinutes(15)));
        _tokens.Setup(t => t.GenerateRefreshToken())
            .Returns(new RefreshTokenResult("new_plain", "new_hash", DateTimeOffset.UtcNow.AddDays(7)));
        _uow.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        var result = await BuildHandler().Handle(new RefreshTokenCommand("validhash_plain", "ip"), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().Be("new_access");
    }

    [Fact]
    public async Task Handle_InvalidToken_ReturnsInvalidRefreshToken()
    {
        _uow.Setup(u => u.Users.GetByRefreshTokenHashAsync(It.IsAny<string>(), default))
            .ReturnsAsync((User?)null);

        var result = await BuildHandler().Handle(new RefreshTokenCommand("bad_token", null), default);

        result.Error.Should().Be(AuthErrors.InvalidRefreshToken);
    }

    [Fact]
    public async Task Handle_RevokedToken_RevokesAllAndReturnsReusedError()
    {
        // Create a user with a token, then rotate it so the original hash becomes inactive.
        var user = MakeUserWithToken("validhash_plain");
        var originalHash = ComputeHash("validhash_plain");
        user.RotateRefreshToken(originalHash, "newhash", DateTimeOffset.UtcNow.AddDays(7), "ip");
        user.ClearDomainEvents();

        _uow.Setup(u => u.Users.GetByRefreshTokenHashAsync(
                It.Is<string>(h => h == ComputeHash("validhash_plain")), default))
            .ReturnsAsync(user);
        // GenerateRefreshToken is called before the rotation check — must be set up to avoid NPE.
        _tokens.Setup(t => t.GenerateRefreshToken())
            .Returns(new RefreshTokenResult("new_plain", "new_hash2", DateTimeOffset.UtcNow.AddDays(7)));
        _uow.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        var result = await BuildHandler().Handle(new RefreshTokenCommand("validhash_plain", "ip"), default);

        result.Error.Should().Be(AuthErrors.RefreshTokenReused);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once());
    }
}
