using EnterpriseApp.Application.Common.Interfaces;
using EnterpriseApp.Domain.Entities;
using EnterpriseApp.Domain.Entities.Authorization;
using EnterpriseApp.Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseApp.Infrastructure.Persistence;

/// <summary>
/// EF Core 9 DbContext.
/// <para>Implements <see cref="IApplicationDbContext"/> so Application handlers
/// can depend on the interface, not the concrete EF type.</para>
/// <para>Domain events are dispatched after SaveChangesAsync via
/// <see cref="DomainEventDispatcherInterceptor"/>, which is registered once
/// through the <c>AddDbContext</c> factory in InfrastructureServiceExtensions.</para>
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : DbContext(options), IApplicationDbContext
{
    public DbSet<TodoItem> TodoItems => Set<TodoItem>();

    // ── Authorization ────────────────────────────────────────────────────────
    public DbSet<Permission>          Permissions          => Set<Permission>();
    public DbSet<Role>                Roles                => Set<Role>();
    public DbSet<RolePermission>      RolePermissions      => Set<RolePermission>();
    public DbSet<UserRole>            UserRoles            => Set<UserRole>();
    public DbSet<PermissionAuditLog>  PermissionAuditLogs  => Set<PermissionAuditLog>();

    // ── Identity ─────────────────────────────────────────────────────────────
    public DbSet<User>          Users         => Set<User>();
    public DbSet<RefreshToken>  RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Apply all IEntityTypeConfiguration<T> found in this assembly.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Global query filter: exclude soft-deleted rows from all queries.
        modelBuilder.Entity<TodoItem>().HasQueryFilter(t => !t.IsDeleted);
        modelBuilder.Entity<User>().HasQueryFilter(u => !u.IsDeleted);

        base.OnModelCreating(modelBuilder);
    }
}
