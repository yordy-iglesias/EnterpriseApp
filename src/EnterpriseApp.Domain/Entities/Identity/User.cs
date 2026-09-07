using EnterpriseApp.Domain.Common;
using EnterpriseApp.Domain.DomainEvents.Identity;
using EnterpriseApp.Domain.ValueObjects;

namespace EnterpriseApp.Domain.Entities.Identity;

public sealed class User : AuditableEntity<UserId>
{
    private readonly List<RefreshToken> _refreshTokens = [];

    private User() : base(UserId.New()) { }

    // ── Identity ──────────────────────────────────────────────────────────────
    public Email   Email           { get; private set; } = default!;
    public string  NormalizedEmail { get; private set; } = default!;
    public string  PasswordHash    { get; private set; } = default!;
    public string  SecurityStamp   { get; private set; } = Guid.NewGuid().ToString("N");

    // ── Profile ───────────────────────────────────────────────────────────────
    public string FirstName { get; private set; } = default!;
    public string LastName  { get; private set; } = default!;
    public string FullName  => $"{FirstName} {LastName}";

    // ── Status ────────────────────────────────────────────────────────────────
    public bool            IsActive        { get; private set; } = true;
    public bool            EmailConfirmed  { get; private set; }
    public Guid?           TenantId        { get; private set; }

    // ── Lockout ───────────────────────────────────────────────────────────────
    public int             AccessFailedCount { get; private set; }
    public DateTimeOffset? LockoutEndsAt     { get; private set; }
    public DateTimeOffset? LastLoginAt       { get; private set; }

    public IReadOnlyCollection<RefreshToken> RefreshTokens => _refreshTokens.AsReadOnly();

    // ── Factory ───────────────────────────────────────────────────────────────
    public static User Create(
        Email   email,
        string  passwordHash,
        string  firstName,
        string  lastName,
        Guid?   tenantId,
        string? createdBy)
    {
        ArgumentNullException.ThrowIfNull(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(firstName);
        ArgumentException.ThrowIfNullOrWhiteSpace(lastName);

        var user = new User
        {
            Email           = email,
            NormalizedEmail = email.Normalized,
            PasswordHash    = passwordHash,
            FirstName       = firstName.Trim(),
            LastName        = lastName.Trim(),
            TenantId        = tenantId,
            CreatedBy       = createdBy,
        };

        user.RaiseDomainEvent(new UserCreated(
            UserId:    user.Id,
            Email:     email.Value,
            TenantId:  tenantId,
            CreatedBy: createdBy));

        return user;
    }

    // ── Behaviour ─────────────────────────────────────────────────────────────

    public void ConfirmEmail() => EmailConfirmed = true;

    public void ChangePassword(string newHash, string? changedBy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newHash);
        PasswordHash  = newHash;
        SecurityStamp = Guid.NewGuid().ToString("N");
        RevokeAllRefreshTokens("password_changed");
        UpdatedBy = changedBy;
        RaiseDomainEvent(new UserPasswordChanged(Id, changedBy));
    }

    public void RecordFailedLogin(int maxAttempts, TimeSpan lockoutDuration)
    {
        AccessFailedCount++;
        RaiseDomainEvent(new UserLoginFailed(Id));

        if (AccessFailedCount >= maxAttempts)
        {
            LockoutEndsAt = DateTimeOffset.UtcNow.Add(lockoutDuration);
            RaiseDomainEvent(new UserLockedOut(Id, LockoutEndsAt.Value));
        }
    }

    public void RecordSuccessfulLogin()
    {
        AccessFailedCount = 0;
        LockoutEndsAt     = null;
        LastLoginAt       = DateTimeOffset.UtcNow;
        RaiseDomainEvent(new UserLoggedIn(Id));
    }

    public bool IsLockedOut(DateTimeOffset now) =>
        LockoutEndsAt is not null && LockoutEndsAt > now;

    public void Activate(string? by)
    {
        IsActive  = true;
        UpdatedBy = by;
        RaiseDomainEvent(new UserActivated(Id, by));
    }

    public void Deactivate(string? by)
    {
        IsActive  = false;
        UpdatedBy = by;
        RevokeAllRefreshTokens("user_deactivated");
        RaiseDomainEvent(new UserDeactivated(Id, by));
    }

    public void Delete(string? by)
    {
        RevokeAllRefreshTokens("user_deleted");
        SoftDelete(by);
        RaiseDomainEvent(new UserDeleted(Id, by));
    }

    public void UpdateProfile(string firstName, string lastName, string? updatedBy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(firstName);
        ArgumentException.ThrowIfNullOrWhiteSpace(lastName);
        FirstName = firstName.Trim();
        LastName  = lastName.Trim();
        UpdatedBy = updatedBy;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    // ── Refresh Tokens ────────────────────────────────────────────────────────

    public void IssueRefreshToken(string tokenHash, DateTimeOffset expiresAt, string? ip)
    {
        var token = RefreshToken.Create(Id, tokenHash, expiresAt, ip);
        _refreshTokens.Add(token);
    }

    public bool RotateRefreshToken(
        string tokenHash, string newHash, DateTimeOffset expiresAt, string? ip)
    {
        var old = _refreshTokens.FirstOrDefault(t => t.TokenHash == tokenHash);
        if (old is null) return false;

        if (!old.IsActive)
        {
            RevokeAllRefreshTokens("reuse_detected");
            RaiseDomainEvent(new RefreshTokenReuseDetected(Id, ip ?? "unknown"));
            return false;
        }

        old.Revoke("rotated", ip, newHash);
        IssueRefreshToken(newHash, expiresAt, ip);
        return true;
    }

    public RefreshToken? GetActiveRefreshToken(string tokenHash) =>
        _refreshTokens.FirstOrDefault(t => t.TokenHash == tokenHash && t.IsActive);

    public void RevokeAllRefreshTokens(string reason)
    {
        foreach (var t in _refreshTokens.Where(t => t.IsActive))
            t.Revoke(reason, null);
    }
}
