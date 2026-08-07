using EnterpriseApp.Application.Common.Interfaces;
using EnterpriseApp.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace EnterpriseApp.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Automatically stamps <c>CreatedAt/By</c> and <c>UpdatedAt/By</c> on any
/// entity that inherits from <see cref="AuditableEntity{TId}"/> before
/// SaveChanges is persisted.
/// </summary>
public sealed class AuditableEntitySaveChangesInterceptor(
    ICurrentUserService currentUser)
    : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData      eventData,
        InterceptionResult<int> result)
    {
        UpdateAuditFields(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData      eventData,
        InterceptionResult<int> result,
        CancellationToken       cancellationToken = default)
    {
        UpdateAuditFields(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void UpdateAuditFields(DbContext? context)
    {
        if (context is null) return;

        var now    = DateTimeOffset.UtcNow;
        var userId = currentUser.UserId;

        foreach (var entry in context.ChangeTracker.Entries())
        {
            // Use duck-typing via reflection-free interface pattern.
            // The base class properties are public so we can access them via the base type.
            if (entry.Entity is not IAuditInfo audit) continue;

            switch (entry.State)
            {
                case EntityState.Added:
                    audit.CreatedAt = now;
                    audit.CreatedBy = userId;
                    break;
                case EntityState.Modified:
                    audit.UpdatedAt = now;
                    audit.UpdatedBy = userId;
                    break;
            }
        }
    }
}
