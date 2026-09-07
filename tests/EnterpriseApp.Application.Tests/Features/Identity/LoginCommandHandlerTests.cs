using EnterpriseApp.Application.Common.Auth;
using EnterpriseApp.Application.Common.Errors;
using EnterpriseApp.Application.Common.Interfaces;
using EnterpriseApp.Application.Features.Identity.Auth.Commands.Login;
using EnterpriseApp.Application.Features.Identity.Auth.DTOs;
using EnterpriseApp.Application.Common.Authorization;
using EnterpriseApp.Domain.Entities.Identity;
using EnterpriseApp.Domain.Interfaces.Repositories;
using EnterpriseApp.Domain.ValueObjects;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace EnterpriseApp.Application.Tests.Features.Identity;

public sealed class LoginCommandHandlerTests
{
    private readonly Mock<IUnitOfWork>         _uow       = new();
    private readonly Mock<IPasswordHasher>     _hasher    = new();
    private readonly Mock<ITokenService>       _tokens    = new();
    private readonly Mock<IPermissionService>  _perms     = new();

    private readonly AuthOptions _opts = new()
    {
        AccessTokenMinutes      = 15,
        RefreshTokenDays        = 7,
        MaxFailedAccessAttempts = 5,
        LockoutMinutes          = 15,
    };

    private LoginCommandHandler BuildHandler() =>
        new(_uow.Object, _hasher.Object, _tokens.Object, _perms.Object,
            Options.Create(_opts));

    private User MakeActiveUser()
    {
        var user = User.Create(
            Email.From("user@test.com"), "hashed_pw", "John", "Doe", null, "seed");
        user.ConfirmEmail();
        return user;
    }

    [Fact]
    public async Task Handle_ValidCredentials_ReturnsAuthResponse()
    {
        var user = MakeActiveUser();
        _uow.Setup(u => u.Users.GetByNormalizedEmailAsync("user@test.com", default))
            .ReturnsAsync(user);
        _hasher.Setup(h => h.Verify("hashed_pw", "correct_pw"))
            .Returns(PasswordVerificationResult.Success);
        _perms.Setup(p => p.GetPermissionsForUserAsync(user.Id.ToString(), null, default))
            .ReturnsAsync((IReadOnlySet<string>)new HashSet<string> { "user.view" });
        _perms.Setup(p => p.ComputeSnapshotVersion(It.IsAny<IEnumerable<string>>()))
            .Returns("psv1");
        _uow.Setup(u => u.Roles.GetRoleNamesForUserAsync(user.Id.ToString(), null, default))
            .ReturnsAsync((IReadOnlyList<string>)new List<string> { "user" });
        _tokens.Setup(t => t.GenerateAccessToken(It.IsAny<TokenSubject>()))
            .Returns(new AccessTokenResult("access_tok", DateTimeOffset.UtcNow.AddMinutes(15)));
        _tokens.Setup(t => t.GenerateRefreshToken())
            .Returns(new RefreshTokenResult("plain_tok", "hash_tok", DateTimeOffset.UtcNow.AddDays(7)));
        _uow.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        var result = await BuildHandler().Handle(new LoginCommand("user@test.com", "correct_pw", null), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().Be("access_tok");
        result.Value.RefreshToken.Should().Be("plain_tok");
    }

    [Fact]
    public async Task Handle_WrongPassword_ReturnsInvalidCredentials()
    {
        var user = MakeActiveUser();
        _uow.Setup(u => u.Users.GetByNormalizedEmailAsync("user@test.com", default))
            .ReturnsAsync(user);
        _hasher.Setup(h => h.Verify("hashed_pw", "wrong"))
            .Returns(PasswordVerificationResult.Failed);
        _uow.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        var result = await BuildHandler().Handle(new LoginCommand("user@test.com", "wrong", null), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AuthErrors.InvalidCredentials);
    }

    [Fact]
    public async Task Handle_EmailNotFound_ReturnsSameInvalidCredentials()
    {
        _uow.Setup(u => u.Users.GetByNormalizedEmailAsync(It.IsAny<string>(), default))
            .ReturnsAsync((User?)null);

        var result = await BuildHandler().Handle(new LoginCommand("no@one.com", "pw", null), default);

        result.Error.Should().Be(AuthErrors.InvalidCredentials);
    }

    [Fact]
    public async Task Handle_LockedAccount_ReturnsAccountLocked()
    {
        var user = MakeActiveUser();
        for (int i = 0; i < 5; i++) user.RecordFailedLogin(5, TimeSpan.FromMinutes(15));

        _uow.Setup(u => u.Users.GetByNormalizedEmailAsync("user@test.com", default))
            .ReturnsAsync(user);
        _hasher.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(PasswordVerificationResult.Success);
        _uow.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        var result = await BuildHandler().Handle(new LoginCommand("user@test.com", "correct_pw", null), default);

        result.Error.Should().Be(AuthErrors.AccountLocked);
    }

    [Fact]
    public async Task Handle_InactiveAccount_ReturnsAccountInactive()
    {
        var user = MakeActiveUser();
        user.Deactivate("admin");
        user.ClearDomainEvents();

        _uow.Setup(u => u.Users.GetByNormalizedEmailAsync("user@test.com", default))
            .ReturnsAsync(user);
        _hasher.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(PasswordVerificationResult.Success);
        _uow.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        var result = await BuildHandler().Handle(new LoginCommand("user@test.com", "pw", null), default);

        result.Error.Should().Be(AuthErrors.AccountInactive);
    }
}
