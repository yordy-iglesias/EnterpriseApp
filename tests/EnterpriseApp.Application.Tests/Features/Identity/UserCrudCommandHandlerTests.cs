using EnterpriseApp.Application.Common.Authorization;
using EnterpriseApp.Application.Common.Errors;
using EnterpriseApp.Application.Common.Interfaces;
using EnterpriseApp.Application.Features.Identity.Users.Commands.ActivateUser;
using EnterpriseApp.Application.Features.Identity.Users.Commands.CreateUser;
using EnterpriseApp.Application.Features.Identity.Users.Commands.DeactivateUser;
using EnterpriseApp.Application.Features.Identity.Users.Commands.DeleteUser;
using EnterpriseApp.Application.Features.Identity.Users.Commands.UpdateUser;
using EnterpriseApp.Domain.Entities.Identity;
using EnterpriseApp.Domain.Interfaces.Repositories;
using EnterpriseApp.Domain.ValueObjects;
using FluentAssertions;
using Moq;
using Xunit;

namespace EnterpriseApp.Application.Tests.Features.Identity;

public sealed class UserCrudCommandHandlerTests
{
    private readonly Mock<IUnitOfWork>    _uow         = new();
    private readonly Mock<IPasswordHasher> _hasher     = new();
    private readonly Mock<ICurrentUser>   _currentUser = new();

    public UserCrudCommandHandlerTests()
    {
        _currentUser.Setup(c => c.UserId).Returns("admin-user");
    }

    private User MakeUser(string email = "user@test.com")
    {
        var user = User.Create(Email.From(email), "hashed_pw", "John", "Doe", null, "seed");
        user.ConfirmEmail();
        user.ClearDomainEvents();
        return user;
    }

    // ── CreateUserCommandHandler ──────────────────────────────────────────────

    [Fact]
    public async Task CreateUser_NewEmail_ReturnsNewUserId()
    {
        _uow.Setup(u => u.Users.ExistsByEmailAsync("new@test.com", default)).ReturnsAsync(false);
        _hasher.Setup(h => h.Hash("Password1")).Returns("hashed");
        _uow.Setup(u => u.Users.AddAsync(It.IsAny<User>(), default)).Returns(Task.CompletedTask);
        _uow.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        var handler = new CreateUserCommandHandler(_uow.Object, _hasher.Object, _currentUser.Object);
        var cmd     = new CreateUserCommand("new@test.com", "Password1", "Jane", "Doe", null);

        var result = await handler.Handle(cmd, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CreateUser_DuplicateEmail_ReturnsEmailAlreadyExists()
    {
        _uow.Setup(u => u.Users.ExistsByEmailAsync("dup@test.com", default)).ReturnsAsync(true);

        var handler = new CreateUserCommandHandler(_uow.Object, _hasher.Object, _currentUser.Object);
        var cmd     = new CreateUserCommand("dup@test.com", "Password1", "Jane", "Doe", null);

        var result = await handler.Handle(cmd, default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AuthErrors.EmailAlreadyExists);
        _hasher.Verify(h => h.Hash(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task CreateUser_DuplicateEmail_DoesNotHashPassword()
    {
        _uow.Setup(u => u.Users.ExistsByEmailAsync("dup@test.com", default)).ReturnsAsync(true);

        var handler = new CreateUserCommandHandler(_uow.Object, _hasher.Object, _currentUser.Object);
        var cmd     = new CreateUserCommand("dup@test.com", "Password1", "Jane", "Doe", null);

        await handler.Handle(cmd, default);

        _hasher.Verify(h => h.Hash(It.IsAny<string>()), Times.Never,
            "Password must NOT be hashed when email already exists");
    }

    // ── UpdateUserCommandHandler ──────────────────────────────────────────────

    [Fact]
    public async Task UpdateUser_ExistingUser_UpdatesProfileAndReturnsSuccess()
    {
        var user = MakeUser();
        _uow.Setup(u => u.Users.GetByIdAsync(UserId.From(user.Id.Value), default)).ReturnsAsync(user);
        _uow.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        var handler = new UpdateUserCommandHandler(_uow.Object, _currentUser.Object);
        var cmd     = new UpdateUserCommand(user.Id.Value, "Updated", "Name");

        var result = await handler.Handle(cmd, default);

        result.IsSuccess.Should().BeTrue();
        user.FirstName.Should().Be("Updated");
        user.LastName.Should().Be("Name");
    }

    [Fact]
    public async Task UpdateUser_NonExistingUser_ReturnsUserNotFound()
    {
        var id = Guid.NewGuid();
        _uow.Setup(u => u.Users.GetByIdAsync(UserId.From(id), default)).ReturnsAsync((User?)null);

        var handler = new UpdateUserCommandHandler(_uow.Object, _currentUser.Object);
        var cmd     = new UpdateUserCommand(id, "A", "B");

        var result = await handler.Handle(cmd, default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AuthErrors.UserNotFound);
    }

    // ── ActivateUserCommandHandler ────────────────────────────────────────────

    [Fact]
    public async Task ActivateUser_ExistingUser_ActivatesAndReturnsSuccess()
    {
        var user = MakeUser();
        user.Deactivate("admin");
        user.ClearDomainEvents();
        _uow.Setup(u => u.Users.GetByIdAsync(UserId.From(user.Id.Value), default)).ReturnsAsync(user);
        _uow.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        var handler = new ActivateUserCommandHandler(_uow.Object, _currentUser.Object);
        var result  = await handler.Handle(new ActivateUserCommand(user.Id.Value), default);

        result.IsSuccess.Should().BeTrue();
        user.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task ActivateUser_NonExistingUser_ReturnsUserNotFound()
    {
        var id = Guid.NewGuid();
        _uow.Setup(u => u.Users.GetByIdAsync(UserId.From(id), default)).ReturnsAsync((User?)null);

        var handler = new ActivateUserCommandHandler(_uow.Object, _currentUser.Object);
        var result  = await handler.Handle(new ActivateUserCommand(id), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AuthErrors.UserNotFound);
    }

    // ── DeactivateUserCommandHandler ──────────────────────────────────────────

    [Fact]
    public async Task DeactivateUser_ExistingUser_DeactivatesAndReturnsSuccess()
    {
        var user = MakeUser();
        _uow.Setup(u => u.Users.GetByIdAsync(UserId.From(user.Id.Value), default)).ReturnsAsync(user);
        _uow.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        var handler = new DeactivateUserCommandHandler(_uow.Object, _currentUser.Object);
        var result  = await handler.Handle(new DeactivateUserCommand(user.Id.Value), default);

        result.IsSuccess.Should().BeTrue();
        user.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task DeactivateUser_NonExistingUser_ReturnsUserNotFound()
    {
        var id = Guid.NewGuid();
        _uow.Setup(u => u.Users.GetByIdAsync(UserId.From(id), default)).ReturnsAsync((User?)null);

        var handler = new DeactivateUserCommandHandler(_uow.Object, _currentUser.Object);
        var result  = await handler.Handle(new DeactivateUserCommand(id), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AuthErrors.UserNotFound);
    }

    // ── DeleteUserCommandHandler ──────────────────────────────────────────────

    [Fact]
    public async Task DeleteUser_ExistingUser_SoftDeletesAndReturnsSuccess()
    {
        var user = MakeUser();
        _uow.Setup(u => u.Users.GetByIdAsync(UserId.From(user.Id.Value), default)).ReturnsAsync(user);
        _uow.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        var handler = new DeleteUserCommandHandler(_uow.Object, _currentUser.Object);
        var result  = await handler.Handle(new DeleteUserCommand(user.Id.Value), default);

        result.IsSuccess.Should().BeTrue();
        user.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteUser_NonExistingUser_ReturnsUserNotFound()
    {
        var id = Guid.NewGuid();
        _uow.Setup(u => u.Users.GetByIdAsync(UserId.From(id), default)).ReturnsAsync((User?)null);

        var handler = new DeleteUserCommandHandler(_uow.Object, _currentUser.Object);
        var result  = await handler.Handle(new DeleteUserCommand(id), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AuthErrors.UserNotFound);
    }
}
