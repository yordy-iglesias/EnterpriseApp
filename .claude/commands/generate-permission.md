# /generate-permission — Añadir un nuevo permiso al sistema

Usa este comando para crear un permiso de forma consistente: define el código,
genera la migración de seed, registra los tests y actualiza la documentación.

## Cómo invocarlo

El usuario invoca `/generate-permission` con un argumento descriptivo, por ejemplo:

```
/generate-permission patient.export.pdf — exportar la ficha del paciente como PDF (sensible)
```

Si el argumento es ambiguo, Claude **debe** usar `AskUserQuestion` antes de generar
nada. Preguntas mínimas:

1. **Code** — confirmar el código `{module}.{action}[.{qualifier}]`.
2. **DisplayName** — texto para la UI de administración de roles.
3. **IsSensitive** — `true` si requiere step-up MFA (firma electrónica, exportar PII, etc.).
4. **Tenant scope** — global (`null`) o tenant-específico.
5. **Roles iniciales** — qué roles del seed reciben el permiso por defecto.

## Validaciones obligatorias antes de generar

- [ ] El `code` cumple el regex `^[a-z][a-z0-9-]*(\.[a-z][a-z0-9-]*){1,2}$`.
- [ ] El `code` no existe ya en `PermissionsSeed.cs` ni en BD (grep + query).
- [ ] El `module` referenciado existe (al menos un permiso previo con ese módulo o creación explícita).
- [ ] El `action` está en la lista canónica:
      `view, create, update, delete, export, import, approve, reject, sign, cancel, assign, revoke, read`.
      Si no está, Claude debe pedir confirmación al usuario.
- [ ] Si `IsSensitive = true`, el endpoint que lo use debe tener documentado el step-up MFA.

## Pasos que ejecuta el comando

### 1. Añadir constante
Editar `Domain/Authorization/PermissionCodes.cs` (clase estática con todos los códigos):

```csharp
public static class PermissionCodes
{
    public static class Patient
    {
        public const string View = "patient.view";
        public const string ViewOwn = "patient.view.own";
        public const string Create = "patient.create";
        public const string ExportPdf = "patient.export.pdf";   // ← nuevo
    }
}
```

> **Nota**: agrupar por módulo en clases anidadas. Mantener orden alfabético dentro de cada grupo.

### 2. Añadir al seed
Editar `Infrastructure/Persistence/Seeds/PermissionsSeed.cs`:

```csharp
new PermissionSeedItem(
    Code: PermissionCodes.Patient.ExportPdf,
    DisplayName: "Exportar paciente a PDF",
    Description: "Permite generar y descargar la ficha completa del paciente en PDF",
    IsSensitive: true,
    DefaultRoles: ["nurse", "doctor"]
),
```

### 3. Generar migración
```bash
dotnet ef migrations add Add_Permission_PatientExportPdf \
    -p src/YourApp.Infrastructure -s src/YourApp.Api \
    -o Persistence/Migrations
```

La migración debe contener **solo** el INSERT del seed (sin cambios de schema).

### 4. Crear test de seed
Editar `tests/YourApp.IntegrationTests/Authorization/PermissionsSeedTests.cs`:

```csharp
[Fact]
public async Task Seed_ShouldContain_PatientExportPdf()
{
    var permission = await _db.Permissions.SingleOrDefaultAsync(p => p.Code == "patient.export.pdf");
    permission.Should().NotBeNull();
    permission!.IsSensitive.Should().BeTrue();
}
```

### 5. Actualizar documentación
- Añadir el permiso al README del módulo: `docs/modules/patient/permissions.md`.
- Si es `IsSensitive`, documentar la regla de step-up MFA en `docs/security/sensitive-actions.md`.

### 6. Si requiere uso inmediato en un endpoint
Generar/actualizar el endpoint con el atributo:

```csharp
group.MapPost("/{id:guid}/export-pdf", ExportPatientAsync)
    .RequirePermission(PermissionCodes.Patient.ExportPdf)
    .RequireStepUpMfa()                      // si IsSensitive
    .WithName("ExportPatientPdf");
```

## Output esperado del comando

Claude debe devolver un resumen estructurado:

```
✅ Permiso creado: patient.export.pdf

Archivos modificados:
- Domain/Authorization/PermissionCodes.cs       (+1 línea)
- Infrastructure/Persistence/Seeds/PermissionsSeed.cs   (+6 líneas)
- Infrastructure/Persistence/Migrations/20260428_Add_Permission_PatientExportPdf.cs   (nuevo)
- tests/YourApp.IntegrationTests/Authorization/PermissionsSeedTests.cs   (+10 líneas)
- docs/modules/patient/permissions.md           (+1 línea)

Próximos pasos manuales:
1. dotnet ef database update -p src/YourApp.Infrastructure -s src/YourApp.Api
2. Verificar que los roles `nurse`, `doctor` aparecen en RolePermission tras el seed
3. Si hay tenants existentes, ejecutar el job `BackfillTenantPermissionsCommand`

Audit:
- Domain event PermissionCreated emitido en startup → quedará registrado en PermissionAuditLog
```

## Anti-patterns a evitar

```csharp
// ❌ Crear el permiso sin pasar por seed (cambio manual en BD)
INSERT INTO Permissions (Code, ...) VALUES ('patient.export.pdf', ...);

// ❌ Code en PascalCase o mezcla de idiomas
public const string ExportPDF = "Patient.ExportPDF";
public const string ExportarPdf = "paciente.exportar.pdf";

// ❌ Usar el permiso solo en frontend (validación client-side)
if (user.perms.includes('patient.export.pdf')) showButton();   // OK para UI
// pero el backend debe validar con [RequirePermission] siempre

// ❌ Omitir IsSensitive en operaciones que tocan PII o son irreversibles
new PermissionSeedItem("patient.delete", IsSensitive: false)   // delete debería ser sensitive

// ❌ Asignar el permiso a 'super-admin' explícitamente — super-admin tiene "*"
DefaultRoles: ["super-admin", "doctor"]    // sobra "super-admin"
```

## Recordatorio

Después de generar el permiso, sugerir al usuario ejecutar:

```bash
dotnet test --filter "Category=Authorization"
```

para verificar que el seed y los tests pasan antes de hacer commit.
