using EnterpriseApp.Domain.Entities;
using EnterpriseApp.Domain.Exceptions;
using EnterpriseApp.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace EnterpriseApp.Domain.Tests.Entities;

public sealed class TodoItemTests
{
    // ── Factory ──────────────────────────────────────────────────────────────

    [Fact]
    public void Create_WithValidArgs_ReturnsItemWithPendingStatus()
    {
        var todo = TodoItem.Create("Buy milk", null, TodoPriority.Low, null, "user-1");

        todo.Title    .Should().Be("Buy milk");
        todo.Status   .Should().Be(TodoStatus.Pending);
        todo.Priority .Should().Be(TodoPriority.Low);
        todo.CreatedBy.Should().Be("user-1");
    }

    [Fact]
    public void Create_RaisesTodoItemCreatedEvent()
    {
        var todo = TodoItem.Create("Task A", null, TodoPriority.Medium, null, null);

        todo.DomainEvents.Should().ContainSingle(e =>
            e is DomainEvents.TodoItemCreatedEvent);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyTitle_ThrowsArgumentException(string title)
    {
        var act = () => TodoItem.Create(title, null, TodoPriority.Low, null, null);

        act.Should().Throw<ArgumentException>();
    }

    // ── Complete ─────────────────────────────────────────────────────────────

    [Fact]
    public void Complete_PendingItem_SetsStatusCompleted()
    {
        var todo = TodoItem.Create("Task", null, TodoPriority.Low, null, null);
        todo.Complete("user-2");

        todo.Status.Should().Be(TodoStatus.Completed);
    }

    [Fact]
    public void Complete_AlreadyCompleted_ThrowsDomainException()
    {
        var todo = TodoItem.Create("Task", null, TodoPriority.Low, null, null);
        todo.Complete(null);

        var act = () => todo.Complete(null);
        act.Should().Throw<DomainException>()
           .WithMessage("*already completed*");
    }

    [Fact]
    public void Complete_CancelledItem_ThrowsDomainException()
    {
        var todo = TodoItem.Create("Task", null, TodoPriority.Low, null, null);
        todo.Cancel(null);

        var act = () => todo.Complete(null);
        act.Should().Throw<DomainException>()
           .WithMessage("*cancelled*");
    }

    // ── Cancel ───────────────────────────────────────────────────────────────

    [Fact]
    public void Cancel_CompletedItem_ThrowsDomainException()
    {
        var todo = TodoItem.Create("Task", null, TodoPriority.Low, null, null);
        todo.Complete(null);

        var act = () => todo.Cancel(null);
        act.Should().Throw<DomainException>();
    }

    // ── Update ───────────────────────────────────────────────────────────────

    [Fact]
    public void Update_CancelledItem_ThrowsDomainException()
    {
        var todo = TodoItem.Create("Task", null, TodoPriority.Low, null, null);
        todo.Cancel(null);

        var act = () => todo.Update("New title", null, TodoPriority.High, null, null);
        act.Should().Throw<DomainException>()
           .WithMessage("*cancelled*");
    }

    // ── SoftDelete ────────────────────────────────────────────────────────────

    [Fact]
    public void SoftDelete_SetsIsDeletedAndDeletedAt()
    {
        var todo = TodoItem.Create("Task", null, TodoPriority.Low, null, null);
        todo.SoftDelete("admin");

        todo.IsDeleted.Should().BeTrue();
        todo.DeletedAt.Should().NotBeNull();
        todo.UpdatedBy.Should().Be("admin");
    }

    // ── Strongly-typed Id ─────────────────────────────────────────────────────

    [Fact]
    public void TodoId_New_GeneratesUniqueIds()
    {
        var id1 = TodoId.New();
        var id2 = TodoId.New();

        id1.Should().NotBe(id2);
    }

    [Fact]
    public void TodoId_From_RoundTripsCorrectly()
    {
        var guid = Guid.NewGuid();
        var id   = TodoId.From(guid);

        id.Value.Should().Be(guid);
        id.ToString().Should().Be(guid.ToString());
    }
}
