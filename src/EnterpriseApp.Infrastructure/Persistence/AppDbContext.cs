using EnterpriseApp.Application.Common.Interfaces;
using EnterpriseApp.Domain.Entities;
using EnterpriseApp.Domain.Entities.Authorization;
using EnterpriseApp.Infrastructure.Persistence.Interceptors;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseApp.Infrastructure.Persistence;

/// <summary>
/// EF Core 9 DbContext.
/// <para>Implements <see cref="IApplicationDbContext"/> so Application handlers
/// can depend on the interface, not the concrete EF type.</para>
/// <para>Domain events are dispatched after SaveChangesAsync via
/// <see cref="DomainEventDispatcherInterceptor"/>.</para>
/// <para>Interceptors are optional (null-safe) so the design-time
/// <see cref="AppDbContextFactory"/> can create instances without a DI container.</para>
/// </summary>
public sealed class AppDbContext(
    DbContextOptions<AppDbContext>        options,
    AuditableEntitySaveChangesInterceptor? auditInterceptor,
    DomainEventDispatcherInterceptor?      domainEventInterceptor)
    : DbContext(options), IApplicationDbContext
{
    public DbSet<TodoItem> TodoItems => Set<TodoItem>();

    // ── Authorization ────────────────────────────────────────────────────────
    public DbSet<Permission>          Permissions          => Set<Permission>();
    public DbSet<Role>                Roles                => Set<Role>();
    public DbSet<RolePermission>      RolePermissions      => Set<RolePermission>();
    public DbSet<UserRole>            UserRoles            => Set<UserRole>();
    public DbSet<PermissionAuditLog>  PermissionAuditLogs  => Set<PermissionAuditLog>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Only register interceptors when they are available (not during design-time migrations).
        if (auditInterceptor is not null && domainEventInterceptor is not null)
            optionsBuilder.AddInterceptors(auditInterceptor, domainEventInterceptor);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Apply all IEntityTypeConfiguration<T> found in this assembly.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Global query filter: exclude soft-deleted rows from all queries.
        modelBuilder.Entity<TodoItem>().HasQueryFilter(t => !t.IsDeleted);

        base.OnModelCreating(modelBuilder);
    }
}
