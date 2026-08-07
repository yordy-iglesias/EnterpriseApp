namespace EnterpriseApp.Domain.Common;

/// <summary>
/// Marker interface implemented by <see cref="AuditableEntity{TId}"/>.
/// Allows Infrastructure interceptors to stamp audit fields without referencing
/// the concrete generic base class.
/// </summary>
public interface IAuditInfo
{
    DateTimeOffset  CreatedAt  { get; set; }
    string?         CreatedBy  { get; set; }
    DateTimeOffset? UpdatedAt  { get; set; }
    string?         UpdatedBy  { get; set; }
}
