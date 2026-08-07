using EnterpriseApp.Domain.Entities;
using EnterpriseApp.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnterpriseApp.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core fluent configuration for <see cref="TodoItem"/>.
/// Converts the strongly-typed <see cref="TodoId"/> to a <see cref="Guid"/> column
/// so the database stays clean of domain types.
/// </summary>
public sealed class TodoItemConfiguration : IEntityTypeConfiguration<TodoItem>
{
    public void Configure(EntityTypeBuilder<TodoItem> builder)
    {
        builder.ToTable("TodoItems");

        // Strongly-typed ID → Guid column conversion
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id)
               .HasConversion(
                   id => id.Value,
                   value => TodoId.From(value))
               .ValueGeneratedNever();

        builder.Property(t => t.Title)
               .IsRequired()
               .HasMaxLength(200);

        builder.Property(t => t.Description)
               .HasMaxLength(2000);

        builder.Property(t => t.Status)
               .HasConversion<string>()
               .HasMaxLength(20);

        builder.Property(t => t.Priority)
               .HasConversion<string>()
               .HasMaxLength(10);

        builder.Property(t => t.DueDate)
               .IsRequired(false);

        // Audit columns
        builder.Property(t => t.CreatedAt).IsRequired();
        builder.Property(t => t.CreatedBy).HasMaxLength(256);
        builder.Property(t => t.UpdatedAt).IsRequired(false);
        builder.Property(t => t.UpdatedBy).HasMaxLength(256);

        // Soft-delete columns
        builder.Property(t => t.IsDeleted).IsRequired().HasDefaultValue(false);
        builder.Property(t => t.DeletedAt).IsRequired(false);

        // Index for common query patterns
        builder.HasIndex(t => t.Status);
        builder.HasIndex(t => t.Priority);
        builder.HasIndex(t => new { t.Status, t.Priority });
        builder.HasIndex(t => t.IsDeleted);  // supports the global query filter
    }
}
