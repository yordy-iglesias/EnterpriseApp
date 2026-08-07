using EnterpriseApp.Domain.Exceptions;
using EnterpriseApp.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace EnterpriseApp.Domain.Tests.Authorization;

public sealed class PermissionCodeTests
{
    [Theory]
    [InlineData("patient.view",          "patient", "view",   null)]
    [InlineData("patient.view.own",      "patient", "view",   "own")]
    [InlineData("invoice.export.pdf",    "invoice", "export", "pdf")]
    [InlineData("user.deactivate",       "user",    "deactivate", null)]
    [InlineData("multi-word.action-x",   "multi-word", "action-x", null)]
    public void From_ValidCode_ParsesIntoSegments(string code, string mod, string act, string? qualifier)
    {
        var parsed = PermissionCode.From(code);

        parsed.Value     .Should().Be(code);
        parsed.Module    .Should().Be(mod);
        parsed.Action    .Should().Be(act);
        parsed.Qualifier .Should().Be(qualifier);
    }

    [Theory]
    [InlineData("Patient.View")]            // PascalCase
    [InlineData("paciente.ver")]            // mixed-language allowed by regex but consistency anti-pattern
    [InlineData("patient")]                 // single segment
    [InlineData("a.b.c.d")]                 // 4 segments
    [InlineData("patient_view")]            // snake_case
    [InlineData("1patient.view")]           // starts with digit
    [InlineData("patient.")]                // trailing dot
    [InlineData(".view")]                   // leading dot
    [InlineData("")]                        // empty
    public void From_InvalidCode_Throws(string code)
    {
        var act = () => PermissionCode.From(code);
        act.Should().Throw<Exception>();
    }

    [Fact]
    public void From_IsCaseInsensitive_StoresLowercased()
    {
        var parsed = PermissionCode.From("PATIENT.VIEW");
        parsed.Value.Should().Be("patient.view");
    }

    [Fact]
    public void Equality_SameCode_ValueObjectsAreEqual()
    {
        var a = PermissionCode.From("user.create");
        var b = PermissionCode.From("user.create");

        a.Should().Be(b);
        (a == b).Should().BeTrue();
    }

    [Fact]
    public void TryFrom_InvalidCode_ReturnsFalse()
    {
        PermissionCode.TryFrom("invalid", out var code).Should().BeFalse();
        code.Should().BeNull();
    }
}
