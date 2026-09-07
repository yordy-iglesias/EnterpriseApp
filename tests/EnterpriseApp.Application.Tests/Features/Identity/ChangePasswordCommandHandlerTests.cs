using EnterpriseApp.Application.Common.Authorization;
using EnterpriseApp.Application.Common.Errors;
using EnterpriseApp.Application.Common.Interfaces;
using EnterpriseApp.Application.Features.Identity.Auth.Commands.ChangePassword;
using EnterpriseApp.Domain.Entities.Identity;
using EnterpriseApp.Domain.Interfaces.Repositories;
using EnterpriseApp.Domain.ValueObjects;
using FluentAssertions;
using Moq;
using Xunit;

namespace EnterpriseApp.Application.Tests.Features.Identity;

public sealed class ChangePasswordCommandHandlerTests
{
    private readonly Mock<IUnitOfWork>      _uow         = new();
    private readonly Mock<IPasswordHasher>  _hasher      = new();
    private readonly Mock<ICurrentUser>     _currentUser = new();

    private ChangePasswordCommandHandler BuildHandler() =>
        new(_uow.Object, _hasher.Object, _currentUser.Object);

    private User MakeActiveUser()
    {
        var user = User.Create(
            Email.From("user@test.com"), "hashed_pw", "John", "Doe", null, "seed");
        user.ConfirmEmail();
        return user;
    }

    [Fact]
    public async Task Handle_ValidCurrentPassword_ChangesPasswordAndReturnsSuccess()
    {
        var user = MakeActiveUser();
        var userId = user.Id;

        _currentUser.Setup(c => c.UserId).Returns(userId.Value.ToString());
        _uow.Setup(u => u.Users.GetByIdAsync(userId, default)).ReturnsAsync(user);
        _hasher.Setup(h => h.Verify("hashed_pw", "OldPass1")).Returns(PasswordVerificationResult.Success);
        _hasher.Setup(h => h.Hash("NewPass1!")).Returns("new_hashed_pw");
        _uow.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        var result = await BuildHandler().Handle(
            new ChangePasswordCommand("OldPass1", "NewPass1!"), default);

        result.IsSuccess.Should().BeTrue();
        _uow.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task Handle_WrongCurrentPassword_ReturnsInvalidCredentials()
    {
        var user = MakeActiveUser();
        var userId = user.Id;

        _currentUser.Setup(c => c.UserId).Returns(userId.Value.ToString());
        _uow.Setup(u => u.Users.GetByIdAsync(userId, default)).ReturnsAsync(user);
        _hasher.Setup(h => h.Verify("hashed_pw", "WrongPass")).Returns(PasswordVerificationResult.Failed);

        var result = await BuildHandler().Handle(
            new ChangePasswordCommand("WrongPass", "NewPass1!"), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AuthErrors.InvalidCredentials);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_UserNotFound_ReturnsUserNotFound()
    {
        _currentUser.Setup(c => c.UserId).Returns(Guid.NewGuid().ToString());
        _uow.Setup(u => u.Users.GetByIdAsync(It.IsAny<UserId>(), default)).ReturnsAsync((User?)null);

        var result = await BuildHandler().Handle(
            new ChangePasswordCommand("OldPass1", "NewPass1!"), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AuthErrors.UserNotFound);
    }

    [Fact]
    public async Task Handle_InvalidUserId_ReturnsUserNotFound()
    {
        _currentUser.Setup(c => c.UserId).Returns("not-a-guid");

        var result = await BuildHandler().Handle(
            new ChangePasswordCommand("OldPass1", "NewPass1!"), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AuthErrors.UserNotFound);
    }
}
