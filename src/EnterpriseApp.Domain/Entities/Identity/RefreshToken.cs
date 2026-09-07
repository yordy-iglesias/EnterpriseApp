using EnterpriseApp.Domain.Common;

namespace EnterpriseApp.Domain.Entities.Identity;

public sealed class RefreshToken : AuditableEntity<Guid>
{
    private RefreshToken() : base(Guid.NewGuid()) { }

    public UserId          UserId         { get; private set; } = default!;
    public string          TokenHash      { get; private set; } = default!;
    public DateTimeOffset  ExpiresAt      { get; private set; }
    public string?         CreatedByIp    { get; private set; }
    public DateTimeOffset? RevokedAt      { get; private set; }
    public string?         RevokedByIp    { get; private set; }
    public string?         ReplacedByHash { get; private set; }
    public string?         RevokedReason  { get; private set; }

    public bool IsActive => RevokedAt is null && DateTimeOffset.UtcNow < ExpiresAt;

    public static RefreshToken Create(UserId userId, string tokenHash, DateTimeOffset expiresAt, string? createdByIp)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);
        return new RefreshToken
        {
            UserId      = userId,
            TokenHash   = tokenHash,
            ExpiresAt   = expiresAt,
            CreatedByIp = createdByIp,
        };
    }

    public void Revoke(string reason, string? byIp, string? replacedByHash = null)
    {
        RevokedAt      = DateTimeOffset.UtcNow;
        RevokedByIp    = byIp;
        RevokedReason  = reason;
        ReplacedByHash = replacedByHash;
    }
}
