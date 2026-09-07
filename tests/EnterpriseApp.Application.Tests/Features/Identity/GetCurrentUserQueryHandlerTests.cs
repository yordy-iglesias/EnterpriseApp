using EnterpriseApp.Application.Common.Authorization;
using EnterpriseApp.Application.Common.Errors;
using EnterpriseApp.Application.Features.Identity.Auth.Queries.GetCurrentUser;
using EnterpriseApp.Domain.Entities.Identity;
using EnterpriseApp.Domain.Interfaces.Repositories;
using EnterpriseApp.Domain.ValueObjects;
using FluentAssertions;
using Moq;
using Xunit;

namespace EnterpriseApp.Application.Tests.Features.Identity;

public sealed class GetCurrentUserQueryHandlerTests
{
    private readonly Mock<IUnitOfWork>       _uow         = new();
    private readonly Mock<ICurrentUser>      _currentUser = new();
    private readonly Mock<IPermissionService> _perms      = new();

    private GetCurrentUserQueryHandler BuildHandler() =>
        new(_uow.Object, _currentUser.Object, _perms.Object);

    private User MakeActiveUser()
    {
        var user = User.Create(
            Email.From("user@test.com"), "hashed_pw", "John", "Doe", null, "seed");
        user.ConfirmEmail();
        return user;
    }

    [Fact]
    public async Task Handle_AuthenticatedUser_ReturnsCurrentUserDto()
    {
        var user = MakeActiveUser();
        var userId = user.Id;

        _currentUser.Setup(c => c.UserId).Returns(userId.Value.ToString());
        _uow.Setup(u => u.Users.GetByIdAsync(userId, default)).ReturnsAsync(user);
        _uow.Setup(u => u.Roles.GetRoleNamesForUserAsync(userId.Value.ToString(), null, default))
            .ReturnsAsync((IReadOnlyList<string>)new List<string> { "user" });
        _perms.Setup(p => p.GetPermissionsForUserAsync(userId.Value.ToString(), null, default))
            .ReturnsAsync((IReadOnlySet<string>)new HashSet<string> { "user.view" });

        var result = await BuildHandler().Handle(new GetCurrentUserQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Email.Should().Be("user@test.com");
        result.Value.FullName.Should().Be("John Doe");
        result.Value.Roles.Should().ContainSingle(r => r == "user");
        result.Value.Permissions.Should().ContainSingle(p => p == "user.view");
    }

    [Fact]
    public async Task Handle_UserNotFound_ReturnsUserNotFound()
    {
        _currentUser.Setup(c => c.UserId).Returns(Guid.NewGuid().ToString());
        _uow.Setup(u => u.Users.GetByIdAsync(It.IsAny<UserId>(), default)).ReturnsAsync((User?)null);

        var result = await BuildHandler().Handle(new GetCurrentUserQuery(), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AuthErrors.UserNotFound);
    }

    [Fact]
    public async Task Handle_InvalidUserId_ReturnsUserNotFound()
    {
        _currentUser.Setup(c => c.UserId).Returns("not-a-guid");

        var result = await BuildHandler().Handle(new GetCurrentUserQuery(), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AuthErrors.UserNotFound);
    }
}
