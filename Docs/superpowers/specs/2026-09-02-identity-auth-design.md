# Diseño — Módulo de Identidad y Autenticación

**Fecha:** 2026-09-02
**Estado:** Aprobado (pendiente de plan de implementación)
**Ámbito:** `EnterpriseApp.Domain`, `.Application`, `.Infrastructure`, `.API` + tests

---

## 1. Problema

El template tiene un modelo RBAC completo y funcional (`Role`, `Permission`,
`RolePermission`, `UserRole`, `PermissionAuditLog`, `PermissionService` con caché
Redis y herencia de roles) y JWT Bearer configurado para **validar** tokens. Pero
no existe ninguna entidad `User` ni nada que **emita** tokens. Consecuencia
práctica: la API no se puede probar — `FallbackPolicy = RequireAuthenticatedUser()`
rechaza todo y no hay forma de obtener un token.

Piezas que ya existen y quedan sin consumidor:

| Pieza existente | Espera algo que no existe |
|---|---|
| `PermissionCodes.User.*` (7 códigos) + su seed a `tenant-admin` | Endpoints de usuarios |
| `CurrentUser` leyendo `sub`, `tid`, `perms`, `perm`, `amr`, `auth_time` | Un emisor de esos claims |
| `StepUpMfaAuthorizationHandler` | Un emisor de `amr` / `auth_time` |
| `PermissionAuthorizationHandler` (claim + fallback a BD) | Un token con `perms` |
| `UserRole.UserId` (string, desacoplado de Identity a propósito) | Un user store |

## 2. Decisiones tomadas

| Decisión | Elección | Razón |
|---|---|---|
| User store | Agregado `User` propio en Domain | `Domain` mantiene cero NuGet (verificado por tests de arquitectura). ASP.NET Identity obligaría a que `AppUser` viviera en Infrastructure y chocaría con el `Role` actual, que **no** hereda de `IdentityRole` y tiene `PermissionsSnapshot` / `IsSystem` / `ParentRoleId` propios. |
| Alcance | Auth completa + CRUD de usuarios | Da consumidor a los `user.*` ya seedeados. |
| Alta de cuentas | Solo admin (`user.create`) | Default seguro para plantilla enterprise/B2B. **No hay `/auth/register` anónimo.** Superficie anónima = `login` + `refresh` únicamente. |
| Permisos en el token | **Enfoque A** — embebidos (`perms` + `psv`) | Es el contrato que `.claude/rules/authorization.md §4` describe y que `CurrentUser` y `PermissionAuthorizationHandler` ya implementan. Autorización sin round-trip. El fallback a `PermissionService` queda como red de seguridad. |
| MFA / TOTP | Fuera de alcance, pero desbloqueado | Ver §7. |
| Multi-tenant | Campo presente, single-tenant por defecto | `User.TenantId` es `Guid?`; el claim `tid` solo se emite si no es null. Coherente con `Role` / `UserRole` / `Permission`. |
| Paquetes NuGet nuevos | **Ninguno** | `PasswordHasher<T>` vive en `Microsoft.Extensions.Identity.Core.dll`, parte del shared framework de ASP.NET Core, ya cubierto por el `FrameworkReference Microsoft.AspNetCore.App` de Infrastructure. `System.IdentityModel.Tokens.Jwt` ya está referenciado. |

## 3. Domain — `EnterpriseApp.Domain/Entities/Identity/`

### 3.1 Regla de pureza (crítica)

`Domain` **no hashea ni verifica contraseñas**. `User.Create` y `User.ChangePassword`
reciben un hash **ya calculado**; la verificación ocurre en Application detrás de
`IPasswordHasher`. Domain solo modela el *estado* de bloqueo y credencial. Esto
mantiene el proyecto con cero dependencias NuGet.

### 3.2 `UserId` y `Email`

- `UserId(Guid Value)` — record con `New()` / `From(Guid)`, mismo patrón que `RoleId`.
- `Email : ValueObject` en `Domain/ValueObjects/Email.cs` — normaliza a minúsculas
  y aplica `Trim`, valida formato, expone `Value` y `Normalized`. Lanza
  `DomainException` si es inválido, igual que `PermissionCode`.

### 3.3 `User : AuditableEntity<UserId>`

Estado:

```
Email  (Email VO)        NormalizedEmail (string, índice único)
PasswordHash             SecurityStamp
FirstName  LastName      FullName (computed)
IsActive                 EmailConfirmed
TenantId?                AccessFailedCount   LockoutEndsAt?
LastLoginAt?             _refreshTokens : List<RefreshToken>
```

Comportamiento:

| Método | Efecto |
|---|---|
| `Create(email, passwordHash, firstName, lastName, tenantId, createdBy)` | Emite `UserCreated`. `IsActive=true`, `EmailConfirmed=false`. |
| `ChangePassword(newHash, changedBy)` | Rota `SecurityStamp`, revoca **todos** los refresh tokens, emite `UserPasswordChanged`. |
| `RecordFailedLogin(maxAttempts, lockoutDuration)` | Incrementa el contador; al llegar al umbral fija `LockoutEndsAt` y emite `UserLockedOut`. Siempre emite `UserLoginFailed`. |
| `RecordSuccessfulLogin()` | Resetea `AccessFailedCount`, limpia `LockoutEndsAt`, fija `LastLoginAt`, emite `UserLoggedIn`. |
| `IsLockedOut(now)` | `LockoutEndsAt is not null && LockoutEndsAt > now`. |
| `ConfirmEmail()` | Fija `EmailConfirmed = true`. Lo usa el seed del admin (§6); sin flujo de correo es la única vía por ahora. |
| `Activate` / `Deactivate(by)` | `Deactivate` revoca todos los refresh tokens y emite `UserDeactivated`. |
| `IssueRefreshToken(hash, expiresAt, ip)` | Añade a la colección. |
| `RotateRefreshToken(oldHash, newHash, expiresAt, ip)` | Marca el viejo como revocado con `ReplacedByHash`, añade el nuevo. |
| `RevokeAllRefreshTokens(reason)` | Revoca la cadena completa. |
| `Delete(by)` | `SoftDelete` heredado + revoca tokens + emite `UserDeleted`. |

Eventos en `Domain/DomainEvents/Identity/IdentityEvents.cs`: `UserCreated`,
`UserLoggedIn`, `UserLoginFailed`, `UserLockedOut`, `UserPasswordChanged`,
`UserActivated`, `UserDeactivated`, `UserDeleted`, `RefreshTokenReuseDetected`.

### 3.4 `RefreshToken`

Entidad hija de `User`. **Nunca almacena el token en claro** — solo su SHA-256:

```
Id  UserId  TokenHash  ExpiresAt  CreatedAt  CreatedByIp
RevokedAt?  RevokedByIp?  ReplacedByHash?  RevokedReason?
IsActive => RevokedAt is null && ExpiresAt > now
```

Habilita **rotación con reuse detection**: si llega un refresh cuyo hash existe
pero ya está revocado, se revoca toda la cadena del usuario y se emite
`RefreshTokenReuseDetected` (señal de token robado).

### 3.5 Repositorio

`Domain/Interfaces/Repositories/Identity/IUserRepository.cs` — `GetByIdAsync`,
`GetByNormalizedEmailAsync`, `GetByRefreshTokenHashAsync`, `ExistsByEmailAsync`,
`Add`, `GetPagedAsync`.

## 4. Application — `EnterpriseApp.Application/Features/Identity/`

### 4.1 Interfaces nuevas en `Common/Interfaces/`

- `IPasswordHasher` — `string Hash(string password)`,
  `PasswordVerificationResult Verify(string hash, string password)`.
  El enum `PasswordVerificationResult { Failed, Success, SuccessRehashNeeded }`
  se declara en Application para no filtrar el tipo de Identity a esta capa.
- `ITokenService` — `AccessTokenResult GenerateAccessToken(TokenSubject subject)` y
  `RefreshTokenResult GenerateRefreshToken()` (devuelve `Plain`, `Hash`, `ExpiresAt`).

### 4.2 Casos de uso

**Auth** (`Features/Identity/Auth/`):

| Caso | Notas |
|---|---|
| `LoginCommand(Email, Password)` | Ver §4.3 |
| `RefreshTokenCommand(RefreshToken)` | Rotación + reuse detection |
| `LogoutCommand(RefreshToken)` | Revoca ese token |
| `ChangePasswordCommand(Current, New)` | Sobre el usuario autenticado |
| `GetCurrentUserQuery()` | `/auth/me` — perfil + roles + permisos efectivos |

**Users** (`Features/Identity/Users/`): `CreateUserCommand`, `UpdateUserCommand`,
`ActivateUserCommand`, `DeactivateUserCommand`, `DeleteUserCommand`,
`GetUsersQuery` (paginada), `GetUserByIdQuery`.

Cada comando con su validator de FluentValidation en la misma carpeta, según
`.claude/rules/architecture.md`. Política de contraseña en el validator: mínimo
8 caracteres, al menos una mayúscula, una minúscula y un dígito.

### 4.3 `LoginCommandHandler` — el núcleo

```
1. Buscar por NormalizedEmail
2. Si no existe        -> Auth.InvalidCredentials
3. Verify(hash, password)
   fallo   -> RecordFailedLogin(max, duration); SaveChanges; Auth.InvalidCredentials
   rehash  -> recalcular hash y persistir
4. Si IsLockedOut      -> Auth.AccountLocked
5. Si !IsActive        -> Auth.AccountInactive
6. RecordSuccessfulLogin()
7. Permisos vía IPermissionService.GetPermissionsForUserAsync(userId, tenantId)
8. Firmar access token; generar refresh; IssueRefreshToken(hash)
9. SaveChanges -> los interceptores despachan los domain events
```

**Enumeración de usuarios:** los pasos 2 y 3 devuelven el **mismo** `Error`
(`Auth.InvalidCredentials`, "Invalid credentials.") para no revelar si el email
existe. Los estados de bloqueo e inactividad sí se distinguen, pero se comprueban
**después** de verificar la contraseña (pasos 4 y 5): así solo se informa de ellos
a quien demuestra conocer la credencial, y un atacante no puede sondear qué
cuentas existen o están bloqueadas.

**Nota:** el bloqueo se evalúa después de verificar, pero `RecordFailedLogin` del
paso 3 sigue incrementando el contador en cada intento fallido, de modo que el
lockout se aplica igualmente. Una contraseña correcta sobre una cuenta bloqueada
devuelve `AccountLocked` sin emitir tokens.

### 4.4 Claims del access token

Conforme a `.claude/rules/authorization.md §4`:

```
sub        userId (Guid)          name   FullName
email      Email                  tid    TenantId (solo si != null)
roles[]    nombres de rol         perms  array JSON de códigos
psv        IPermissionService.ComputeSnapshotVersion(perms)
amr        ["pwd"]                auth_time  unix seconds
jti  iat  exp  iss  aud
```

`perms` se emite como claim único con array JSON, que es la primera forma que
`CurrentUser.ResolvePermissions` y `PermissionAuthorizationHandler` intentan leer.

### 4.5 Errores

`Common/Errors/AuthErrors.cs`, siguiendo el patrón `Error(Code, Description)`:

| Error | HTTP |
|---|---|
| `Auth.InvalidCredentials` | 401 |
| `Auth.AccountLocked` | 423 |
| `Auth.AccountInactive` | 403 |
| `Auth.InvalidRefreshToken` | 401 |
| `Auth.RefreshTokenReused` | 401 |
| `User.EmailAlreadyExists` | 409 |
| `User.NotFound` | 404 |

## 5. Infrastructure

- `Identity/PasswordHasherAdapter.cs` — envuelve `PasswordHasher<User>`
  (PBKDF2-HMAC-SHA512 en .NET 9). Sin paquete nuevo.
- `Identity/TokenService.cs` — firma HS256 con `Jwt:Key`, lee TTLs de `AuthOptions`.
  El refresh son 32 bytes de `RandomNumberGenerator` en Base64Url; se persiste su SHA-256.
- `Repositories/Identity/UserRepository.cs`.
- `Persistence/Configurations/Identity/UserConfiguration.cs` — conversión de `UserId`
  y `Email`, índice **único** sobre `NormalizedEmail` (filtrado por `IsDeleted = false`
  donde el proveedor lo soporte), relación con `RefreshToken`.
- `Persistence/Configurations/Identity/RefreshTokenConfiguration.cs` — índice sobre `TokenHash`.
- `AppDbContext` + `IApplicationDbContext`: `DbSet<User> Users`, `DbSet<RefreshToken> RefreshTokens`.
  Filtro global `HasQueryFilter(u => !u.IsDeleted)` sobre `User`.
- Migración nueva `AddIdentity`.
- `Persistence/Seeds/DatabaseSeeder` — nuevo paso `SeedAdminUserAsync` (§6).

## 6. Seed del usuario administrador

Se ejecuta al final de `DatabaseSeeder.SeedAsync`, después de roles y permisos,
y es **idempotente**: si ya existe un usuario con ese email normalizado, no hace nada.

```
1. Leer AuthOptions.SeedAdmin { Enabled, Email, Password, FirstName, LastName }
2. Si !Enabled -> salir
3. Si existe usuario con ese NormalizedEmail -> salir
4. User.Create(email, hasher.Hash(password), ..., createdBy: "system-seed")
   seguido de user.ConfirmEmail()   -> IsActive = true, EmailConfirmed = true
5. Localizar el rol de sistema "super-admin" (ya seedeado, con permiso wildcard "*")
6. UserRole.Create(user.Id.Value.ToString(), role.Id, tenantId: null, assignedBy: "system-seed")
7. SaveChanges
8. Si la contraseña sigue siendo la del default -> LogWarning muy visible
```

Defaults en `appsettings.json`: `admin@enterpriseapp.local` / `Admin123!`.
Al tener el rol `super-admin` con permiso wildcard `*`, este usuario pasa todos
los `[RequirePermission]` del sistema — es el que permite probar la API de
extremo a extremo desde el primer arranque.

El `LogWarning` del paso 8 se emite siempre que la contraseña coincida con el
default, para que no llegue a producción por descuido.

> `UserRole.UserId` es `string`; se usa `user.Id.Value.ToString()` de forma
> consistente en todo el módulo, incluido el claim `sub` y las consultas de
> `PermissionService`. Es el contrato que fija el formato.

## 7. Desbloqueo del step-up MFA

**Problema:** `StepUpMfaAuthorizationHandler` solo llama a `context.Succeed` si
`amr` contiene `mfa` u `otp`. Sin MFA implementado, todo endpoint con
`[RequireStepUpMfa]` — asignar/quitar rol y grant/revoke de permisos en
`RolesController` — devuelve **403 permanente**, incluso a `super-admin`. El
módulo de usuarios sería inútil: podrías autenticarte pero no gestionar roles.

**Solución:** introducir `StepUpMfaOptions { bool Enabled, int MaxAgeMinutes }`
inyectado en el handler.

- `Enabled = false` (**default** mientras no exista MFA): concede el requisito a
  cualquier principal autenticado. Se registra un `LogWarning` al arrancar
  indicando que el step-up MFA está desactivado.
- `Enabled = true`: comportamiento actual, sin cambios.

Así `[RequireStepUpMfa]` no queda decorativo — pasa a ser un interruptor real que
se activa el día que se añada TOTP, sin tocar ningún controller.

## 8. API

### 8.1 `AuthController` — `/api/v1/auth`

| Método | Ruta | Auth | Éxito |
|---|---|---|---|
| POST | `/login` | `[AllowAnonymous]` + rate limit `login` | 200 `AuthResponse` |
| POST | `/refresh` | `[AllowAnonymous]` + rate limit `login` | 200 `AuthResponse` |
| POST | `/logout` | autenticado | 204 |
| GET | `/me` | autenticado | 200 `CurrentUserDto` |
| POST | `/change-password` | autenticado | 204 |

### 8.2 `UsersController` — `/api/v1/users`

| Método | Ruta | Permiso | Éxito |
|---|---|---|---|
| GET | `/` | `user.view` | 200 `PagedUsersDto` |
| GET | `/{id:guid}` | `user.view` | 200 `UserDetailDto` |
| POST | `/` | `user.create` | 201 + `Location` |
| PUT | `/{id:guid}` | `user.update` | 204 |
| PATCH | `/{id:guid}/activate` | `user.activate` | 204 |
| PATCH | `/{id:guid}/deactivate` | `user.deactivate` | 204 |
| DELETE | `/{id:guid}` | `user.delete` | 204 |

Mismo estilo que `RolesController`: `ISender` por primary constructor,
`ProducesResponseType`, y traducción de `Result.Error` a `Problem(...)`.

### 8.3 Rate limiting

`AddRateLimiter` no existe hoy en `Program.cs` pese a estar en
`.claude/rules/security.md`. Se añade con política `login`: sliding window,
10 permisos por minuto, 6 segmentos, particionada por IP. Se aplica con
`[EnableRateLimiting("login")]` en `login` y `refresh`.

### 8.4 Configuración — sección `Auth` en `appsettings.json`

```jsonc
"Auth": {
  "AccessTokenMinutes": 15,
  "RefreshTokenDays": 7,
  "MaxFailedAccessAttempts": 5,
  "LockoutMinutes": 15,
  "StepUpMfa": { "Enabled": false, "MaxAgeMinutes": 5 },
  "SeedAdmin": {
    "Enabled": true,
    "Email": "admin@enterpriseapp.local",
    "Password": "Admin123!",
    "FirstName": "System",
    "LastName": "Administrator"
  }
}
```

`Jwt:ExpiryMinutes` (hoy 60) queda obsoleto y se elimina: el TTL pasa a
`Auth:AccessTokenMinutes` con 15 minutos, conforme a `authorization.md §4`
("TTL del access token: ≤ 15 min").

## 9. Testing

Se implementa con TDD — test primero en cada unidad.

**`EnterpriseApp.Domain.Tests/Identity/`**

- `UserTests`: bloqueo tras N fallos; el bloqueo expira; login correcto resetea el
  contador; `ChangePassword` rota `SecurityStamp` y revoca tokens; `Deactivate`
  revoca tokens; la rotación de refresh marca `ReplacedByHash`.
- `EmailTests`: normalización, y formatos inválidos lanzan `DomainException`.

**`EnterpriseApp.Application.Tests/Features/Identity/`**

- `LoginCommandHandlerTests` con `IUserRepository`, `IPasswordHasher`,
  `ITokenService` e `IPermissionService` mockeados (Moq + FluentAssertions, el
  stack ya existente): credenciales válidas devuelven tokens; contraseña
  incorrecta incrementa el contador y devuelve `InvalidCredentials`; email
  inexistente devuelve **el mismo** error; cuenta bloqueada; cuenta inactiva.
- `RefreshTokenCommandHandlerTests`: rotación correcta; un token ya revocado
  dispara reuse detection y revoca la cadena completa.

**Arquitectura:** los tests existentes deben seguir en verde — en particular el
que verifica que `Domain` no depende de nada externo. Es la comprobación
automática de que la regla de §3.1 se respetó.

## 10. Fuera de alcance

- MFA / TOTP (enroll, verify, emisión real de `amr:["mfa"]`) — desbloqueado por §7.
- Confirmación de email y recuperación de contraseña (requieren `IEmailSender`).
- Registro público self-service.
- Invitaciones con token de un solo uso.
- Proveedores externos (OIDC, social login).
