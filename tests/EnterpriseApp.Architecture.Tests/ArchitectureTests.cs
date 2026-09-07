using System.Reflection;
using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace EnterpriseApp.ArchitectureTests;

/// <summary>
/// Enforces Clean Architecture dependency rules using NetArchTest.
/// These tests guard against layer violations being introduced during feature development.
/// </summary>
public sealed class ArchitectureTests
{
    // ── Assembly references ───────────────────────────────────────────────────
    private static readonly Assembly DomainAssembly         = typeof(Domain.Common.BaseEntity).Assembly;
    private static readonly Assembly ApplicationAssembly    = typeof(Application.DependencyInjection.ApplicationServiceExtensions).Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(Infrastructure.Persistence.AppDbContext).Assembly;
    private static readonly Assembly ApiAssembly            = typeof(API.Controllers.TodosController).Assembly;
    // Note: Program is exposed via `public partial class Program {}` at end of Program.cs

    // ── Namespace constants ───────────────────────────────────────────────────
    private const string DomainNs         = "EnterpriseApp.Domain";
    private const string ApplicationNs    = "EnterpriseApp.Application";
    private const string InfrastructureNs = "EnterpriseApp.Infrastructure";
    private const string ApiNs            = "EnterpriseApp.API";

    // ── Domain layer rules ────────────────────────────────────────────────────

    [Fact]
    public void Domain_ShouldNot_DependOnApplication()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot().HaveDependencyOn(ApplicationNs).GetResult();
        result.IsSuccessful.Should().BeTrue(because: "Domain must not reference Application.");
    }

    [Fact]
    public void Domain_ShouldNot_DependOnInfrastructure()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot().HaveDependencyOn(InfrastructureNs).GetResult();
        result.IsSuccessful.Should().BeTrue(because: "Domain must not reference Infrastructure.");
    }

    [Fact]
    public void Domain_ShouldNot_DependOnApi()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot().HaveDependencyOn(ApiNs).GetResult();
        result.IsSuccessful.Should().BeTrue(because: "Domain must not reference API.");
    }

    // ── Application layer rules ───────────────────────────────────────────────

    [Fact]
    public void Application_ShouldNot_DependOnInfrastructure()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot().HaveDependencyOn(InfrastructureNs).GetResult();
        result.IsSuccessful.Should().BeTrue(because: "Application must not reference Infrastructure.");
    }

    [Fact]
    public void Application_ShouldNot_DependOnApi()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot().HaveDependencyOn(ApiNs).GetResult();
        result.IsSuccessful.Should().BeTrue(because: "Application must not reference API.");
    }

    // ── Infrastructure layer rules ────────────────────────────────────────────

    [Fact]
    public void Infrastructure_ShouldNot_DependOnApi()
    {
        var result = Types.InAssembly(InfrastructureAssembly)
            .ShouldNot().HaveDependencyOn(ApiNs).GetResult();
        result.IsSuccessful.Should().BeTrue(because: "Infrastructure must not reference API.");
    }

    // ── Naming conventions ────────────────────────────────────────────────────

    [Fact]
    public void CommandHandlers_ShouldEndWith_Handler()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .That()
            .ImplementInterface(typeof(MediatR.IRequestHandler<>))
            .Or()
            .ImplementInterface(typeof(MediatR.IRequestHandler<,>))
            .Should()
            .HaveNameEndingWith("Handler")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(because: "All MediatR handlers must end with 'Handler'.");
    }

    [Fact]
    public void DomainEntities_ShouldLiveIn_EntitiesNamespace()
    {
        var result = Types.InAssembly(DomainAssembly)
            .That()
            .Inherit(typeof(Domain.Common.BaseEntity))
            .And()
            .AreNotAbstract()
            .Should()
            .ResideInNamespace($"{DomainNs}.Entities")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "All domain entities should live in the Entities namespace.");
    }
}
