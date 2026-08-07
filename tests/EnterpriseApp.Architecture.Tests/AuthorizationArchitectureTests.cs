using System.Reflection;
using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace EnterpriseApp.ArchitectureTests;

/// <summary>
/// Architectural guardrails for the authorization module — enforces the
/// rules in <c>.claude/rules/authorization.md</c> and prevents the legacy
/// NHCS pattern (using <c>ConcurrencyStamp</c> for permission storage) from
/// sneaking back in.
/// </summary>
public sealed class AuthorizationArchitectureTests
{
    private static readonly Assembly DomainAssembly         = typeof(Domain.Common.BaseEntity).Assembly;
    private static readonly Assembly ApplicationAssembly    = typeof(Application.DependencyInjection.ApplicationServiceExtensions).Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(Infrastructure.Persistence.AppDbContext).Assembly;

    [Fact]
    public void Application_ShouldNot_ReferenceAspNetCoreAuthorization()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot().HaveDependencyOn("Microsoft.AspNetCore.Authorization")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Application stays framework-agnostic. " +
                     "Authorization attributes/handlers belong in API/Infrastructure.");
    }

    [Fact]
    public void RolePermission_StoredInDedicatedSnapshotField_NotConcurrencyStamp()
    {
        // Guards against regressing to the legacy NHCS pattern of storing the
        // permissions JWT inside IdentityRole.ConcurrencyStamp.
        // Domain Role must expose PermissionsSnapshot — a dedicated field.
        var roleType = typeof(Domain.Entities.Authorization.Role);

        roleType.GetProperty("PermissionsSnapshot")
            .Should().NotBeNull("permissions are stored in a dedicated field, not ConcurrencyStamp");
    }

    [Fact]
    public void PermissionAuditLog_HasNoMutatingMethods()
    {
        var t = typeof(Domain.Entities.Authorization.PermissionAuditLog);

        // Allow only `Record` (factory) and inherited members from BaseEntity.
        var declaredPublicMethods = t.GetMethods(BindingFlags.Public | BindingFlags.Static |
                                                 BindingFlags.Instance | BindingFlags.DeclaredOnly)
                                     .Where(m => !m.IsSpecialName) // ignore property accessors
                                     .Select(m => m.Name)
                                     .ToList();

        declaredPublicMethods.Should().BeEquivalentTo(new[] { "Record" },
            because: "PermissionAuditLog is append-only — no Update/Delete/Modify methods allowed.");
    }

    [Fact]
    public void IPermissionAuditLogRepository_DoesNotExposeUpdateOrRemove()
    {
        var t = typeof(Domain.Interfaces.Repositories.Authorization.IPermissionAuditLogRepository);
        var methods = t.GetMethods().Select(m => m.Name).ToList();

        methods.Should().NotContain("Update");
        methods.Should().NotContain("Remove");
        methods.Should().Contain("AddAsync");
    }

    [Fact]
    public void Application_AuthorizationFolder_DoesNotReferenceHttpContext()
    {
        // Application layer should not couple to HttpContext directly; ICurrentUser is the abstraction.
        var result = Types.InAssembly(ApplicationAssembly)
            .That().ResideInNamespace("EnterpriseApp.Application.Common.Authorization")
            .ShouldNot().HaveDependencyOn("Microsoft.AspNetCore.Http")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }
}
