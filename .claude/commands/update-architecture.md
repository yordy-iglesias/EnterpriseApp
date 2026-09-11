# /update-architecture

Update the architecture diagram at `Docs/architecture/architecture-diagram.html` to reflect the current state of the codebase.

## Steps

1. **Read the current spec** from `Docs/architecture/architecture-diagram.json`.

2. **Identify what changed.** The user will describe the change, or you can infer it from recent commits (`git log --oneline -10`) and the current source tree. Focus on changes that affect:
   - New aggregates / domain entities
   - New or removed infrastructure components (DB providers, cache, messaging)
   - New API endpoints or significant endpoint restructuring
   - New MediatR pipeline behaviors
   - New security boundaries or auth mechanisms
   - Changes to the layer dependency structure

3. **Update the spec JSON** (`Docs/architecture/architecture-diagram.json`). Follow these rules:
   - Add new nodes with fresh stable IDs (lowercase, hyphens, e.g. `outbox-worker`)
   - Remove nodes that no longer exist
   - Add/remove connections accordingly
   - Keep `meta.quality_profile: "showcase"`
   - Keep the primary path (`client → api → pipeline → handler → ef_core → postgres`) as the visual spine unless the architecture itself changed that flow
   - Update `cards` to reflect what changed

4. **Validate** the updated spec:
   ```
   node C:/Users/yordy/.claude/skills/archify/bin/archify.mjs validate architecture Docs/architecture/architecture-diagram.json --quality showcase --json
   ```
   Fix any diagnostics before proceeding (label overlaps → apply suggested `labelAt`/`labelDy`; readability → shorten sublabels or reduce viewBox width).

5. **Deliver** the HTML:
   ```
   node C:/Users/yordy/.claude/skills/archify/bin/archify.mjs deliver architecture Docs/architecture/architecture-diagram.json Docs/architecture/architecture-diagram.html --quality showcase --json
   ```
   A non-zero exit is a failure — do not proceed.

6. **Visual check**:
   ```
   node C:/Users/yordy/.claude/skills/archify/bin/archify.mjs visual-check Docs/architecture/architecture-diagram.html --json
   ```
   Fix any viewport overflow before reporting success.

7. **Update the published Artifact** (keep the same URL):
   Republish `Docs/architecture/architecture-diagram.html` to `https://claude.ai/code/artifact/2a452feb-46f4-4974-858f-101fdb313c7d` so the shared link stays current.

8. **Report** what changed: which nodes/connections were added or removed, and the new validate/deliver receipt.

## What counts as an architectural change

| Trigger | Example |
|---|---|
| New aggregate or service | Adding `Order` domain with `OrderRepository` |
| New infrastructure dep | Adding MassTransit / RabbitMQ |
| New DB provider | Enabling SQL Server alongside SQLite |
| New pipeline behavior | Adding `IdempotencyBehavior` |
| New security layer | Adding step-up MFA middleware |
| Layer dependency shift | Application gaining a direct Redis dependency |
| New background worker | `OutboxWorker` draining domain events |

## What does NOT require a diagram update

- New commands/queries inside existing features (same handler node)
- New EF configurations or migrations (same EF Core node)
- New permissions or roles (same security boundary)
- Test projects
- Bug fixes with no structural impact
