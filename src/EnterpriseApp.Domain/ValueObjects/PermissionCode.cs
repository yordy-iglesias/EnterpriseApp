using System.Text.RegularExpressions;
using EnterpriseApp.Domain.Common;
using EnterpriseApp.Domain.Exceptions;

namespace EnterpriseApp.Domain.ValueObjects;

/// <summary>
/// Strongly-typed permission code value object.
/// <para>Format: <c>{module}.{action}[.{qualifier}]</c> — e.g.
/// <c>module.view</c>, <c>module.view.own</c>, <c>invoice.export.pdf</c>.</para>
/// <para>Validates the format on construction. See <c>.claude/rules/authorization.md §2</c>.</para>
/// </summary>
public sealed partial class PermissionCode : ValueObject
{
    /// <summary>
    /// kebab-case segments separated by '.', between 2 and 3 segments.
    /// </summary>
    [GeneratedRegex(@"^[a-z][a-z0-9-]*(\.[a-z][a-z0-9-]*){1,2}$", RegexOptions.CultureInvariant, 200)]
    private static partial Regex CodeRegex();

    public string Value     { get; }
    public string Module    { get; }
    public string Action    { get; }
    public string? Qualifier { get; }

    private PermissionCode(string value, string module, string action, string? qualifier)
    {
        Value     = value;
        Module    = module;
        Action    = action;
        Qualifier = qualifier;
    }

    public static PermissionCode From(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        var normalized = value.Trim().ToLowerInvariant();
        if (!CodeRegex().IsMatch(normalized))
            throw new DomainException(
                $"Invalid permission code '{value}'. " +
                "Expected format: '{module}.{action}[.{qualifier}]' (kebab-case, 2-3 segments).");

        var parts = normalized.Split('.');
        return new PermissionCode(
            value:     normalized,
            module:    parts[0],
            action:    parts[1],
            qualifier: parts.Length == 3 ? parts[2] : null);
    }

    public static bool TryFrom(string? value, out PermissionCode? code)
    {
        code = null;
        if (string.IsNullOrWhiteSpace(value)) return false;
        var normalized = value.Trim().ToLowerInvariant();
        if (!CodeRegex().IsMatch(normalized)) return false;

        var parts = normalized.Split('.');
        code = new PermissionCode(
            value:     normalized,
            module:    parts[0],
            action:    parts[1],
            qualifier: parts.Length == 3 ? parts[2] : null);
        return true;
    }

    protected override IEnumerable<object?> GetAtomicValues()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    public static implicit operator string(PermissionCode code) => code.Value;
}
