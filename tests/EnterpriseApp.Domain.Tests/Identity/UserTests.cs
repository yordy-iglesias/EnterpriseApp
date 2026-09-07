using EnterpriseApp.Domain.DomainEvents.Identity;
using EnterpriseApp.Domain.Entities.Identity;
using EnterpriseApp.Domain.Exceptions;
using EnterpriseApp.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace EnterpriseApp.Domain.Tests.Identity;

public sealed class UserTests
{
    private static User CreateUser(string email = "test@example.com") =>
        User.Create(
            email:        Email.From(email),
            passwordHash: "hash123",
            firstName:    "John",
            lastName:     "Doe",
            tenantId:     null,
            createdBy:    "system");

    [Fact]
    public void Create_ValidInput_RaisesUserCreatedEvent()
    {
        var user = CreateUser();
        user.DomainEvents.Should().ContainSingle(e => e is UserCreated);
        user.IsActive.Should().BeTrue();
        user.EmailConfirmed.Should().BeFalse();
    }

    [Fact]
    public void RecordFailedLogin_BelowThreshold_IncrementsCounter()
    {
        var user = CreateUser();
        user.RecordFailedLogin(5, TimeSpan.FromMinutes(15));
        user.AccessFailedCount.Should().Be(1);
        user.IsLockedOut(DateTimeOffset.UtcNow).Should().BeFalse();
    }

    [Fact]
    public void RecordFailedLogin_ReachesThreshold_LocksAccount()
    {
        var user = CreateUser();
        for (int i = 0; i < 5; i++)
            user.RecordFailedLogin(5, TimeSpan.FromMinutes(15));

        user.IsLockedOut(DateTimeOffset.UtcNow).Should().BeTrue();
        user.DomainEvents.Should().Contain(e => e is UserLockedOut);
    }

    [Fact]
    public void RecordSuccessfulLogin_ResetsFailed_AndSetsLastLogin()
    {
        var user = CreateUser();
        user.RecordFailedLogin(5, TimeSpan.FromMinutes(15));
        user.RecordSuccessfulLogin();

        user.AccessFailedCount.Should().Be(0);
        user.LastLoginAt.Should().NotBeNull();
        user.DomainEvents.Should().Contain(e => e is UserLoggedIn);
    }

    [Fact]
    public void IsLockedOut_AfterExpiry_ReturnsFalse()
    {
        var user = CreateUser();
        for (int i = 0; i < 5; i++)
            user.RecordFailedLogin(5, TimeSpan.FromMinutes(15));

        user.IsLockedOut(DateTimeOffset.UtcNow.AddMinutes(20)).Should().BeFalse();
    }

    [Fact]
    public void ChangePassword_RotatesSecurityStampAndRevokesTokens()
    {
        var user = CreateUser();
        user.ConfirmEmail();
        user.IssueRefreshToken("hash1", DateTimeOffset.UtcNow.AddDays(7), "127.0.0.1");
        var stampBefore = user.SecurityStamp;

        user.ChangePassword("newhash", "admin");

        user.SecurityStamp.Should().NotBe(stampBefore);
        user.DomainEvents.Should().Contain(e => e is UserPasswordChanged);
        user.GetActiveRefreshToken("hash1").Should().BeNull();
    }

    [Fact]
    public void Deactivate_RevokesAllTokens_RaisesEvent()
    {
        var user = CreateUser();
        user.ConfirmEmail();
        user.IssueRefreshToken("tok1", DateTimeOffset.UtcNow.AddDays(7), "ip");
        user.Deactivate("admin");

        user.IsActive.Should().BeFalse();
        user.DomainEvents.Should().Contain(e => e is UserDeactivated);
        user.GetActiveRefreshToken("tok1").Should().BeNull();
    }

    [Fact]
    public void RotateRefreshToken_MarksOldRevoked_AddsNew()
    {
        var user = CreateUser();
        user.IssueRefreshToken("oldhash", DateTimeOffset.UtcNow.AddDays(7), "ip");
        user.RotateRefreshToken("oldhash", "newhash", DateTimeOffset.UtcNow.AddDays(7), "ip");

        user.GetActiveRefreshToken("newhash").Should().NotBeNull();
        user.GetActiveRefreshToken("oldhash").Should().BeNull();
    }

    [Fact]
    public void RotateRefreshToken_WithRevokedToken_FiresReuseDetectedAndRevokesAll()
    {
        var user = CreateUser();
        user.IssueRefreshToken("revokedhash", DateTimeOffset.UtcNow.AddDays(7), "ip");
        user.IssueRefreshToken("activehash", DateTimeOffset.UtcNow.AddDays(7), "ip");

        // Revoke the first token explicitly
        user.RotateRefreshToken("revokedhash", "newhash", DateTimeOffset.UtcNow.AddDays(7), "ip");
        // Now "revokedhash" is revoked (replaced by "newhash"); attempt reuse
        user.RotateRefreshToken("revokedhash", "attackhash", DateTimeOffset.UtcNow.AddDays(7), "ip");

        user.DomainEvents.Should().Contain(e => e is RefreshTokenReuseDetected);
        user.RefreshTokens.Should().NotContain(t => t.IsActive);
    }
}
