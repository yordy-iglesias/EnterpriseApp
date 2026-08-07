using EnterpriseApp.Application.Common.Interfaces;
using EnterpriseApp.Application.Features.TodoItems.Commands.CreateTodo;
using EnterpriseApp.Domain.Entities;
using EnterpriseApp.Domain.Interfaces.Repositories;
using EnterpriseApp.Domain.ValueObjects;
using FluentAssertions;
using Moq;
using Xunit;

namespace EnterpriseApp.Application.Tests.Features.TodoItems;

public sealed class CreateTodoCommandHandlerTests
{
    private readonly Mock<IUnitOfWork>         _uowMock    = new();
    private readonly Mock<ITodoRepository>     _repoMock   = new();
    private readonly Mock<ICurrentUserService> _userMock   = new();
    private readonly Mock<ICacheService>       _cacheMock  = new();

    public CreateTodoCommandHandlerTests()
    {
        _uowMock.SetupGet(u => u.Todos).Returns(_repoMock.Object);
        _userMock.SetupGet(u => u.UserId).Returns("user-test");
    }

    [Fact]
    public async Task Handle_ValidCommand_AddsItemAndReturnsGuid()
    {
        // Arrange
        var capturedTodo = default(TodoItem);
        _repoMock
            .Setup(r => r.AddAsync(It.IsAny<TodoItem>(), It.IsAny<CancellationToken>()))
            .Callback<TodoItem, CancellationToken>((t, _) => capturedTodo = t)
            .Returns(Task.CompletedTask);

        _uowMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var handler = new CreateTodoCommandHandler(_uowMock.Object, _userMock.Object, _cacheMock.Object);
        var cmd     = new CreateTodoCommand("Test title", null, TodoPriority.Medium, null);

        // Act
        var resultId = await handler.Handle(cmd, CancellationToken.None);

        // Assert
        resultId.Should().NotBeEmpty();
        capturedTodo.Should().NotBeNull();
        capturedTodo!.Title.Should().Be("Test title");
        capturedTodo.CreatedBy.Should().Be("user-test");

        _repoMock.Verify(r => r.AddAsync(It.IsAny<TodoItem>(), default), Times.Once);
        _uowMock.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task Handle_ValidCommand_ReturnedIdMatchesTodoId()
    {
        TodoItem? capturedTodo = null;
        _repoMock
            .Setup(r => r.AddAsync(It.IsAny<TodoItem>(), default))
            .Callback<TodoItem, CancellationToken>((t, _) => capturedTodo = t)
            .Returns(Task.CompletedTask);
        _uowMock.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        var handler  = new CreateTodoCommandHandler(_uowMock.Object, _userMock.Object, _cacheMock.Object);
        var resultId = await handler.Handle(
            new CreateTodoCommand("Another", null, TodoPriority.High, null),
            CancellationToken.None);

        resultId.Should().Be(capturedTodo!.Id.Value);
    }
}
