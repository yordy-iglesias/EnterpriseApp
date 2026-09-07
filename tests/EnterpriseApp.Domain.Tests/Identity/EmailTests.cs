using EnterpriseApp.Domain.Exceptions;
using EnterpriseApp.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace EnterpriseApp.Domain.Tests.Identity;

public sealed class EmailTests
{
    [Theory]
    [InlineData("User@Example.COM", "user@example.com")]
    [InlineData("  admin@test.io  ", "admin@test.io")]
    public void From_ValidEmail_NormalizesToLowercase(string input, string expectedNormalized)
    {
        var email = Email.From(input);
        email.Normalized.Should().Be(expectedNormalized);
        email.Value.Should().Be(input.Trim());
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("notanemail")]
    [InlineData("missing@")]
    [InlineData("@nodomain.com")]
    public void From_InvalidEmail_ThrowsDomainException(string input)
    {
        var act = () => Email.From(input);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Equality_SameNormalized_AreEqual()
    {
        var a = Email.From("User@Test.com");
        var b = Email.From("user@test.com");
        a.Should().Be(b);
    }

    [Fact]
    public void ImplicitConversion_ReturnsValue()
    {
        var email = Email.From("x@y.io");
        string s = email;
        s.Should().Be("x@y.io");
    }
}
