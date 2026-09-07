using EnterpriseApp.Domain.Common;
using EnterpriseApp.Domain.Exceptions;

namespace EnterpriseApp.Domain.ValueObjects;

public sealed class Email : ValueObject
{
    private Email() { }

    private Email(string value, string normalized)
    {
        Value      = value;
        Normalized = normalized;
    }

    public string Value      { get; private set; } = default!;
    public string Normalized { get; private set; } = default!;

    public static Email From(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new DomainException("Email cannot be empty.");
        var trimmed = email.Trim();
        if (!IsValid(trimmed))
            throw new DomainException($"'{trimmed}' is not a valid email address.");
        return new Email(trimmed, trimmed.ToLowerInvariant());
    }

    private static bool IsValid(string email)
    {
        var at  = email.IndexOf('@');
        var dot = email.LastIndexOf('.');
        return at > 0 && dot > at + 1 && dot < email.Length - 1;
    }

    protected override IEnumerable<object?> GetAtomicValues()
    {
        yield return Normalized;
    }

    public static implicit operator string(Email email) => email.Value;
    public override string ToString() => Value;
}
