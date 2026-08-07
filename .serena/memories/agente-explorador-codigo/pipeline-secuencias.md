# Pipeline de Validación — FluentValidation + ValidationBehavior

> Detalle del ciclo de vida de validación. Ver también [[pipeline-cqrs]] para el flujo completo.

## ValidationBehavior

Ejecuta TODOS los `IValidator<TRequest>` registrados antes de que el handler reciba la request.

```
ValidationBehavior.Handle(request, next, ct)
  → validators = IEnumerable<IValidator<TRequest>>  (inyectados por DI)
  → context = new ValidationContext<TRequest>(request)
  → results = validators.Select(v => v.Validate(context))
  → failures = results.SelectMany(r => r.Errors).Where(f => f is not null)
  → if (failures.Count > 0) throw new ValidationException(failures)
  → else await next()
```

`ValidationException` es capturada por `GlobalExceptionMiddleware` → HTTP 400 ProblemDetails:
```json
{
  "type": "...",
  "title": "Validation failed",
  "status": 400,
  "errors": {
    "Title": ["'Title' must not be empty."],
    "Priority": ["'Priority' has a range of values which does not include '99'."]
  }
}
```

## Convención de validators

- Un validator por command/query, mismo folder.
- Nombre: `{CommandName}Validator` / `{QueryName}Validator`.
- Registrado automáticamente via `AddValidatorsFromAssembly` en `ApplicationServiceExtensions`.

```csharp
public sealed class CreateTodoCommandValidator : AbstractValidator<CreateTodoCommand>
{
    public CreateTodoCommandValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Priority)
            .IsInEnum();
    }
}
```

## Queries sin validator

Si una query no tiene validator registrado, `ValidationBehavior` la pasa directo al handler.
No es un error — solo añadir validator cuando haya invariants a validar en la capa de entrada.
