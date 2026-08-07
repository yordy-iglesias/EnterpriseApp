using EnterpriseApp.Domain.DomainEvents.Authorization;
using EnterpriseApp.Domain.Entities.Authorization;
using EnterpriseApp.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace EnterpriseApp.Domain.Tests.Authorization;

public sealed class RoleTests
{
    private static Role NewRole(bool system = false, Guid? tenant = null) =>
        Role.Create(
            name:           "nurse",
            normalizedName: "NURSE",
            description:    null,
            tenantId:       tenant,
            parentRoleId:   null,
            isSystem:       system,
            createdBy:      "tester");

    private static Permission NewPermission(string code = "patient.view", Guid? tenant = null) =>
        Permission.Create(code, "Display", null, isSensitive: false, isSystem: false, tenant, "tester");

    [Fact]
    public void Create_RaisesRoleCreatedEvent()
    {
        var role = NewRole();
        role.DomainEvents.Should().ContainSingle(e => e is RoleCreated);
    }

    [Fact]
    public void GrantPermission_NewPermission_AddsAndRaisesEvents()
    {
        var role = NewRole();
        role.ClearDomainEvents();

        role.GrantPermission(NewPermission(), "admin");

        role.RolePermissions.Should().HaveCount(1);
        role.DomainEvents.Should().Contain(e => e is PermissionGranted);
        role.DomainEvents.Should().Contain(e => e is RolePermissionsChanged);
    }

    [Fact]
    public void GrantPermission_Duplicate_Throws()
    {
        var role = NewRole();
        var perm = NewPermission();
        role.GrantPermission(perm, "admin");

        var act = () => role.GrantPermission(perm, "admin");

        act.Should().Throw<DomainException>().WithMessage("*already has permission*");
    }

    [Fact]
    public void GrantPermission_CrossTenant_Throws()
    {
        var roleTenant = Guid.NewGuid();
        var permTenant = Guid.NewGuid();
        var role = NewRole(tenant: roleTenant);
        var perm = NewPermission(tenant: permTenant);

        var act = () => role.GrantPermission(perm, "admin");

        act.Should().Throw<DomainException>().WithMessage("*different tenant*");
    }

    [Fact]
    public void RevokePermission_NotGranted_Throws()
    {
        var role = NewRole();
        var perm = NewPermission();

        var act = () => role.RevokePermission(perm.Id, "admin");

        act.Should().Throw<DomainException>().WithMessage("*does not have*");
    }

    [Fact]
    public void Rename_SystemRole_Throws()
    {
        var role = NewRole(system: true);
        var act = () => role.Rename("super-nurse", "admin");
        act.Should().Throw<DomainException>().WithMessage("*System roles cannot be renamed*");
    }

    [Fact]
    public void Delete_SystemRole_Throws()
    {
        var role = NewRole(system: true);
        var act = () => role.Delete("admin");
        act.Should().Throw<DomainException>().WithMessage("*System roles cannot be deleted*");
    }

    [Fact]
    public void RefreshPermissionsSnapshot_StoresSignedSnapshot_AndTimestamp()
    {
        var role = NewRole();
        role.RefreshPermissionsSnapshot("signed.jwt.snapshot");

        role.PermissionsSnapshot.Should().Be("signed.jwt.snapshot");
        role.PermissionsSnapshotUpdatedAt.Should().NotBeNull();
    }
}
