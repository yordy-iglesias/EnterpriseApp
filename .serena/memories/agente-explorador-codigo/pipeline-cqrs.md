# Pipeline CQRS — flujo detallado

> Complementa [[arquitectura]]. Aquí el flujo exacto para un command y una query de ejemplo.

## Flujo de un Command (CreateTodo)

```
HTTP POST /api/todos
  → TodosController.CreateAsync([FromBody] CreateTodoRequest req)
  → mediator.Send(new CreateTodoCommand(req.Title, req.Description, req.Priority, req.DueDate))
  → LoggingBehavior.Handle  (log "CreateTodoCommand started")
  → ValidationBehavior.Handle  (ejecuta CreateTodoCommandValidator; 400 si falla)
  → CachingBehavior.Handle  (CreateTodoCommand no implementa ICachedQuery → pasa directo)
  → PerformanceBehavior.Handle  (Stopwatch.Start)
  → CreateTodoCommandHandler.Handle
      → TodoItem.Create(title, description, priority, dueDate, currentUser.UserId)
          → RaiseDomainEvent(new TodoItemCreatedEvent(...))
      → db.TodoItems.Add(todo)
      → db.SaveChangesAsync(ct)
          → AuditableEntitySaveChangesInterceptor: stampa CreatedAt + CreatedBy
          → [SaveChanges completa]
          → DomainEventDispatcherInterceptor: publica TodoItemCreatedEvent via IPublisher
              → [handlers de TodoItemCreatedEvent si existen]
      → return todo.Id.Value  (Guid)
  → PerformanceBehavior: Stopwatch.Stop (warning si >500ms)
  → LoggingBehavior: log "CreateTodoCommand completed in Xms"
→ HTTP 201 Created + Location: /api/todos/{id}
```

## Flujo de una Query cacheable (GetTodoById)

```
HTTP GET /api/todos/{id}
  → TodosController.GetByIdAsync(Guid id)
  → mediator.Send(new GetTodoByIdQuery(id))
  → LoggingBehavior
  → ValidationBehavior  (GetTodoByIdQuery no tiene validator → pasa)
  → CachingBehavior:
      → ICacheService.GetAsync<TodoItemDto>($"todo:{id}")
        → HIT: return cacheado → saltar al PerformanceBehavior
        → MISS: continuar
  → PerformanceBehavior
  → GetTodoByIdQueryHandler.Handle
      → db.TodoItems.AsNoTracking().Where(t => t.Id == TodoId.From(id))
                    .ProjectTo<TodoItemDto>(mapper).FirstOrDefaultAsync(ct)
  → PerformanceBehavior: stop
  → CachingBehavior: ICacheService.SetAsync($"todo:{id}", result, expiration)
  → LoggingBehavior
→ HTTP 200 OK + body: TodoItemDto  (o 404 si null)
```

## Notas

- `CachingBehavior` activa SOLO si `TRequest : ICachedQuery<TResponse>`.
  Implementar `ICachedQuery<T>` en la query exponiendo `CacheKey` y `Expiration`.
- `ValidationBehavior` lanza `ValidationException` con la lista completa de errores.
  `GlobalExceptionMiddleware` la convierte a HTTP 400 ProblemDetails con `errors: {}`.
- Los domain events se publican DESPUÉS del commit — si el handler de evento falla,
  la transacción ya se confirmó. Diseñar handlers de eventos como operaciones idempotentes
  o usar outbox pattern para operaciones críticas.
