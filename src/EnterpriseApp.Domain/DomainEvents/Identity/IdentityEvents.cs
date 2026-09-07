using EnterpriseApp.Domain.Common;
using EnterpriseApp.Domain.Entities.Identity;

namespace EnterpriseApp.Domain.DomainEvents.Identity;

public sealed record UserCreated(
    UserId  UserId,
    string  Email,
    Guid?   TenantId,
    string? CreatedBy) : DomainEvent;

public sealed record UserLoggedIn(
    UserId UserId) : DomainEvent;

public sealed record UserLoginFailed(
    UserId UserId) : DomainEvent;

public sealed record UserLockedOut(
    UserId         UserId,
    DateTimeOffset LockoutEndsAt) : DomainEvent;

public sealed record UserPasswordChanged(
    UserId  UserId,
    string? ChangedBy) : DomainEvent;

public sealed record UserActivated(
    UserId  UserId,
    string? ActivatedBy) : DomainEvent;

public sealed record UserDeactivated(
    UserId  UserId,
    string? DeactivatedBy) : DomainEvent;

public sealed record UserDeleted(
    UserId  UserId,
    string? DeletedBy) : DomainEvent;

public sealed record RefreshTokenReuseDetected(
    UserId UserId,
    string IpAddress) : DomainEvent;
