# Identity & Authentication Module — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implementar el módulo completo de identidad y autenticación — entidad `User`, `RefreshToken`, JWT emission, endpoints `/auth` y `/users`, y el fix de `StepUpMfaAuthorizationHandler` — para que la API sea completamente utilizable.

**Architecture:** Agregado `User` propio en Domain (sin ASP.NET Identity, sin NuGet nuevos). Application expone `IPasswordHasher` e `ITokenService` como interfaces; Infrastructure los implementa con `PasswordHasher<User>` (PBKDF2) y `System.IdentityModel.Tokens.Jwt`. `AuthOptions` controla TTLs, lockout, step-up MFA y seed del admin.

**Tech Stack:** .NET 9 / C# 13, MediatR 12, FluentValidation, EF Core 9, xUnit + Moq + FluentAssertions

**Spec:** `Docs/superpowers/specs/2026-09-02-identity-auth-design.md`

## Global Constraints

- Domain tiene CERO dependencias NuGet — nunca `using Microsoft.AspNetCore.*` ni `using Microsoft.Extensions.*` en Domain
- Todos los IDs son strongly-typed records con `New()` / `From(Guid)` / `ToString()`
- Eventos heredan `DomainEvent` (abstract record en `Domain/Common/IDomainEvent.cs`)
- Entidades heredan `AuditableEntity<TId>` — tiene `Id`, `CreatedAt/By`, `UpdatedAt/By`, `IsDeleted`, `DeletedAt`, `SoftDelete(userId?)`
- `ValueObject` usa `GetAtomicValues()` (no `GetEqualityComponents`)
- Handlers: primary constructors, `IRequestHandler<TCommand, Result<T>>`
- Commands create → `Result<Guid>` | Commands mutate → `Result` | Auth commands → `Result<AuthResponse>`
- `UserRole.UserId` es `string` (Guid.ToString()) — contrato fijo en todo el módulo
- Tests: Moq + FluentAssertions + xUnit, sin I/O real

---

## File Map

### Created
```
src/EnterpriseApp.Domain/
  ValueObjects/Email.cs
  Entities/Identity/UserId.cs
  Entities/Identity/RefreshToken.cs
  Entities/Identity/User.cs
  DomainEvents/Identity/IdentityEvents.cs
  Interfaces/Repositories/Identity/IUserRepository.cs

src/EnterpriseApp.Application/
  Common/Interfaces/IPasswordHasher.cs
  Common/Interfaces/ITokenService.cs
  Common/Errors/AuthErrors.cs
  Features/Identity/Auth/DTOs/AuthDtos.cs
  Features/Identity/Auth/Commands/Login/{LoginCommand,LoginCommandValidator,LoginCommandHandler}.cs
  Features/Identity/Auth/Commands/RefreshToken/{RefreshTokenCommand,RefreshTokenCommandHandler}.cs
  Features/Identity/Auth/Commands/Logout/{LogoutCommand,LogoutCommandHandler}.cs
  Features/Identity/Auth/Commands/ChangePassword/{ChangePasswordCommand,ChangePasswordCommandValidator,ChangePasswordCommandHandler}.cs
  Features/Identity/Auth/Queries/GetCurrentUser/{GetCurrentUserQuery,GetCurrentUserQueryHandler}.cs
  Features/Identity/Users/DTOs/UserDtos.cs
  Features/Identity/Users/Commands/CreateUser/{CreateUserCommand,CreateUserCommandValidator,CreateUserCommandHandler}.cs
  Features/Identity/Users/Commands/UpdateUser/{UpdateUserCommand,UpdateUserCommandValidator,UpdateUserCommandHandler}.cs
  Features/Identity/Users/Commands/ActivateUser/{ActivateUserCommand,ActivateUserCommandHandler}.cs
  Features/Identity/Users/Commands/DeactivateUser/{DeactivateUserCommand,DeactivateUserCommandHandler}.cs
  Features/Identity/Users/Commands/DeleteUser/{DeleteUserCommand,DeleteUserCommandHandler}.cs
  Features/Identity/Users/Queries/GetUsers/{GetUsersQuery,GetUsersQueryHandler}.cs
  Features/Identity/Users/Queries/GetUserById/{GetUserByIdQuery,GetUserByIdQueryHandler}.cs

src/EnterpriseApp.Infrastructure/
  Identity/AuthOptions.cs
  Identity/PasswordHasherAdapter.cs
  Identity/TokenService.cs
  Repositories/Identity/UserRepository.cs
  Persistence/Configurations/Identity/UserConfiguration.cs
  Persistence/Configurations/Identity/RefreshTokenConfiguration.cs

src/EnterpriseApp.API/Controllers/AuthController.cs
src/EnterpriseApp.API/Controllers/UsersController.cs

tests/EnterpriseApp.Domain.Tests/Identity/EmailTests.cs
tests/EnterpriseApp.Domain.Tests/Identity/UserTests.cs
tests/EnterpriseApp.Application.Tests/Features/Identity/LoginCommandHandlerTests.cs
tests/EnterpriseApp.Application.Tests/Features/Identity/RefreshTokenCommandHandlerTests.cs
```

### Modified
```
src/EnterpriseApp.Domain/Interfaces/Repositories/IUnitOfWork.cs          — add IUserRepository Users
src/EnterpriseApp.Application/Common/Interfaces/IApplicationDbContext.cs — add DbSet<User>, DbSet<RefreshToken>
src/EnterpriseApp.Infrastructure/Persistence/AppDbContext.cs             — DbSets + query filters
src/EnterpriseApp.Infrastructure/Persistence/Seeds/DatabaseSeeder.cs    — SeedAdminUserAsync
src/EnterpriseApp.Infrastructure/Authorization/StepUpMfaAuthorizationHandler.cs — StepUpMfaOptions
src/EnterpriseApp.Infrastructure/DependencyInjection/InfrastructureServiceExtensions.cs — register all new services
src/EnterpriseApp.API/appsettings.json                                   — Auth section
src/EnterpriseApp.API/Program.cs                                         — AddRateLimiter
```

---

## Task 1: Domain — Email VO + UserId + IdentityEvents

**Files:**
- Create: `src/EnterpriseApp.Domain/ValueObjects/Email.cs`
- Create: `src/EnterpriseApp.Domain/Entities/Identity/UserId.cs`
- Create: `src/EnterpriseApp.Domain/DomainEvents/Identity/IdentityEvents.cs`
- Test: `tests/EnterpriseApp.Domain.Tests/Identity/EmailTests.cs`

**Produces:**
- `Email` value object with `Value`, `Normalized`, `From(string)`, implicit `string` cast
- `UserId` record with `New()`, `From(Guid)`, `ToString()`
- 9 domain events: `UserCreated`, `UserLoggedIn`, `UserLoginFailed`, `UserLockedOut`, `UserPasswordChanged`, `UserActivated`, `UserDeactivated`, `UserDeleted`, `RefreshTokenReuseDetected`

- [ ] **Step 1: Write failing Email tests**

```csharp
// tests/EnterpriseApp.Domain.Tests/Identity/EmailTests.cs
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
```

- [ ] **Step 2: Run test — expect compile failure (Email not found)**

```bash
dotnet test tests/EnterpriseApp.Domain.Tests --filter "FullyQualifiedName~EmailTests" 2>&1 | head -20
```

- [ ] **Step 3: Implement Email VO**

```csharp
// src/EnterpriseApp.Domain/ValueObjects/Email.cs
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
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        var trimmed = email.Trim();
        if (!IsValid(trimmed))
            throw new DomainException($"'{trimmed}' is not a valid email address.");
        return new Email(trimmed, trimmed.ToLowerInvariant());
    }

    private static bool IsValid(string email)
    {
        var at   = email.IndexOf('@');
        var dot  = email.LastIndexOf('.');
        return at > 0 && dot > at + 1 && dot < email.Length - 1;
    }

    protected override IEnumerable<object?> GetAtomicValues()
    {
        yield return Normalized;
    }

    public static implicit operator string(Email email) => email.Value;
    public override string ToString() => Value;
}
```

- [ ] **Step 4: Implement UserId**

```csharp
// src/EnterpriseApp.Domain/Entities/Identity/UserId.cs
namespace EnterpriseApp.Domain.Entities.Identity;

public sealed record UserId(Guid Value)
{
    public static UserId New()            => new(Guid.NewGuid());
    public static UserId From(Guid value) => new(value);
    public override string ToString()     => Value.ToString();
}
```

- [ ] **Step 5: Implement IdentityEvents**

```csharp
// src/EnterpriseApp.Domain/DomainEvents/Identity/IdentityEvents.cs
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
    UserId          UserId,
    DateTimeOffset  LockoutEndsAt) : DomainEvent;

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
```

- [ ] **Step 6: Run Email tests — expect green**

```bash
dotnet test tests/EnterpriseApp.Domain.Tests --filter "FullyQualifiedName~EmailTests" -v minimal
```

- [ ] **Step 7: Commit**

```bash
git add src/EnterpriseApp.Domain/ValueObjects/Email.cs \
        src/EnterpriseApp.Domain/Entities/Identity/UserId.cs \
        src/EnterpriseApp.Domain/DomainEvents/Identity/IdentityEvents.cs \
        tests/EnterpriseApp.Domain.Tests/Identity/EmailTests.cs
git commit -m "feat(domain): add Email VO, UserId, and identity domain events"
```

---

## Task 2: Domain — RefreshToken + User entity

**Files:**
- Create: `src/EnterpriseApp.Domain/Entities/Identity/RefreshToken.cs`
- Create: `src/EnterpriseApp.Domain/Entities/Identity/User.cs`
- Test: `tests/EnterpriseApp.Domain.Tests/Identity/UserTests.cs`

**Produces:**
- `RefreshToken` entity with `IsActive`, `Revoke`, `ReplacedByHash`
- `User` entity with all behaviour from spec §3.3

- [ ] **Step 1: Write failing UserTests**

```csharp
// tests/EnterpriseApp.Domain.Tests/Identity/UserTests.cs
using EnterpriseApp.Domain.DomainEvents.Identity;
using EnterpriseApp.Domain.Entities.Identity;
using EnterpriseApp.Domain.Exceptions;
using EnterpriseApp.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace EnterpriseApp.Domain.Tests.Identity;

public sealed class UserTests
{
    private static User CreateUser(string email = "test@example.com") =>
        User.Create(
            email:        Email.From(email),
            passwordHash: "hash123",
            firstName:    "John",
            lastName:     "Doe",
            tenantId:     null,
            createdBy:    "system");

    [Fact]
    public void Create_ValidInput_RaisesUserCreatedEvent()
    {
        var user = CreateUser();
        user.DomainEvents.Should().ContainSingle(e => e is UserCreated);
        user.IsActive.Should().BeTrue();
        user.EmailConfirmed.Should().BeFalse();
    }

    [Fact]
    public void RecordFailedLogin_BelowThreshold_IncrementsCounter()
    {
        var user = CreateUser();
        user.RecordFailedLogin(5, TimeSpan.FromMinutes(15));
        user.AccessFailedCount.Should().Be(1);
        user.IsLockedOut(DateTimeOffset.UtcNow).Should().BeFalse();
    }

    [Fact]
    public void RecordFailedLogin_ReachesThreshold_LocksAccount()
    {
        var user = CreateUser();
        for (int i = 0; i < 5; i++)
            user.RecordFailedLogin(5, TimeSpan.FromMinutes(15));

        user.IsLockedOut(DateTimeOffset.UtcNow).Should().BeTrue();
        user.DomainEvents.Should().Contain(e => e is UserLockedOut);
    }

    [Fact]
    public void RecordSuccessfulLogin_ResetsFailed_AndSetsLastLogin()
    {
        var user = CreateUser();
        user.RecordFailedLogin(5, TimeSpan.FromMinutes(15));
        user.RecordSuccessfulLogin();

        user.AccessFailedCount.Should().Be(0);
        user.LastLoginAt.Should().NotBeNull();
        user.DomainEvents.Should().Contain(e => e is UserLoggedIn);
    }

    [Fact]
    public void IsLockedOut_AfterExpiry_ReturnsFalse()
    {
        var user = CreateUser();
        for (int i = 0; i < 5; i++)
            user.RecordFailedLogin(5, TimeSpan.FromMinutes(15));

        user.IsLockedOut(DateTimeOffset.UtcNow.AddMinutes(20)).Should().BeFalse();
    }

    [Fact]
    public void ChangePassword_RotatesSecurityStampAndRevokesTokens()
    {
        var user = CreateUser();
        user.ConfirmEmail();
        user.IssueRefreshToken("hash1", DateTimeOffset.UtcNow.AddDays(7), "127.0.0.1");
        var stampBefore = user.SecurityStamp;

        user.ChangePassword("newhash", "admin");

        user.SecurityStamp.Should().NotBe(stampBefore);
        user.DomainEvents.Should().Contain(e => e is UserPasswordChanged);
    }

    [Fact]
    public void Deactivate_RevokesAllTokens_RaisesEvent()
    {
        var user = CreateUser();
        user.ConfirmEmail();
        user.IssueRefreshToken("tok1", DateTimeOffset.UtcNow.AddDays(7), "ip");
        user.Deactivate("admin");

        user.IsActive.Should().BeFalse();
        user.DomainEvents.Should().Contain(e => e is UserDeactivated);
    }

    [Fact]
    public void RotateRefreshToken_MarksOldRevoked_AddsNew()
    {
        var user = CreateUser();
        user.IssueRefreshToken("oldhash", DateTimeOffset.UtcNow.AddDays(7), "ip");
        user.RotateRefreshToken("oldhash", "newhash", DateTimeOffset.UtcNow.AddDays(7), "ip");

        user.GetActiveRefreshToken("newhash").Should().NotBeNull();
        user.GetActiveRefreshToken("oldhash").Should().BeNull();
    }
}
```

- [ ] **Step 2: Run tests — expect compile failure**

```bash
dotnet test tests/EnterpriseApp.Domain.Tests --filter "FullyQualifiedName~UserTests" 2>&1 | head -20
```

- [ ] **Step 3: Implement RefreshToken**

```csharp
// src/EnterpriseApp.Domain/Entities/Identity/RefreshToken.cs
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
```

- [ ] **Step 4: Implement User entity**

```csharp
// src/EnterpriseApp.Domain/Entities/Identity/User.cs
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
```

- [ ] **Step 5: Run UserTests — expect green**

```bash
dotnet test tests/EnterpriseApp.Domain.Tests --filter "FullyQualifiedName~UserTests" -v minimal
```

- [ ] **Step 6: Commit**

```bash
git add src/EnterpriseApp.Domain/Entities/Identity/ \
        tests/EnterpriseApp.Domain.Tests/Identity/UserTests.cs
git commit -m "feat(domain): add User aggregate and RefreshToken entity"
```

---

## Task 3: Domain — IUserRepository + update IUnitOfWork

**Files:**
- Create: `src/EnterpriseApp.Domain/Interfaces/Repositories/Identity/IUserRepository.cs`
- Modify: `src/EnterpriseApp.Domain/Interfaces/Repositories/IUnitOfWork.cs`

**Produces:** `IUserRepository` interface, `IUnitOfWork.Users` property

- [ ] **Step 1: Create IUserRepository**

```csharp
// src/EnterpriseApp.Domain/Interfaces/Repositories/Identity/IUserRepository.cs
using EnterpriseApp.Domain.Common;
using EnterpriseApp.Domain.Entities.Identity;

namespace EnterpriseApp.Domain.Interfaces.Repositories.Identity;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(UserId id, CancellationToken ct = default);
    Task<User?> GetByNormalizedEmailAsync(string normalizedEmail, CancellationToken ct = default);
    Task<User?> GetByRefreshTokenHashAsync(string tokenHash, CancellationToken ct = default);
    Task<bool>  ExistsByEmailAsync(string normalizedEmail, CancellationToken ct = default);
    Task<PagedList<User>> GetPagedAsync(int page, int pageSize, CancellationToken ct = default);
    Task AddAsync(User user, CancellationToken ct = default);
}
```

- [ ] **Step 2: Add Users to IUnitOfWork**

Open `src/EnterpriseApp.Domain/Interfaces/Repositories/IUnitOfWork.cs` and add:

```csharp
// After the existing IRoleRepository Roles line, add:
IUserRepository Users { get; }
```

Full updated interface:
```csharp
using EnterpriseApp.Domain.Interfaces.Repositories.Identity;

public interface IUnitOfWork
{
    ITodoRepository Todos { get; }

    // ── Authorization aggregates ───────────────────────────────────────────────
    IRoleRepository                Roles          { get; }
    IPermissionRepository          Permissions    { get; }
    IUserRoleRepository            UserRoles      { get; }
    IPermissionAuditLogRepository  PermissionAuditLogs { get; }

    // ── Identity ──────────────────────────────────────────────────────────────
    IUserRepository Users { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
```

- [ ] **Step 3: Build to confirm no compile errors**

```bash
dotnet build src/EnterpriseApp.Domain/EnterpriseApp.Domain.csproj 2>&1 | tail -5
```

- [ ] **Step 4: Commit**

```bash
git add src/EnterpriseApp.Domain/Interfaces/
git commit -m "feat(domain): add IUserRepository and extend IUnitOfWork"
```

---

## Task 4: Application — Interfaces + Errors + DTOs

**Files:**
- Create: `src/EnterpriseApp.Application/Common/Interfaces/IPasswordHasher.cs`
- Create: `src/EnterpriseApp.Application/Common/Interfaces/ITokenService.cs`
- Create: `src/EnterpriseApp.Application/Common/Errors/AuthErrors.cs`
- Create: `src/EnterpriseApp.Application/Features/Identity/Auth/DTOs/AuthDtos.cs`
- Create: `src/EnterpriseApp.Application/Features/Identity/Users/DTOs/UserDtos.cs`

- [ ] **Step 1: IPasswordHasher**

```csharp
// src/EnterpriseApp.Application/Common/Interfaces/IPasswordHasher.cs
namespace EnterpriseApp.Application.Common.Interfaces;

public enum PasswordVerificationResult { Failed, Success, SuccessRehashNeeded }

public interface IPasswordHasher
{
    string Hash(string password);
    PasswordVerificationResult Verify(string hash, string password);
}
```

- [ ] **Step 2: ITokenService**

```csharp
// src/EnterpriseApp.Application/Common/Interfaces/ITokenService.cs
namespace EnterpriseApp.Application.Common.Interfaces;

public sealed record TokenSubject(
    string              UserId,
    string              Email,
    string              FullName,
    Guid?               TenantId,
    IEnumerable<string> RoleNames,
    IEnumerable<string> PermissionCodes,
    string              SnapshotVersion,
    DateTimeOffset      AuthTime);

public sealed record AccessTokenResult(string Token, DateTimeOffset ExpiresAt);
public sealed record RefreshTokenResult(string Plain, string Hash, DateTimeOffset ExpiresAt);

public interface ITokenService
{
    AccessTokenResult  GenerateAccessToken(TokenSubject subject);
    RefreshTokenResult GenerateRefreshToken();
}
```

- [ ] **Step 3: AuthErrors**

```csharp
// src/EnterpriseApp.Application/Common/Errors/AuthErrors.cs
using EnterpriseApp.Domain.Common;

namespace EnterpriseApp.Application.Common.Errors;

public static class AuthErrors
{
    public static readonly Error InvalidCredentials  = new("Auth.InvalidCredentials",  "Invalid credentials.");
    public static readonly Error AccountLocked       = new("Auth.AccountLocked",       "Account is temporarily locked.");
    public static readonly Error AccountInactive     = new("Auth.AccountInactive",     "Account is inactive.");
    public static readonly Error InvalidRefreshToken = new("Auth.InvalidRefreshToken", "Refresh token is invalid or expired.");
    public static readonly Error RefreshTokenReused  = new("Auth.RefreshTokenReused",  "Refresh token reuse detected. All sessions revoked.");
    public static readonly Error EmailAlreadyExists  = new("User.EmailAlreadyExists",  "A user with that email already exists.");
    public static readonly Error UserNotFound        = new("User.NotFound",            "User not found.");
}
```

- [ ] **Step 4: Auth DTOs**

```csharp
// src/EnterpriseApp.Application/Features/Identity/Auth/DTOs/AuthDtos.cs
namespace EnterpriseApp.Application.Features.Identity.Auth.DTOs;

public sealed record AuthResponse(
    string         AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string         RefreshToken);

public sealed record CurrentUserDto(
    Guid              Id,
    string            Email,
    string            FullName,
    Guid?             TenantId,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions);
```

- [ ] **Step 5: User DTOs**

```csharp
// src/EnterpriseApp.Application/Features/Identity/Users/DTOs/UserDtos.cs
namespace EnterpriseApp.Application.Features.Identity.Users.DTOs;

public sealed record UserSummaryDto(
    Guid           Id,
    string         Email,
    string         FullName,
    bool           IsActive,
    bool           EmailConfirmed,
    DateTimeOffset CreatedAt);

public sealed record UserDetailDto(
    Guid                  Id,
    string                Email,
    string                FirstName,
    string                LastName,
    string                FullName,
    bool                  IsActive,
    bool                  EmailConfirmed,
    Guid?                 TenantId,
    DateTimeOffset?       LastLoginAt,
    DateTimeOffset        CreatedAt,
    IReadOnlyList<string> Roles);

public sealed record PagedUsersDto(
    IReadOnlyList<UserSummaryDto> Data,
    int TotalCount,
    int PageNumber,
    int PageSize);
```

- [ ] **Step 6: Build Application layer**

```bash
dotnet build src/EnterpriseApp.Application/EnterpriseApp.Application.csproj 2>&1 | tail -5
```

- [ ] **Step 7: Commit**

```bash
git add src/EnterpriseApp.Application/Common/Interfaces/IPasswordHasher.cs \
        src/EnterpriseApp.Application/Common/Interfaces/ITokenService.cs \
        src/EnterpriseApp.Application/Common/Errors/AuthErrors.cs \
        src/EnterpriseApp.Application/Features/Identity/
git commit -m "feat(application): add IPasswordHasher, ITokenService, AuthErrors, auth DTOs"
```

---

## Task 5: Application — LoginCommand (TDD)

**Files:**
- Create: `src/EnterpriseApp.Application/Features/Identity/Auth/Commands/Login/LoginCommand.cs`
- Create: `src/EnterpriseApp.Application/Features/Identity/Auth/Commands/Login/LoginCommandValidator.cs`
- Create: `src/EnterpriseApp.Application/Features/Identity/Auth/Commands/Login/LoginCommandHandler.cs`
- Test: `tests/EnterpriseApp.Application.Tests/Features/Identity/LoginCommandHandlerTests.cs`

**Consumes:** `IUnitOfWork.Users`, `IPasswordHasher`, `ITokenService`, `IPermissionService`, `IOptions<AuthOptions>` (AuthOptions defined in Task 9)

**Produces:** `LoginCommandHandler` returning `Result<AuthResponse>`

- [ ] **Step 1: Write failing tests**

```csharp
// tests/EnterpriseApp.Application.Tests/Features/Identity/LoginCommandHandlerTests.cs
using EnterpriseApp.Application.Common.Errors;
using EnterpriseApp.Application.Common.Interfaces;
using EnterpriseApp.Application.Features.Identity.Auth.Commands.Login;
using EnterpriseApp.Application.Features.Identity.Auth.DTOs;
using EnterpriseApp.Application.Common.Authorization;
using EnterpriseApp.Domain.Entities.Identity;
using EnterpriseApp.Domain.Interfaces.Repositories;
using EnterpriseApp.Domain.ValueObjects;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace EnterpriseApp.Application.Tests.Features.Identity;

public sealed class LoginCommandHandlerTests
{
    private readonly Mock<IUnitOfWork>         _uow       = new();
    private readonly Mock<IPasswordHasher>     _hasher    = new();
    private readonly Mock<ITokenService>       _tokens    = new();
    private readonly Mock<IPermissionService>  _perms     = new();

    private readonly AuthOptions _opts = new()
    {
        AccessTokenMinutes      = 15,
        RefreshTokenDays        = 7,
        MaxFailedAccessAttempts = 5,
        LockoutMinutes          = 15,
    };

    private LoginCommandHandler BuildHandler() =>
        new(_uow.Object, _hasher.Object, _tokens.Object, _perms.Object,
            Options.Create(_opts));

    private User MakeActiveUser()
    {
        var user = User.Create(
            Email.From("user@test.com"), "hashed_pw", "John", "Doe", null, "seed");
        user.ConfirmEmail();
        return user;
    }

    [Fact]
    public async Task Handle_ValidCredentials_ReturnsAuthResponse()
    {
        var user = MakeActiveUser();
        _uow.Setup(u => u.Users.GetByNormalizedEmailAsync("user@test.com", default))
            .ReturnsAsync(user);
        _hasher.Setup(h => h.Verify("hashed_pw", "correct_pw"))
            .Returns(PasswordVerificationResult.Success);
        _perms.Setup(p => p.GetPermissionsForUserAsync(user.Id.ToString(), null, default))
            .ReturnsAsync((IReadOnlySet<string>)new HashSet<string> { "user.view" });
        _perms.Setup(p => p.ComputeSnapshotVersion(It.IsAny<IEnumerable<string>>()))
            .Returns("psv1");
        _uow.Setup(u => u.Roles.GetRoleNamesForUserAsync(user.Id.ToString(), null, default))
            .ReturnsAsync((IReadOnlyList<string>)new List<string> { "user" });
        _tokens.Setup(t => t.GenerateAccessToken(It.IsAny<TokenSubject>()))
            .Returns(new AccessTokenResult("access_tok", DateTimeOffset.UtcNow.AddMinutes(15)));
        _tokens.Setup(t => t.GenerateRefreshToken())
            .Returns(new RefreshTokenResult("plain_tok", "hash_tok", DateTimeOffset.UtcNow.AddDays(7)));
        _uow.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        var result = await BuildHandler().Handle(new LoginCommand("user@test.com", "correct_pw", null), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().Be("access_tok");
        result.Value.RefreshToken.Should().Be("plain_tok");
    }

    [Fact]
    public async Task Handle_WrongPassword_ReturnsInvalidCredentials()
    {
        var user = MakeActiveUser();
        _uow.Setup(u => u.Users.GetByNormalizedEmailAsync("user@test.com", default))
            .ReturnsAsync(user);
        _hasher.Setup(h => h.Verify("hashed_pw", "wrong"))
            .Returns(PasswordVerificationResult.Failed);
        _uow.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        var result = await BuildHandler().Handle(new LoginCommand("user@test.com", "wrong", null), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AuthErrors.InvalidCredentials);
    }

    [Fact]
    public async Task Handle_EmailNotFound_ReturnsSameInvalidCredentials()
    {
        _uow.Setup(u => u.Users.GetByNormalizedEmailAsync(It.IsAny<string>(), default))
            .ReturnsAsync((User?)null);

        var result = await BuildHandler().Handle(new LoginCommand("no@one.com", "pw", null), default);

        result.Error.Should().Be(AuthErrors.InvalidCredentials);
    }

    [Fact]
    public async Task Handle_LockedAccount_ReturnsAccountLocked()
    {
        var user = MakeActiveUser();
        for (int i = 0; i < 5; i++) user.RecordFailedLogin(5, TimeSpan.FromMinutes(15));

        _uow.Setup(u => u.Users.GetByNormalizedEmailAsync("user@test.com", default))
            .ReturnsAsync(user);
        _hasher.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(PasswordVerificationResult.Success);
        _uow.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        var result = await BuildHandler().Handle(new LoginCommand("user@test.com", "correct_pw", null), default);

        result.Error.Should().Be(AuthErrors.AccountLocked);
    }

    [Fact]
    public async Task Handle_InactiveAccount_ReturnsAccountInactive()
    {
        var user = MakeActiveUser();
        user.Deactivate("admin");
        user.ClearDomainEvents();

        _uow.Setup(u => u.Users.GetByNormalizedEmailAsync("user@test.com", default))
            .ReturnsAsync(user);
        _hasher.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(PasswordVerificationResult.Success);
        _uow.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        var result = await BuildHandler().Handle(new LoginCommand("user@test.com", "pw", null), default);

        result.Error.Should().Be(AuthErrors.AccountInactive);
    }
}
```

**Note:** `IRoleRepository.GetRoleNamesForUserAsync` is needed — add it to `IRoleRepository` (see step 2c).

- [ ] **Step 2a: Add GetRoleNamesForUserAsync to IRoleRepository**

Open `src/EnterpriseApp.Domain/Interfaces/Repositories/Authorization/IRoleRepository.cs` and add:
```csharp
Task<IReadOnlyList<string>> GetRoleNamesForUserAsync(string userId, Guid? tenantId, CancellationToken ct = default);
```

Also add a stub implementation in `RoleRepository`:
```csharp
public async Task<IReadOnlyList<string>> GetRoleNamesForUserAsync(string userId, Guid? tenantId, CancellationToken ct = default)
{
    return await _context.UserRoles
        .AsNoTracking()
        .Where(ur => ur.UserId == userId && ur.TenantId == tenantId)
        .Join(_context.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Name)
        .ToListAsync(ct);
}
```

- [ ] **Step 2b: AuthOptions class** (needed by handler; full impl in Task 9, just add skeleton now)

```csharp
// src/EnterpriseApp.Infrastructure/Identity/AuthOptions.cs
namespace EnterpriseApp.Infrastructure.Identity;

public sealed class AuthOptions
{
    public const string SectionName = "Auth";
    public int    AccessTokenMinutes      { get; init; } = 15;
    public int    RefreshTokenDays        { get; init; } = 7;
    public int    MaxFailedAccessAttempts { get; init; } = 5;
    public int    LockoutMinutes          { get; init; } = 15;
    public StepUpMfaOptions  StepUpMfa   { get; init; } = new();
    public SeedAdminOptions  SeedAdmin   { get; init; } = new();
}

public sealed class StepUpMfaOptions
{
    public bool Enabled       { get; init; }
    public int  MaxAgeMinutes { get; init; } = 5;
}

public sealed class SeedAdminOptions
{
    public bool   Enabled   { get; init; } = true;
    public string Email     { get; init; } = "admin@enterpriseapp.local";
    public string Password  { get; init; } = "Admin123!";
    public string FirstName { get; init; } = "System";
    public string LastName  { get; init; } = "Administrator";
}
```

- [ ] **Step 2c: LoginCommand + Validator**

```csharp
// src/EnterpriseApp.Application/Features/Identity/Auth/Commands/Login/LoginCommand.cs
using EnterpriseApp.Application.Features.Identity.Auth.DTOs;
using EnterpriseApp.Domain.Common;
using MediatR;

namespace EnterpriseApp.Application.Features.Identity.Auth.Commands.Login;

public sealed record LoginCommand(
    string  Email,
    string  Password,
    string? IpAddress) : IRequest<Result<AuthResponse>>;
```

```csharp
// src/EnterpriseApp.Application/Features/Identity/Auth/Commands/Login/LoginCommandValidator.cs
using FluentValidation;

namespace EnterpriseApp.Application.Features.Identity.Auth.Commands.Login;

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(254);

        RuleFor(x => x.Password)
            .NotEmpty()
            .MaximumLength(100);
    }
}
```

- [ ] **Step 2d: LoginCommandHandler**

```csharp
// src/EnterpriseApp.Application/Features/Identity/Auth/Commands/Login/LoginCommandHandler.cs
using EnterpriseApp.Application.Common.Authorization;
using EnterpriseApp.Application.Common.Errors;
using EnterpriseApp.Application.Common.Interfaces;
using EnterpriseApp.Application.Features.Identity.Auth.DTOs;
using EnterpriseApp.Domain.Common;
using EnterpriseApp.Domain.Interfaces.Repositories;
using EnterpriseApp.Infrastructure.Identity;
using MediatR;
using Microsoft.Extensions.Options;

namespace EnterpriseApp.Application.Features.Identity.Auth.Commands.Login;

internal sealed class LoginCommandHandler(
    IUnitOfWork         uow,
    IPasswordHasher     hasher,
    ITokenService       tokens,
    IPermissionService  permissions,
    IOptions<AuthOptions> opts)
    : IRequestHandler<LoginCommand, Result<AuthResponse>>
{
    public async Task<Result<AuthResponse>> Handle(LoginCommand cmd, CancellationToken ct)
    {
        var o = opts.Value;
        var normalized = cmd.Email.Trim().ToLowerInvariant();

        var user = await uow.Users.GetByNormalizedEmailAsync(normalized, ct);

        // Steps 2-3: same error for not-found and bad password (anti-enumeration)
        if (user is null)
            return Result.Failure<AuthResponse>(AuthErrors.InvalidCredentials);

        var verification = hasher.Verify(user.PasswordHash, cmd.Password);
        if (verification == PasswordVerificationResult.Failed)
        {
            user.RecordFailedLogin(o.MaxFailedAccessAttempts, TimeSpan.FromMinutes(o.LockoutMinutes));
            await uow.SaveChangesAsync(ct);
            return Result.Failure<AuthResponse>(AuthErrors.InvalidCredentials);
        }

        // Rehash if needed
        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.ChangePassword(hasher.Hash(cmd.Password), "system");
        }

        // Step 4-5: check lockout and active AFTER verifying password
        if (user.IsLockedOut(DateTimeOffset.UtcNow))
            return Result.Failure<AuthResponse>(AuthErrors.AccountLocked);

        if (!user.IsActive)
            return Result.Failure<AuthResponse>(AuthErrors.AccountInactive);

        user.RecordSuccessfulLogin();

        var userId    = user.Id.ToString();
        var tenantId  = user.TenantId;
        var perms     = await permissions.GetPermissionsForUserAsync(userId, tenantId, ct);
        var roles     = await uow.Roles.GetRoleNamesForUserAsync(userId, tenantId, ct);
        var psv       = permissions.ComputeSnapshotVersion(perms);
        var authTime  = DateTimeOffset.UtcNow;

        var subject = new TokenSubject(
            UserId:          userId,
            Email:           user.Email.Value,
            FullName:        user.FullName,
            TenantId:        tenantId,
            RoleNames:       roles,
            PermissionCodes: perms,
            SnapshotVersion: psv,
            AuthTime:        authTime);

        var access  = tokens.GenerateAccessToken(subject);
        var refresh = tokens.GenerateRefreshToken();

        user.IssueRefreshToken(refresh.Hash, refresh.ExpiresAt, cmd.IpAddress);
        await uow.SaveChangesAsync(ct);

        return Result.Success(new AuthResponse(access.Token, access.ExpiresAt, refresh.Plain));
    }
}
```

**Note:** This handler references `EnterpriseApp.Infrastructure.Identity.AuthOptions`. This is an Application→Infrastructure reference, which violates Clean Architecture. Move `AuthOptions` to Application layer to fix:
- Move `AuthOptions`, `StepUpMfaOptions`, `SeedAdminOptions` to `src/EnterpriseApp.Application/Common/Auth/AuthOptions.cs`
- Keep Infrastructure-specific config in Infrastructure only if needed
- Update all using statements accordingly

Alternatively, define a minimal `AuthSettings` interface/record in Application and bind from config there. The simplest fix: put `AuthOptions` in Application.

Corrected placement for `AuthOptions.cs`:
```csharp
// src/EnterpriseApp.Application/Common/Auth/AuthOptions.cs
namespace EnterpriseApp.Application.Common.Auth;

public sealed class AuthOptions
{
    public const string SectionName = "Auth";
    public int    AccessTokenMinutes      { get; init; } = 15;
    public int    RefreshTokenDays        { get; init; } = 7;
    public int    MaxFailedAccessAttempts { get; init; } = 5;
    public int    LockoutMinutes          { get; init; } = 15;
    public StepUpMfaOptions StepUpMfa    { get; init; } = new();
    public SeedAdminOptions SeedAdmin    { get; init; } = new();
}

public sealed class StepUpMfaOptions
{
    public bool Enabled       { get; init; }
    public int  MaxAgeMinutes { get; init; } = 5;
}

public sealed class SeedAdminOptions
{
    public bool   Enabled   { get; init; } = true;
    public string Email     { get; init; } = "admin@enterpriseapp.local";
    public string Password  { get; init; } = "Admin123!";
    public string FirstName { get; init; } = "System";
    public string LastName  { get; init; } = "Administrator";
}
```

Update handler using statement: `using EnterpriseApp.Application.Common.Auth;`

- [ ] **Step 3: Run LoginCommandHandlerTests**

```bash
dotnet test tests/EnterpriseApp.Application.Tests \
  --filter "FullyQualifiedName~LoginCommandHandlerTests" -v minimal
```

- [ ] **Step 4: Commit**

```bash
git add src/EnterpriseApp.Application/ \
        tests/EnterpriseApp.Application.Tests/Features/Identity/LoginCommandHandlerTests.cs
git commit -m "feat(application): add LoginCommand with TDD — anti-enumeration + lockout flow"
```

---

## Task 6: Application — RefreshToken + Logout commands (TDD)

**Files:**
- Create: `src/.../Auth/Commands/RefreshToken/RefreshTokenCommand.cs`
- Create: `src/.../Auth/Commands/RefreshToken/RefreshTokenCommandHandler.cs`
- Create: `src/.../Auth/Commands/Logout/LogoutCommand.cs`
- Create: `src/.../Auth/Commands/Logout/LogoutCommandHandler.cs`
- Test: `tests/.../Features/Identity/RefreshTokenCommandHandlerTests.cs`

- [ ] **Step 1: Write failing RefreshToken tests**

```csharp
// tests/EnterpriseApp.Application.Tests/Features/Identity/RefreshTokenCommandHandlerTests.cs
using EnterpriseApp.Application.Common.Auth;
using EnterpriseApp.Application.Common.Errors;
using EnterpriseApp.Application.Common.Interfaces;
using EnterpriseApp.Application.Features.Identity.Auth.Commands.RefreshToken;
using EnterpriseApp.Application.Common.Authorization;
using EnterpriseApp.Domain.Entities.Identity;
using EnterpriseApp.Domain.Interfaces.Repositories;
using EnterpriseApp.Domain.ValueObjects;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace EnterpriseApp.Application.Tests.Features.Identity;

public sealed class RefreshTokenCommandHandlerTests
{
    private readonly Mock<IUnitOfWork>        _uow    = new();
    private readonly Mock<ITokenService>      _tokens = new();
    private readonly Mock<IPermissionService> _perms  = new();

    private readonly AuthOptions _opts = new()
    {
        AccessTokenMinutes = 15,
        RefreshTokenDays   = 7,
    };

    private RefreshTokenCommandHandler BuildHandler() =>
        new(_uow.Object, _tokens.Object, _perms.Object, Options.Create(_opts));

    private User MakeUserWithToken(string tokenHash = "validhash")
    {
        var user = User.Create(Email.From("u@t.com"), "pw", "U", "T", null, "seed");
        user.ConfirmEmail();
        user.IssueRefreshToken(tokenHash, DateTimeOffset.UtcNow.AddDays(7), "ip");
        user.ClearDomainEvents();
        return user;
    }

    [Fact]
    public async Task Handle_ValidToken_ReturnsNewAuthResponse()
    {
        var user = MakeUserWithToken("validhash");
        _uow.Setup(u => u.Users.GetByRefreshTokenHashAsync("validhash", default))
            .ReturnsAsync(user);
        _perms.Setup(p => p.GetPermissionsForUserAsync(It.IsAny<string>(), null, default))
            .ReturnsAsync((IReadOnlySet<string>)new HashSet<string>());
        _perms.Setup(p => p.ComputeSnapshotVersion(It.IsAny<IEnumerable<string>>())).Returns("v");
        _uow.Setup(u => u.Roles.GetRoleNamesForUserAsync(It.IsAny<string>(), null, default))
            .ReturnsAsync((IReadOnlyList<string>)new List<string>());
        _tokens.Setup(t => t.GenerateAccessToken(It.IsAny<TokenSubject>()))
            .Returns(new AccessTokenResult("new_access", DateTimeOffset.UtcNow.AddMinutes(15)));
        _tokens.Setup(t => t.GenerateRefreshToken())
            .Returns(new RefreshTokenResult("new_plain", "new_hash", DateTimeOffset.UtcNow.AddDays(7)));
        _uow.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        var result = await BuildHandler().Handle(new RefreshTokenCommand("validhash_plain", "ip"), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().Be("new_access");
    }

    [Fact]
    public async Task Handle_InvalidToken_ReturnsInvalidRefreshToken()
    {
        _uow.Setup(u => u.Users.GetByRefreshTokenHashAsync(It.IsAny<string>(), default))
            .ReturnsAsync((User?)null);

        var result = await BuildHandler().Handle(new RefreshTokenCommand("bad_token", null), default);

        result.Error.Should().Be(AuthErrors.InvalidRefreshToken);
    }

    [Fact]
    public async Task Handle_RevokedToken_RevokesAllAndReturnsReusedError()
    {
        var user = MakeUserWithToken("validhash");
        // Revoke the token to simulate reuse
        var token = user.GetActiveRefreshToken("validhash");
        // Force rotate then try to use old hash — reuse detection path
        user.RotateRefreshToken("validhash", "newhash", DateTimeOffset.UtcNow.AddDays(7), "ip");
        user.ClearDomainEvents();

        _uow.Setup(u => u.Users.GetByRefreshTokenHashAsync(It.IsAny<string>(), default))
            .ReturnsAsync(user);
        _uow.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        var result = await BuildHandler().Handle(new RefreshTokenCommand("validhash_plain", "ip"), default);

        result.Error.Should().Be(AuthErrors.RefreshTokenReused);
    }
}
```

**Note:** `GetByRefreshTokenHashAsync` receives the SHA-256 hash of the plain token. In the handler, compute SHA-256 from the plain token before querying.

- [ ] **Step 2: RefreshTokenCommand**

```csharp
// src/.../Auth/Commands/RefreshToken/RefreshTokenCommand.cs
using EnterpriseApp.Application.Features.Identity.Auth.DTOs;
using EnterpriseApp.Domain.Common;
using MediatR;

namespace EnterpriseApp.Application.Features.Identity.Auth.Commands.RefreshToken;

public sealed record RefreshTokenCommand(
    string  PlainToken,
    string? IpAddress) : IRequest<Result<AuthResponse>>;
```

- [ ] **Step 3: RefreshTokenCommandHandler**

```csharp
// src/.../Auth/Commands/RefreshToken/RefreshTokenCommandHandler.cs
using System.Security.Cryptography;
using System.Text;
using EnterpriseApp.Application.Common.Auth;
using EnterpriseApp.Application.Common.Authorization;
using EnterpriseApp.Application.Common.Errors;
using EnterpriseApp.Application.Common.Interfaces;
using EnterpriseApp.Application.Features.Identity.Auth.DTOs;
using EnterpriseApp.Domain.Common;
using EnterpriseApp.Domain.Interfaces.Repositories;
using MediatR;
using Microsoft.Extensions.Options;

namespace EnterpriseApp.Application.Features.Identity.Auth.Commands.RefreshToken;

internal sealed class RefreshTokenCommandHandler(
    IUnitOfWork        uow,
    ITokenService      tokens,
    IPermissionService permissions,
    IOptions<AuthOptions> opts)
    : IRequestHandler<RefreshTokenCommand, Result<AuthResponse>>
{
    public async Task<Result<AuthResponse>> Handle(RefreshTokenCommand cmd, CancellationToken ct)
    {
        var hash = ComputeHash(cmd.PlainToken);
        var user = await uow.Users.GetByRefreshTokenHashAsync(hash, ct);
        if (user is null)
            return Result.Failure<AuthResponse>(AuthErrors.InvalidRefreshToken);

        var o       = opts.Value;
        var refresh = tokens.GenerateRefreshToken();

        var rotated = user.RotateRefreshToken(hash, refresh.Hash, refresh.ExpiresAt, cmd.IpAddress);
        if (!rotated)
        {
            await uow.SaveChangesAsync(ct);
            return Result.Failure<AuthResponse>(AuthErrors.RefreshTokenReused);
        }

        var userId   = user.Id.ToString();
        var tenantId = user.TenantId;
        var perms    = await permissions.GetPermissionsForUserAsync(userId, tenantId, ct);
        var roles    = await uow.Roles.GetRoleNamesForUserAsync(userId, tenantId, ct);
        var psv      = permissions.ComputeSnapshotVersion(perms);

        var subject = new TokenSubject(
            UserId:          userId,
            Email:           user.Email.Value,
            FullName:        user.FullName,
            TenantId:        tenantId,
            RoleNames:       roles,
            PermissionCodes: perms,
            SnapshotVersion: psv,
            AuthTime:        DateTimeOffset.UtcNow);

        var access = tokens.GenerateAccessToken(subject);
        await uow.SaveChangesAsync(ct);

        return Result.Success(new AuthResponse(access.Token, access.ExpiresAt, refresh.Plain));
    }

    private static string ComputeHash(string plain)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(plain));
        return Convert.ToBase64String(bytes);
    }
}
```

- [ ] **Step 4: LogoutCommand + Handler**

```csharp
// src/.../Auth/Commands/Logout/LogoutCommand.cs
using EnterpriseApp.Domain.Common;
using MediatR;

namespace EnterpriseApp.Application.Features.Identity.Auth.Commands.Logout;

public sealed record LogoutCommand(
    string  PlainToken,
    string? IpAddress) : IRequest<Result>;
```

```csharp
// src/.../Auth/Commands/Logout/LogoutCommandHandler.cs
using System.Security.Cryptography;
using System.Text;
using EnterpriseApp.Domain.Common;
using EnterpriseApp.Domain.Interfaces.Repositories;
using MediatR;

namespace EnterpriseApp.Application.Features.Identity.Auth.Commands.Logout;

internal sealed class LogoutCommandHandler(IUnitOfWork uow)
    : IRequestHandler<LogoutCommand, Result>
{
    public async Task<Result> Handle(LogoutCommand cmd, CancellationToken ct)
    {
        var hash = ComputeHash(cmd.PlainToken);
        var user = await uow.Users.GetByRefreshTokenHashAsync(hash, ct);
        if (user is null) return Result.Success(); // idempotent

        var token = user.GetActiveRefreshToken(hash);
        token?.Revoke("logout", cmd.IpAddress);
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    private static string ComputeHash(string plain)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(plain));
        return Convert.ToBase64String(bytes);
    }
}
```

- [ ] **Step 5: Run RefreshToken tests**

```bash
dotnet test tests/EnterpriseApp.Application.Tests \
  --filter "FullyQualifiedName~RefreshTokenCommandHandlerTests" -v minimal
```

- [ ] **Step 6: Commit**

```bash
git add src/EnterpriseApp.Application/Features/Identity/Auth/Commands/RefreshToken/ \
        src/EnterpriseApp.Application/Features/Identity/Auth/Commands/Logout/ \
        tests/EnterpriseApp.Application.Tests/Features/Identity/RefreshTokenCommandHandlerTests.cs
git commit -m "feat(application): add RefreshToken rotation with reuse detection and Logout"
```

---

## Task 7: Application — ChangePassword + GetCurrentUser

- [ ] **Step 1: ChangePasswordCommand**

```csharp
// src/.../Auth/Commands/ChangePassword/ChangePasswordCommand.cs
using EnterpriseApp.Domain.Common;
using MediatR;

namespace EnterpriseApp.Application.Features.Identity.Auth.Commands.ChangePassword;

public sealed record ChangePasswordCommand(
    string CurrentPassword,
    string NewPassword) : IRequest<Result>;
```

```csharp
// src/.../Auth/Commands/ChangePassword/ChangePasswordCommandValidator.cs
using FluentValidation;

namespace EnterpriseApp.Application.Features.Identity.Auth.Commands.ChangePassword;

public sealed class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty();
        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .MinimumLength(8)
            .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches("[a-z]").WithMessage("Password must contain at least one lowercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain at least one digit.");
    }
}
```

```csharp
// src/.../Auth/Commands/ChangePassword/ChangePasswordCommandHandler.cs
using EnterpriseApp.Application.Common.Authorization;
using EnterpriseApp.Application.Common.Errors;
using EnterpriseApp.Application.Common.Interfaces;
using EnterpriseApp.Domain.Common;
using EnterpriseApp.Domain.Entities.Identity;
using EnterpriseApp.Domain.Interfaces.Repositories;
using MediatR;

namespace EnterpriseApp.Application.Features.Identity.Auth.Commands.ChangePassword;

internal sealed class ChangePasswordCommandHandler(
    IUnitOfWork    uow,
    IPasswordHasher hasher,
    ICurrentUser   currentUser)
    : IRequestHandler<ChangePasswordCommand, Result>
{
    public async Task<Result> Handle(ChangePasswordCommand cmd, CancellationToken ct)
    {
        if (!Guid.TryParse(currentUser.UserId, out var guid))
            return Result.Failure(AuthErrors.UserNotFound);

        var user = await uow.Users.GetByIdAsync(UserId.From(guid), ct);
        if (user is null) return Result.Failure(AuthErrors.UserNotFound);

        if (hasher.Verify(user.PasswordHash, cmd.CurrentPassword) == PasswordVerificationResult.Failed)
            return Result.Failure(AuthErrors.InvalidCredentials);

        user.ChangePassword(hasher.Hash(cmd.NewPassword), currentUser.UserId);
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
```

- [ ] **Step 2: GetCurrentUserQuery**

```csharp
// src/.../Auth/Queries/GetCurrentUser/GetCurrentUserQuery.cs
using EnterpriseApp.Application.Features.Identity.Auth.DTOs;
using EnterpriseApp.Domain.Common;
using MediatR;

namespace EnterpriseApp.Application.Features.Identity.Auth.Queries.GetCurrentUser;

public sealed record GetCurrentUserQuery : IRequest<Result<CurrentUserDto>>;
```

```csharp
// src/.../Auth/Queries/GetCurrentUser/GetCurrentUserQueryHandler.cs
using EnterpriseApp.Application.Common.Authorization;
using EnterpriseApp.Application.Common.Errors;
using EnterpriseApp.Application.Features.Identity.Auth.DTOs;
using EnterpriseApp.Domain.Common;
using EnterpriseApp.Domain.Entities.Identity;
using EnterpriseApp.Domain.Interfaces.Repositories;
using MediatR;

namespace EnterpriseApp.Application.Features.Identity.Auth.Queries.GetCurrentUser;

internal sealed class GetCurrentUserQueryHandler(
    IUnitOfWork        uow,
    ICurrentUser       currentUser,
    IPermissionService permissions)
    : IRequestHandler<GetCurrentUserQuery, Result<CurrentUserDto>>
{
    public async Task<Result<CurrentUserDto>> Handle(GetCurrentUserQuery _, CancellationToken ct)
    {
        if (!Guid.TryParse(currentUser.UserId, out var guid))
            return Result.Failure<CurrentUserDto>(AuthErrors.UserNotFound);

        var user = await uow.Users.GetByIdAsync(UserId.From(guid), ct);
        if (user is null) return Result.Failure<CurrentUserDto>(AuthErrors.UserNotFound);

        var userId   = user.Id.ToString();
        var tenantId = user.TenantId;
        var roles    = await uow.Roles.GetRoleNamesForUserAsync(userId, tenantId, ct);
        var perms    = await permissions.GetPermissionsForUserAsync(userId, tenantId, ct);

        return Result.Success(new CurrentUserDto(
            Id:          user.Id.Value,
            Email:       user.Email.Value,
            FullName:    user.FullName,
            TenantId:    tenantId,
            Roles:       roles.ToList(),
            Permissions: perms.ToList()));
    }
}
```

- [ ] **Step 3: Build**

```bash
dotnet build src/EnterpriseApp.Application/ 2>&1 | tail -5
```

- [ ] **Step 4: Commit**

```bash
git add src/EnterpriseApp.Application/Features/Identity/Auth/Commands/ChangePassword/ \
        src/EnterpriseApp.Application/Features/Identity/Auth/Queries/
git commit -m "feat(application): add ChangePassword and GetCurrentUser use cases"
```

---

## Task 8: Application — User CRUD use cases

- [ ] **Step 1: CreateUserCommand**

```csharp
// src/.../Users/Commands/CreateUser/CreateUserCommand.cs
using EnterpriseApp.Domain.Common;
using MediatR;

namespace EnterpriseApp.Application.Features.Identity.Users.Commands.CreateUser;

public sealed record CreateUserCommand(
    string  Email,
    string  Password,
    string  FirstName,
    string  LastName,
    Guid?   TenantId) : IRequest<Result<Guid>>;
```

```csharp
// src/.../Users/Commands/CreateUser/CreateUserCommandValidator.cs
using FluentValidation;

namespace EnterpriseApp.Application.Features.Identity.Users.Commands.CreateUser;

public sealed class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(254);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8)
            .Matches("[A-Z]").WithMessage("Must contain uppercase.")
            .Matches("[a-z]").WithMessage("Must contain lowercase.")
            .Matches("[0-9]").WithMessage("Must contain digit.");
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
    }
}
```

```csharp
// src/.../Users/Commands/CreateUser/CreateUserCommandHandler.cs
using EnterpriseApp.Application.Common.Authorization;
using EnterpriseApp.Application.Common.Errors;
using EnterpriseApp.Application.Common.Interfaces;
using EnterpriseApp.Domain.Common;
using EnterpriseApp.Domain.Entities.Identity;
using EnterpriseApp.Domain.Interfaces.Repositories;
using EnterpriseApp.Domain.ValueObjects;
using MediatR;

namespace EnterpriseApp.Application.Features.Identity.Users.Commands.CreateUser;

internal sealed class CreateUserCommandHandler(
    IUnitOfWork     uow,
    IPasswordHasher hasher,
    ICurrentUser    currentUser)
    : IRequestHandler<CreateUserCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateUserCommand cmd, CancellationToken ct)
    {
        var normalized = cmd.Email.Trim().ToLowerInvariant();
        if (await uow.Users.ExistsByEmailAsync(normalized, ct))
            return Result.Failure<Guid>(AuthErrors.EmailAlreadyExists);

        var email = Email.From(cmd.Email);
        var user  = User.Create(
            email:        email,
            passwordHash: hasher.Hash(cmd.Password),
            firstName:    cmd.FirstName,
            lastName:     cmd.LastName,
            tenantId:     cmd.TenantId,
            createdBy:    currentUser.UserId);

        await uow.Users.AddAsync(user, ct);
        await uow.SaveChangesAsync(ct);
        return Result.Success(user.Id.Value);
    }
}
```

- [ ] **Step 2: UpdateUserCommand**

```csharp
// src/.../Users/Commands/UpdateUser/UpdateUserCommand.cs
using EnterpriseApp.Domain.Common;
using MediatR;

namespace EnterpriseApp.Application.Features.Identity.Users.Commands.UpdateUser;

public sealed record UpdateUserCommand(
    Guid   Id,
    string FirstName,
    string LastName) : IRequest<Result>;
```

```csharp
// src/.../Users/Commands/UpdateUser/UpdateUserCommandValidator.cs
using FluentValidation;

namespace EnterpriseApp.Application.Features.Identity.Users.Commands.UpdateUser;

public sealed class UpdateUserCommandValidator : AbstractValidator<UpdateUserCommand>
{
    public UpdateUserCommandValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
    }
}
```

```csharp
// src/.../Users/Commands/UpdateUser/UpdateUserCommandHandler.cs
using EnterpriseApp.Application.Common.Authorization;
using EnterpriseApp.Application.Common.Errors;
using EnterpriseApp.Domain.Common;
using EnterpriseApp.Domain.Entities.Identity;
using EnterpriseApp.Domain.Interfaces.Repositories;
using MediatR;

namespace EnterpriseApp.Application.Features.Identity.Users.Commands.UpdateUser;

internal sealed class UpdateUserCommandHandler(IUnitOfWork uow, ICurrentUser currentUser)
    : IRequestHandler<UpdateUserCommand, Result>
{
    public async Task<Result> Handle(UpdateUserCommand cmd, CancellationToken ct)
    {
        var user = await uow.Users.GetByIdAsync(UserId.From(cmd.Id), ct);
        if (user is null) return Result.Failure(AuthErrors.UserNotFound);

        user.UpdateProfile(cmd.FirstName, cmd.LastName, currentUser.UserId);
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
```

- [ ] **Step 3: Activate/Deactivate/Delete commands**

```csharp
// ActivateUserCommand.cs
using EnterpriseApp.Domain.Common; using MediatR;
namespace EnterpriseApp.Application.Features.Identity.Users.Commands.ActivateUser;
public sealed record ActivateUserCommand(Guid Id) : IRequest<Result>;

// ActivateUserCommandHandler.cs
using EnterpriseApp.Application.Common.Authorization;
using EnterpriseApp.Application.Common.Errors;
using EnterpriseApp.Domain.Common; using EnterpriseApp.Domain.Entities.Identity;
using EnterpriseApp.Domain.Interfaces.Repositories; using MediatR;
namespace EnterpriseApp.Application.Features.Identity.Users.Commands.ActivateUser;
internal sealed class ActivateUserCommandHandler(IUnitOfWork uow, ICurrentUser cu)
    : IRequestHandler<ActivateUserCommand, Result>
{
    public async Task<Result> Handle(ActivateUserCommand cmd, CancellationToken ct)
    {
        var user = await uow.Users.GetByIdAsync(UserId.From(cmd.Id), ct);
        if (user is null) return Result.Failure(AuthErrors.UserNotFound);
        user.Activate(cu.UserId);
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
```

```csharp
// DeactivateUserCommand.cs
using EnterpriseApp.Domain.Common; using MediatR;
namespace EnterpriseApp.Application.Features.Identity.Users.Commands.DeactivateUser;
public sealed record DeactivateUserCommand(Guid Id) : IRequest<Result>;

// DeactivateUserCommandHandler.cs
using EnterpriseApp.Application.Common.Authorization;
using EnterpriseApp.Application.Common.Errors;
using EnterpriseApp.Domain.Common; using EnterpriseApp.Domain.Entities.Identity;
using EnterpriseApp.Domain.Interfaces.Repositories; using MediatR;
namespace EnterpriseApp.Application.Features.Identity.Users.Commands.DeactivateUser;
internal sealed class DeactivateUserCommandHandler(IUnitOfWork uow, ICurrentUser cu)
    : IRequestHandler<DeactivateUserCommand, Result>
{
    public async Task<Result> Handle(DeactivateUserCommand cmd, CancellationToken ct)
    {
        var user = await uow.Users.GetByIdAsync(UserId.From(cmd.Id), ct);
        if (user is null) return Result.Failure(AuthErrors.UserNotFound);
        user.Deactivate(cu.UserId);
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
```

```csharp
// DeleteUserCommand.cs
using EnterpriseApp.Domain.Common; using MediatR;
namespace EnterpriseApp.Application.Features.Identity.Users.Commands.DeleteUser;
public sealed record DeleteUserCommand(Guid Id) : IRequest<Result>;

// DeleteUserCommandHandler.cs
using EnterpriseApp.Application.Common.Authorization;
using EnterpriseApp.Application.Common.Errors;
using EnterpriseApp.Domain.Common; using EnterpriseApp.Domain.Entities.Identity;
using EnterpriseApp.Domain.Interfaces.Repositories; using MediatR;
namespace EnterpriseApp.Application.Features.Identity.Users.Commands.DeleteUser;
internal sealed class DeleteUserCommandHandler(IUnitOfWork uow, ICurrentUser cu)
    : IRequestHandler<DeleteUserCommand, Result>
{
    public async Task<Result> Handle(DeleteUserCommand cmd, CancellationToken ct)
    {
        var user = await uow.Users.GetByIdAsync(UserId.From(cmd.Id), ct);
        if (user is null) return Result.Failure(AuthErrors.UserNotFound);
        user.Delete(cu.UserId);
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
```

- [ ] **Step 4: GetUsersQuery**

```csharp
// GetUsersQuery.cs
using EnterpriseApp.Application.Features.Identity.Users.DTOs;
using EnterpriseApp.Domain.Common;
using MediatR;

namespace EnterpriseApp.Application.Features.Identity.Users.Queries.GetUsers;

public sealed record GetUsersQuery(int Page = 1, int PageSize = 20) : IRequest<Result<PagedUsersDto>>;
```

```csharp
// GetUsersQueryHandler.cs
using EnterpriseApp.Application.Features.Identity.Users.DTOs;
using EnterpriseApp.Domain.Common;
using EnterpriseApp.Domain.Interfaces.Repositories;
using MediatR;

namespace EnterpriseApp.Application.Features.Identity.Users.Queries.GetUsers;

internal sealed class GetUsersQueryHandler(IUnitOfWork uow)
    : IRequestHandler<GetUsersQuery, Result<PagedUsersDto>>
{
    public async Task<Result<PagedUsersDto>> Handle(GetUsersQuery q, CancellationToken ct)
    {
        var paged = await uow.Users.GetPagedAsync(q.Page, q.PageSize, ct);
        var data  = paged.Items.Select(u => new UserSummaryDto(
            u.Id.Value, u.Email.Value, u.FullName, u.IsActive, u.EmailConfirmed, u.CreatedAt)).ToList();

        return Result.Success(new PagedUsersDto(data, paged.TotalCount, q.Page, q.PageSize));
    }
}
```

- [ ] **Step 5: GetUserByIdQuery**

```csharp
// GetUserByIdQuery.cs
using EnterpriseApp.Application.Features.Identity.Users.DTOs;
using EnterpriseApp.Domain.Common;
using MediatR;
namespace EnterpriseApp.Application.Features.Identity.Users.Queries.GetUserById;
public sealed record GetUserByIdQuery(Guid Id) : IRequest<Result<UserDetailDto>>;
```

```csharp
// GetUserByIdQueryHandler.cs
using EnterpriseApp.Application.Common.Authorization;
using EnterpriseApp.Application.Common.Errors;
using EnterpriseApp.Application.Features.Identity.Users.DTOs;
using EnterpriseApp.Domain.Common;
using EnterpriseApp.Domain.Entities.Identity;
using EnterpriseApp.Domain.Interfaces.Repositories;
using MediatR;

namespace EnterpriseApp.Application.Features.Identity.Users.Queries.GetUserById;

internal sealed class GetUserByIdQueryHandler(IUnitOfWork uow)
    : IRequestHandler<GetUserByIdQuery, Result<UserDetailDto>>
{
    public async Task<Result<UserDetailDto>> Handle(GetUserByIdQuery q, CancellationToken ct)
    {
        var user = await uow.Users.GetByIdAsync(UserId.From(q.Id), ct);
        if (user is null) return Result.Failure<UserDetailDto>(AuthErrors.UserNotFound);

        var roles = await uow.Roles.GetRoleNamesForUserAsync(user.Id.ToString(), user.TenantId, ct);

        return Result.Success(new UserDetailDto(
            Id:             user.Id.Value,
            Email:          user.Email.Value,
            FirstName:      user.FirstName,
            LastName:       user.LastName,
            FullName:       user.FullName,
            IsActive:       user.IsActive,
            EmailConfirmed: user.EmailConfirmed,
            TenantId:       user.TenantId,
            LastLoginAt:    user.LastLoginAt,
            CreatedAt:      user.CreatedAt,
            Roles:          roles.ToList()));
    }
}
```

- [ ] **Step 6: Build Application**

```bash
dotnet build src/EnterpriseApp.Application/ 2>&1 | tail -10
```

- [ ] **Step 7: Commit**

```bash
git add src/EnterpriseApp.Application/Features/Identity/Users/
git commit -m "feat(application): add User CRUD commands and queries"
```

---

## Task 9: Infrastructure — PasswordHasherAdapter + TokenService

**Files:**
- AuthOptions already created in Task 5 Step 2b at `src/EnterpriseApp.Application/Common/Auth/AuthOptions.cs`
- Create: `src/EnterpriseApp.Infrastructure/Identity/PasswordHasherAdapter.cs`
- Create: `src/EnterpriseApp.Infrastructure/Identity/TokenService.cs`

- [ ] **Step 1: PasswordHasherAdapter**

```csharp
// src/EnterpriseApp.Infrastructure/Identity/PasswordHasherAdapter.cs
using EnterpriseApp.Application.Common.Interfaces;
using EnterpriseApp.Domain.Entities.Identity;
using Microsoft.AspNetCore.Identity;

namespace EnterpriseApp.Infrastructure.Identity;

internal sealed class PasswordHasherAdapter : IPasswordHasher
{
    private readonly PasswordHasher<User> _inner = new();
    private static readonly User _dummy = User.Create(
        Domain.ValueObjects.Email.From("dummy@dummy.com"), "x", "D", "U", null, null);

    public string Hash(string password) => _inner.HashPassword(_dummy, password);

    public Application.Common.Interfaces.PasswordVerificationResult Verify(string hash, string password)
    {
        var result = _inner.VerifyHashedPassword(_dummy, hash, password);
        return result switch
        {
            Microsoft.AspNetCore.Identity.PasswordVerificationResult.Success
                => Application.Common.Interfaces.PasswordVerificationResult.Success,
            Microsoft.AspNetCore.Identity.PasswordVerificationResult.SuccessRehashNeeded
                => Application.Common.Interfaces.PasswordVerificationResult.SuccessRehashNeeded,
            _ => Application.Common.Interfaces.PasswordVerificationResult.Failed,
        };
    }
}
```

- [ ] **Step 2: TokenService**

```csharp
// src/EnterpriseApp.Infrastructure/Identity/TokenService.cs
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EnterpriseApp.Application.Common.Auth;
using EnterpriseApp.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace EnterpriseApp.Infrastructure.Identity;

internal sealed class TokenService(IConfiguration config, IOptions<AuthOptions> opts) : ITokenService
{
    public AccessTokenResult GenerateAccessToken(TokenSubject subject)
    {
        var o       = opts.Value;
        var jwtKey  = config["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key not configured.");
        var issuer   = config["Jwt:Issuer"];
        var audience = config["Jwt:Audience"];
        var key      = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var creds    = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires  = DateTimeOffset.UtcNow.AddMinutes(o.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub,   subject.UserId),
            new(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat,   DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new("name",      subject.FullName),
            new("email",     subject.Email),
            new("psv",       subject.SnapshotVersion),
            new("auth_time", subject.AuthTime.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new("amr",       "pwd"),
            new("perms",     JsonSerializer.Serialize(subject.PermissionCodes.ToArray())),
        };

        if (subject.TenantId is { } tid)
            claims.Add(new Claim("tid", tid.ToString()));

        foreach (var role in subject.RoleNames)
            claims.Add(new Claim(ClaimTypes.Role, role));

        var token = new JwtSecurityToken(
            issuer:             issuer,
            audience:           audience,
            claims:             claims,
            notBefore:          DateTime.UtcNow,
            expires:            expires.UtcDateTime,
            signingCredentials: creds);

        return new AccessTokenResult(new JwtSecurityTokenHandler().WriteToken(token), expires);
    }

    public RefreshTokenResult GenerateRefreshToken()
    {
        var o     = opts.Value;
        var bytes = RandomNumberGenerator.GetBytes(32);
        var plain = Convert.ToBase64String(bytes);
        var hash  = ComputeHash(plain);
        var exp   = DateTimeOffset.UtcNow.AddDays(o.RefreshTokenDays);
        return new RefreshTokenResult(plain, hash, exp);
    }

    private static string ComputeHash(string plain)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(plain));
        return Convert.ToBase64String(bytes);
    }
}
```

- [ ] **Step 3: Build Infrastructure**

```bash
dotnet build src/EnterpriseApp.Infrastructure/ 2>&1 | tail -10
```

- [ ] **Step 4: Commit**

```bash
git add src/EnterpriseApp.Infrastructure/Identity/
git commit -m "feat(infrastructure): add PasswordHasherAdapter and TokenService"
```

---

## Task 10: Infrastructure — EF Core configs + update AppDbContext

**Files:**
- Create: `src/EnterpriseApp.Infrastructure/Persistence/Configurations/Identity/UserConfiguration.cs`
- Create: `src/EnterpriseApp.Infrastructure/Persistence/Configurations/Identity/RefreshTokenConfiguration.cs`
- Modify: `src/EnterpriseApp.Infrastructure/Persistence/AppDbContext.cs`
- Modify: `src/EnterpriseApp.Application/Common/Interfaces/IApplicationDbContext.cs`

- [ ] **Step 1: UserConfiguration**

```csharp
// src/.../Configurations/Identity/UserConfiguration.cs
using EnterpriseApp.Domain.Entities.Identity;
using EnterpriseApp.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnterpriseApp.Infrastructure.Persistence.Configurations.Identity;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id)
            .HasConversion(id => id.Value, v => UserId.From(v));

        // Email value object — stored as two columns
        builder.OwnsOne(u => u.Email, email =>
        {
            email.Property(e => e.Value)
                .HasColumnName("Email")
                .HasMaxLength(254)
                .IsRequired();
            email.Property(e => e.Normalized)
                .HasColumnName("NormalizedEmail")
                .HasMaxLength(254)
                .IsRequired();
        });

        builder.HasIndex("NormalizedEmail").IsUnique();

        builder.Property(u => u.PasswordHash).HasMaxLength(512).IsRequired();
        builder.Property(u => u.SecurityStamp).HasMaxLength(64).IsRequired();
        builder.Property(u => u.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(u => u.LastName).HasMaxLength(100).IsRequired();

        builder.HasMany(u => u.RefreshTokens)
            .WithOne()
            .HasForeignKey(rt => rt.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(u => !u.IsDeleted);
    }
}
```

- [ ] **Step 2: RefreshTokenConfiguration**

```csharp
// src/.../Configurations/Identity/RefreshTokenConfiguration.cs
using EnterpriseApp.Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnterpriseApp.Infrastructure.Persistence.Configurations.Identity;

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.HasKey(rt => rt.Id);

        builder.Property(rt => rt.UserId)
            .HasConversion(id => id.Value, v => UserId.From(v))
            .IsRequired();

        builder.Property(rt => rt.TokenHash).HasMaxLength(256).IsRequired();
        builder.HasIndex(rt => rt.TokenHash);

        builder.Property(rt => rt.ReplacedByHash).HasMaxLength(256);
        builder.Property(rt => rt.RevokedReason).HasMaxLength(100);
        builder.Property(rt => rt.CreatedByIp).HasMaxLength(45);
        builder.Property(rt => rt.RevokedByIp).HasMaxLength(45);
    }
}
```

- [ ] **Step 3: Update IApplicationDbContext**

Add to the interface:
```csharp
DbSet<User>         Users         { get; }
DbSet<RefreshToken> RefreshTokens { get; }
```

Full updated file:
```csharp
// src/EnterpriseApp.Application/Common/Interfaces/IApplicationDbContext.cs
using EnterpriseApp.Domain.Entities;
using EnterpriseApp.Domain.Entities.Authorization;
using EnterpriseApp.Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseApp.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<TodoItem>          TodoItems           { get; }
    DbSet<Permission>        Permissions         { get; }
    DbSet<Role>              Roles               { get; }
    DbSet<RolePermission>    RolePermissions     { get; }
    DbSet<UserRole>          UserRoles           { get; }
    DbSet<PermissionAuditLog> PermissionAuditLogs { get; }

    // ── Identity ──────────────────────────────────────────────────────────────
    DbSet<User>         Users         { get; }
    DbSet<RefreshToken> RefreshTokens { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
```

- [ ] **Step 4: Update AppDbContext**

Add the DbSets and query filter. The class body becomes:
```csharp
// src/EnterpriseApp.Infrastructure/Persistence/AppDbContext.cs
// Add inside the class:
public DbSet<User>         Users         => Set<User>();
public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

// Add in OnModelCreating after the TodoItem filter:
modelBuilder.Entity<User>().HasQueryFilter(u => !u.IsDeleted);
```

- [ ] **Step 5: Build**

```bash
dotnet build src/ 2>&1 | tail -10
```

- [ ] **Step 6: Commit**

```bash
git add src/EnterpriseApp.Infrastructure/Persistence/Configurations/Identity/ \
        src/EnterpriseApp.Infrastructure/Persistence/AppDbContext.cs \
        src/EnterpriseApp.Application/Common/Interfaces/IApplicationDbContext.cs
git commit -m "feat(infrastructure): add EF Core configurations for User and RefreshToken"
```

---

## Task 11: Infrastructure — UserRepository + DI + StepUpMfa fix + Seeder

**Files:**
- Create: `src/EnterpriseApp.Infrastructure/Repositories/Identity/UserRepository.cs`
- Modify: `src/EnterpriseApp.Infrastructure/Authorization/StepUpMfaAuthorizationHandler.cs`
- Modify: `src/EnterpriseApp.Infrastructure/DependencyInjection/InfrastructureServiceExtensions.cs`
- Modify: `src/EnterpriseApp.Infrastructure/Persistence/Seeds/DatabaseSeeder.cs`
- Modify: `src/EnterpriseApp.Infrastructure/Repositories/UnitOfWork.cs` (add Users property)

- [ ] **Step 1: UserRepository**

```csharp
// src/EnterpriseApp.Infrastructure/Repositories/Identity/UserRepository.cs
using EnterpriseApp.Domain.Common;
using EnterpriseApp.Domain.Entities.Identity;
using EnterpriseApp.Domain.Interfaces.Repositories.Identity;
using EnterpriseApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseApp.Infrastructure.Repositories.Identity;

internal sealed class UserRepository(AppDbContext context) : IUserRepository
{
    public async Task<User?> GetByIdAsync(UserId id, CancellationToken ct = default) =>
        await context.Users
            .Include(u => u.RefreshTokens)
            .FirstOrDefaultAsync(u => u.Id == id, ct);

    public async Task<User?> GetByNormalizedEmailAsync(string normalizedEmail, CancellationToken ct = default) =>
        await context.Users
            .Include(u => u.RefreshTokens)
            .FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, ct);

    public async Task<User?> GetByRefreshTokenHashAsync(string tokenHash, CancellationToken ct = default) =>
        await context.Users
            .Include(u => u.RefreshTokens)
            .FirstOrDefaultAsync(u => u.RefreshTokens.Any(rt => rt.TokenHash == tokenHash), ct);

    public async Task<bool> ExistsByEmailAsync(string normalizedEmail, CancellationToken ct = default) =>
        await context.Users.AnyAsync(u => u.NormalizedEmail == normalizedEmail, ct);

    public async Task<PagedList<User>> GetPagedAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var query = context.Users.AsNoTracking().OrderBy(u => u.CreatedAt);
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedList<User>(items, total, page, pageSize);
    }

    public async Task AddAsync(User user, CancellationToken ct = default) =>
        await context.Users.AddAsync(user, ct);
}
```

**Note:** `PagedList<T>` — check if it's a class with `(IReadOnlyList<T> Items, int TotalCount)` or similar. Look at `Domain/Common/PagedList.cs` and match the constructor.

- [ ] **Step 2: Fix UnitOfWork — add Users**

Find `src/EnterpriseApp.Infrastructure/Repositories/UnitOfWork.cs` and add:
```csharp
public IUserRepository Users { get; }
```
And inject/initialize it in the constructor.

Full pattern (check existing constructor, then add):
```csharp
// Constructor parameter: IUserRepository users
// Property: public IUserRepository Users { get; } = users;
```

- [ ] **Step 3: Fix StepUpMfaAuthorizationHandler**

Replace the handler to support `StepUpMfaOptions.Enabled = false`:
```csharp
// src/EnterpriseApp.Infrastructure/Authorization/StepUpMfaAuthorizationHandler.cs
using EnterpriseApp.Application.Common.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EnterpriseApp.Infrastructure.Authorization;

public sealed class StepUpMfaAuthorizationHandler(
    IOptions<AuthOptions>    opts,
    ILogger<StepUpMfaAuthorizationHandler> logger)
    : AuthorizationHandler<StepUpMfaRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        StepUpMfaRequirement        requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true) return Task.CompletedTask;

        if (!opts.Value.StepUpMfa.Enabled)
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        var amr = context.User.FindAll("amr").Select(c => c.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!amr.Contains("mfa") && !amr.Contains("otp"))
            return Task.CompletedTask;

        var authTimeClaim = context.User.FindFirstValue("auth_time");
        if (!long.TryParse(authTimeClaim, out var unix))
            return Task.CompletedTask;

        var authTime = DateTimeOffset.FromUnixTimeSeconds(unix);
        if (DateTimeOffset.UtcNow - authTime <= TimeSpan.FromMinutes(opts.Value.StepUpMfa.MaxAgeMinutes))
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}
```

- [ ] **Step 4: Update DatabaseSeeder — add SeedAdminUserAsync**

Add at the end of `SeedAsync`:
```csharp
await SeedAdminUserAsync(ct);
```

Add the method:
```csharp
private async Task SeedAdminUserAsync(CancellationToken ct)
{
    var seedOpts = _authOptions.SeedAdmin;
    if (!seedOpts.Enabled) return;

    var normalized = seedOpts.Email.Trim().ToLowerInvariant();
    var exists = await db.Users.IgnoreQueryFilters()
        .AnyAsync(u => u.NormalizedEmail == normalized, ct);
    if (exists) return;

    var hash = _hasher.Hash(seedOpts.Password);
    var user = User.Create(
        email:        Email.From(seedOpts.Email),
        passwordHash: hash,
        firstName:    seedOpts.FirstName,
        lastName:     seedOpts.LastName,
        tenantId:     null,
        createdBy:    "system-seed");

    user.ConfirmEmail();

    var superAdmin = await db.Roles.IgnoreQueryFilters()
        .FirstOrDefaultAsync(r => r.NormalizedName == "SUPER-ADMIN", ct);

    await db.Users.AddAsync(user, ct);
    await db.SaveChangesAsync(ct);

    if (superAdmin is not null)
    {
        var userRole = UserRole.Create(
            userId:     user.Id.ToString(),
            roleId:     superAdmin.Id,
            tenantId:   null,
            assignedBy: "system-seed");
        await db.UserRoles.AddAsync(userRole, ct);
        await db.SaveChangesAsync(ct);
    }

    if (seedOpts.Password == "Admin123!")
        logger.LogWarning("Admin user seeded with DEFAULT password. Change it before deploying to production!");
    else
        logger.LogInformation("Admin user seeded: {Email}", seedOpts.Email);
}
```

Also update `DatabaseSeeder` constructor to inject `IPasswordHasher` and `IOptions<AuthOptions>`:
```csharp
public sealed class DatabaseSeeder(
    AppDbContext                db,
    IPasswordHasher             hasher,
    IOptions<AuthOptions>       authOptions,
    ILogger<DatabaseSeeder>     logger)
{
    private readonly IPasswordHasher   _hasher      = hasher;
    private readonly AuthOptions       _authOptions = authOptions.Value;
    // ... existing SeedAsync ...
}
```

- [ ] **Step 5: Register everything in InfrastructureServiceExtensions**

Add in the Repositories section:
```csharp
services.AddScoped<IUserRepository, UserRepository>();
```

Add for Identity services:
```csharp
// ── Identity — password hashing + token signing ──────────────────────────
services.Configure<AuthOptions>(configuration.GetSection(AuthOptions.SectionName));
services.AddScoped<IPasswordHasher, PasswordHasherAdapter>();
services.AddScoped<ITokenService, TokenService>();
```

- [ ] **Step 6: Build**

```bash
dotnet build src/ 2>&1 | tail -10
```

- [ ] **Step 7: Commit**

```bash
git add src/EnterpriseApp.Infrastructure/
git commit -m "feat(infrastructure): UserRepository, DI wiring, StepUpMfa fix, admin seeder"
```

---

## Task 12: API — appsettings.json + Rate Limiting + AuthController

- [ ] **Step 1: Update appsettings.json**

Add the `Auth` section after `Jwt` and remove `Jwt:ExpiryMinutes`:
```json
"Jwt": {
  "Key":      "CHANGE-ME-IN-PRODUCTION-use-a-256bit-secret-key-here!",
  "Issuer":   "EnterpriseApp",
  "Audience": "EnterpriseApp.Clients"
},

"Auth": {
  "AccessTokenMinutes":      15,
  "RefreshTokenDays":        7,
  "MaxFailedAccessAttempts": 5,
  "LockoutMinutes":          15,
  "StepUpMfa": {
    "Enabled":       false,
    "MaxAgeMinutes": 5
  },
  "SeedAdmin": {
    "Enabled":   true,
    "Email":     "admin@enterpriseapp.local",
    "Password":  "Admin123!",
    "FirstName": "System",
    "LastName":  "Administrator"
  }
}
```

- [ ] **Step 2: Add Rate Limiting to Program.cs**

Add before `var app = builder.Build();`:
```csharp
// ── Rate Limiting ─────────────────────────────────────────────────────────────
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("login", ctx =>
        System.Threading.RateLimiting.RateLimitPartition.GetSlidingWindowLimiter(
            ctx.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            _ => new System.Threading.RateLimiting.SlidingWindowRateLimiterOptions
            {
                PermitLimit      = 10,
                Window           = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = 6,
            }));
});
```

Add `app.UseRateLimiter();` after `app.UseAuthentication();`.

- [ ] **Step 3: Add AuthController**

```csharp
// src/EnterpriseApp.API/Controllers/AuthController.cs
using EnterpriseApp.Application.Features.Identity.Auth.Commands.ChangePassword;
using EnterpriseApp.Application.Features.Identity.Auth.Commands.Login;
using EnterpriseApp.Application.Features.Identity.Auth.Commands.Logout;
using EnterpriseApp.Application.Features.Identity.Auth.Commands.RefreshToken;
using EnterpriseApp.Application.Features.Identity.Auth.DTOs;
using EnterpriseApp.Application.Features.Identity.Auth.Queries.GetCurrentUser;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace EnterpriseApp.API.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(ISender sender) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status423Locked)]
    public async Task<IActionResult> Login([FromBody] LoginRequest body, CancellationToken ct)
    {
        var ip     = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await sender.Send(new LoginCommand(body.Email, body.Password, ip), ct);
        if (result.IsFailure)
        {
            var status = result.Error.Code switch
            {
                "Auth.AccountLocked"   => 423,
                "Auth.AccountInactive" => 403,
                _                      => 401,
            };
            return Problem(result.Error.Description, statusCode: status, title: result.Error.Code);
        }
        return Ok(result.Value);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest body, CancellationToken ct)
    {
        var ip     = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await sender.Send(new RefreshTokenCommand(body.RefreshToken, ip), ct);
        if (result.IsFailure)
            return Problem(result.Error.Description, statusCode: 401, title: result.Error.Code);
        return Ok(result.Value);
    }

    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest body, CancellationToken ct)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        await sender.Send(new LogoutCommand(body.RefreshToken, ip), ct);
        return NoContent();
    }

    [HttpGet("me")]
    [ProducesResponseType<CurrentUserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var result = await sender.Send(new GetCurrentUserQuery(), ct);
        if (result.IsFailure)
            return Problem(result.Error.Description, statusCode: 404, title: result.Error.Code);
        return Ok(result.Value);
    }

    [HttpPost("change-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest body, CancellationToken ct)
    {
        var result = await sender.Send(new ChangePasswordCommand(body.CurrentPassword, body.NewPassword), ct);
        if (result.IsFailure)
            return Problem(result.Error.Description, statusCode: 400, title: result.Error.Code);
        return NoContent();
    }
}

// ── Request records ────────────────────────────────────────────────────────────
public sealed record LoginRequest(string Email, string Password);
public sealed record RefreshRequest(string RefreshToken);
public sealed record LogoutRequest(string RefreshToken);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
```

- [ ] **Step 4: Build**

```bash
dotnet build src/EnterpriseApp.API/ 2>&1 | tail -10
```

- [ ] **Step 5: Commit**

```bash
git add src/EnterpriseApp.API/
git commit -m "feat(api): add AuthController with login, refresh, logout, me, change-password + rate limiting"
```

---

## Task 13: API — UsersController

- [ ] **Step 1: Create UsersController**

```csharp
// src/EnterpriseApp.API/Controllers/UsersController.cs
using EnterpriseApp.API.Filters;
using EnterpriseApp.Application.Features.Identity.Users.Commands.ActivateUser;
using EnterpriseApp.Application.Features.Identity.Users.Commands.CreateUser;
using EnterpriseApp.Application.Features.Identity.Users.Commands.DeactivateUser;
using EnterpriseApp.Application.Features.Identity.Users.Commands.DeleteUser;
using EnterpriseApp.Application.Features.Identity.Users.Commands.UpdateUser;
using EnterpriseApp.Application.Features.Identity.Users.DTOs;
using EnterpriseApp.Application.Features.Identity.Users.Queries.GetUserById;
using EnterpriseApp.Application.Features.Identity.Users.Queries.GetUsers;
using EnterpriseApp.Domain.Authorization;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace EnterpriseApp.API.Controllers;

[ApiController]
[Route("api/v1/users")]
public sealed class UsersController(ISender sender) : ControllerBase
{
    [HttpGet]
    [RequirePermission(PermissionCodes.User.View)]
    [ProducesResponseType<PagedUsersDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await sender.Send(new GetUsersQuery(page, pageSize), ct);
        return Ok(result.Value);
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(PermissionCodes.User.View)]
    [ProducesResponseType<UserDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new GetUserByIdQuery(id), ct);
        if (result.IsFailure)
            return Problem(result.Error.Description, statusCode: 404, title: result.Error.Code);
        return Ok(result.Value);
    }

    [HttpPost]
    [RequirePermission(PermissionCodes.User.Create)]
    [ProducesResponseType<Guid>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest body, CancellationToken ct)
    {
        var result = await sender.Send(
            new CreateUserCommand(body.Email, body.Password, body.FirstName, body.LastName, body.TenantId), ct);
        if (result.IsFailure)
            return Problem(result.Error.Description, statusCode: 409, title: result.Error.Code);
        return CreatedAtAction(nameof(GetById), new { id = result.Value }, result.Value);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(PermissionCodes.User.Update)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserRequest body, CancellationToken ct)
    {
        var result = await sender.Send(new UpdateUserCommand(id, body.FirstName, body.LastName), ct);
        if (result.IsFailure)
            return Problem(result.Error.Description, statusCode: 404, title: result.Error.Code);
        return NoContent();
    }

    [HttpPatch("{id:guid}/activate")]
    [RequirePermission(PermissionCodes.User.Activate)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new ActivateUserCommand(id), ct);
        if (result.IsFailure)
            return Problem(result.Error.Description, statusCode: 404, title: result.Error.Code);
        return NoContent();
    }

    [HttpPatch("{id:guid}/deactivate")]
    [RequirePermission(PermissionCodes.User.Deactivate)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new DeactivateUserCommand(id), ct);
        if (result.IsFailure)
            return Problem(result.Error.Description, statusCode: 404, title: result.Error.Code);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(PermissionCodes.User.Delete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new DeleteUserCommand(id), ct);
        if (result.IsFailure)
            return Problem(result.Error.Description, statusCode: 404, title: result.Error.Code);
        return NoContent();
    }
}

public sealed record CreateUserRequest(string Email, string Password, string FirstName, string LastName, Guid? TenantId);
public sealed record UpdateUserRequest(string FirstName, string LastName);
```

- [ ] **Step 2: Build full solution**

```bash
dotnet build 2>&1 | tail -15
```

- [ ] **Step 3: Commit**

```bash
git add src/EnterpriseApp.API/Controllers/UsersController.cs
git commit -m "feat(api): add UsersController — CRUD + activate/deactivate endpoints"
```

---

## Task 14: Migration

- [ ] **Step 1: Generate migration**

```bash
dotnet ef migrations add AddIdentity \
  --project src/EnterpriseApp.Infrastructure \
  --startup-project src/EnterpriseApp.API \
  --output-dir Persistence/Migrations
```

- [ ] **Step 2: Review generated migration**

Open the generated `AddIdentity.cs` and verify:
- `Users` table with columns: `Id`, `Email`, `NormalizedEmail` (unique index), `PasswordHash`, `SecurityStamp`, `FirstName`, `LastName`, `IsActive`, `EmailConfirmed`, `TenantId`, `AccessFailedCount`, `LockoutEndsAt`, `LastLoginAt`, audit columns, soft-delete columns
- `RefreshTokens` table with FK to `Users.Id`
- `NormalizedEmail` unique index on `Users`
- `TokenHash` index on `RefreshTokens`

- [ ] **Step 3: Run all existing tests — must stay green**

```bash
dotnet test 2>&1 | tail -15
```

- [ ] **Step 4: Commit**

```bash
git add src/EnterpriseApp.Infrastructure/Persistence/Migrations/
git commit -m "feat(db): add AddIdentity migration — Users and RefreshTokens tables"
```

---

## Task 15: End-to-End Verification

- [ ] **Step 1: Start app**

```bash
dotnet run --project src/EnterpriseApp.API
```

Look for log lines:
- `Authorization seed completed.`
- `Admin user seeded: admin@enterpriseapp.local` OR the warning if default password

- [ ] **Step 2: Login as admin**

```bash
curl -X POST https://localhost:5001/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@enterpriseapp.local","password":"Admin123!"}' \
  -k
```

Expected: `200 OK` with `accessToken` + `refreshToken`

- [ ] **Step 3: Call /auth/me with the token**

```bash
curl https://localhost:5001/api/v1/auth/me \
  -H "Authorization: Bearer <access_token>" -k
```

Expected: `200 OK` with user profile, roles `["super-admin"]`, and permissions including `*`

- [ ] **Step 4: Create a new user**

```bash
curl -X POST https://localhost:5001/api/v1/users \
  -H "Authorization: Bearer <access_token>" \
  -H "Content-Type: application/json" \
  -d '{"email":"newuser@test.com","password":"Test123!","firstName":"New","lastName":"User","tenantId":null}' -k
```

Expected: `201 Created`

- [ ] **Step 5: Test refresh flow**

```bash
curl -X POST https://localhost:5001/api/v1/auth/refresh \
  -H "Content-Type: application/json" \
  -d '{"refreshToken":"<plain_refresh_token>"}' -k
```

Expected: `200 OK` with new tokens

- [ ] **Step 6: Run full test suite**

```bash
dotnet test 2>&1 | tail -20
```

All tests must pass, including architecture tests (Domain has no external deps).

- [ ] **Step 7: Final commit**

```bash
git add .
git commit -m "feat: identity and authentication module — complete implementation"
```

---

## PagedList note

Before implementing `UserRepository.GetPagedAsync`, read `src/EnterpriseApp.Domain/Common/PagedList.cs` to confirm the exact constructor signature. The call must match. The plan assumes:
```csharp
new PagedList<User>(items, total, page, pageSize)
```
Adjust if the actual constructor differs.

## Architecture test note

The `EnterpriseApp.ArchitectureTests` project verifies that Domain has no dependency on Application/Infrastructure. After adding `User`, `Email`, `RefreshToken` — all in Domain with zero NuGet deps — these tests must continue to pass. If they fail, check for any accidental `using Microsoft.Extensions.*` inside the Domain project.
