using EnterpriseApp.Application.Common.Errors;
using EnterpriseApp.Application.Features.Identity.Users.Queries.GetUserById;
using EnterpriseApp.Application.Features.Identity.Users.Queries.GetUsers;
using EnterpriseApp.Domain.Common;
using EnterpriseApp.Domain.Entities.Identity;
using EnterpriseApp.Domain.Interfaces.Repositories;
using EnterpriseApp.Domain.ValueObjects;
using FluentAssertions;
using Moq;
using Xunit;

namespace EnterpriseApp.Application.Tests.Features.Identity;

public sealed class UserQueryHandlerTests
{
    private readonly Mock<IUnitOfWork> _uow = new();

    private User MakeUser(string email = "user@test.com")
    {
        var user = User.Create(Email.From(email), "hashed_pw", "John", "Doe", null, "seed");
        user.ConfirmEmail();
        user.ClearDomainEvents();
        return user;
    }

    // ── GetUsersQueryHandler ──────────────────────────────────────────────────

    [Fact]
    public async Task GetUsers_ReturnsPagedResult()
    {
        var user = MakeUser();
        var paged = PagedList<User>.Create(new List<User> { user }, 1, 1, 20);
        _uow.Setup(u => u.Users.GetPagedAsync(1, 20, default)).ReturnsAsync(paged);

        var handler = new GetUsersQueryHandler(_uow.Object);
        var result  = await handler.Handle(new GetUsersQuery(1, 20), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Data.Should().HaveCount(1);
        result.Value.TotalCount.Should().Be(1);
        result.Value.Data[0].Email.Should().Be("user@test.com");
    }

    [Fact]
    public async Task GetUsers_EmptyPage_ReturnsEmptyList()
    {
        var paged = PagedList<User>.Create(new List<User>(), 0, 1, 20);
        _uow.Setup(u => u.Users.GetPagedAsync(1, 20, default)).ReturnsAsync(paged);

        var handler = new GetUsersQueryHandler(_uow.Object);
        var result  = await handler.Handle(new GetUsersQuery(1, 20), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Data.Should().BeEmpty();
        result.Value.TotalCount.Should().Be(0);
    }

    // ── GetUserByIdQueryHandler ───────────────────────────────────────────────

    [Fact]
    public async Task GetUserById_ExistingUser_ReturnsUserDetail()
    {
        var user = MakeUser();
        _uow.Setup(u => u.Users.GetByIdAsync(UserId.From(user.Id.Value), default)).ReturnsAsync(user);
        _uow.Setup(u => u.Roles.GetRoleNamesForUserAsync(user.Id.ToString(), null, default))
            .ReturnsAsync((IReadOnlyList<string>)new List<string> { "user" });

        var handler = new GetUserByIdQueryHandler(_uow.Object);
        var result  = await handler.Handle(new GetUserByIdQuery(user.Id.Value), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Email.Should().Be("user@test.com");
        result.Value.FullName.Should().Be("John Doe");
        result.Value.Roles.Should().Contain("user");
    }

    [Fact]
    public async Task GetUserById_NonExistingUser_ReturnsUserNotFound()
    {
        var id = Guid.NewGuid();
        _uow.Setup(u => u.Users.GetByIdAsync(UserId.From(id), default)).ReturnsAsync((User?)null);

        var handler = new GetUserByIdQueryHandler(_uow.Object);
        var result  = await handler.Handle(new GetUserByIdQuery(id), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AuthErrors.UserNotFound);
    }
}
