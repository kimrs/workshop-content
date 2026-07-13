# Workshop: Event-Driven Migration App

A small, self-contained, event-driven **Source → Target migration** app, built as a teaching
vehicle for an architecture & code-structure workshop. It migrates a client (identified by an
organization number) from a simulated **Source** system to a simulated **Target** system. Neither
system makes real network calls — both can be **configured to fail** so the workshop can demonstrate
**retry** and **error handling** live.

The value is in the **patterns**, not breadth: hexagonal architecture, an event-driven core, the
**transactional outbox** and **inbox** patterns, two fan-out/fan-in points, bounded retries, and
validation-driven automatic cancellation.

## The flow

```
                 ┌─ Lock Source ──────────┐
Start  ─────────►├─ Trigger SAF-T export ─┤ (fan-in)
                 └─ Lock Target ──────────┘
      │
      ▼
Transform Source → Target   (auto-cancel if the client has no address)
      │
      ▼
Upload pre-SAF-T work  →  Upload SAF-T file
      │
      ▼
Wait for user approval        ◄── manual POST /Approve
      │
      ▼
                 ┌─ Unlock Source ─┐
                 ├─ Unlock Target ─┤ (fan-in)
                 └─────────────────┘
      │
      ▼
Notify completion
```

- **Setup fan-out** — `Lock Source`, `Trigger SAF-T export`, `Lock Target` are three independent,
  individually-retryable steps. When all three complete, the migration fans in to `Transform`.
- **Teardown fan-out** — `Unlock Source`, `Unlock Target`. When both complete, the migration fans in,
  transitions to its terminal state, and notifies.
- **The one domain rule**: pre-SAF-T work is just "does the client have an address?". No address ⇒
  the migration **auto-cancels** (unlock Source & Target, then notify).

### The state machine

| State            | Fan-in flags                                      | Transition(s)                              |
|------------------|---------------------------------------------------|--------------------------------------------|
| `Created`        | `SourceLocked`, `TargetLocked`, `ExportTriggered` | `IsReadyToTransform` (→ `Prepared`, performed by the last flag write, spec 9) |
| `Prepared`       | —                                                 | `Transform()`                              |
| `Transformed`    | —                                                 | `UploadPreSaft()`                          |
| `PreSaftUploaded`| —                                                 | `UploadSaft()`                             |
| `SaftUploaded`   | — (awaits approval)                               | `Approve()`                                |
| `Unlocking`      | `SourceUnlocked`, `TargetUnlocked`                | `IsFullyUnlocked`; `Complete()`            |
| `Completed`      | terminal                                          | —                                          |
| `Cancelling`     | `SourceUnlocked`, `TargetUnlocked`                | `IsFullyUnlocked`; `FinalizeCancellation()`|
| `Cancelled`      | terminal                                          | —                                          |

`Created`, `Prepared`, `Transformed`, `PreSaftUploaded`, `SaftUploaded` are `ICancellable` (via `POST /Cancel`).
On entering `Cancelling`, each unlock flag is seeded `true` for a system that was never locked
(nothing to release). Once `Cancelled` or `Completed`, the org number can be migrated again.

## Idempotency (important)

Every stage transition is driven by a **domain-event message**. The message envelope is
`(MigrationId, DomainEvent, Attempt, Payload, TraceParent)`. **There is no `MessageId`.** Two messages
are the same iff `(MigrationId, DomainEvent, Attempt)` are equal — and that triple is the **Inbox**
composite primary key.

- **Redelivery** of the same attempt is deduped by the inbox → a handler runs at most once per triple.
- **A retry is a new attempt**: a faulted retryable step reschedules the event with `Attempt + 1`,
  producing a new triple that is *not* deduped.
- **An abort is recorded**: a handler failure rolls its work back and stamps the inbox row with
  `FailedAt`/`FailureReason` — the durable trace a human investigates; redeliveries of that
  attempt are skipped.

Delivery guarantees, honestly stated: the **Postgres** transport is at-least-once end to end —
messages are acknowledged (`ConsumedAt`) only *after* processing, so a crash mid-handling
redelivers and the inbox dedups. The **InMemory** transport pops from an in-process channel;
in-flight messages die with the process (inherent to an in-process bus).

## Architecture

Hexagonal (ports & adapters) with an event-driven core, plus transactional outbox + inbox.

- **Domain** is pure (`Mt.Domain`, depends only on `Mt.Results`): value objects, the `Migration`
  aggregate (a discriminated union of states), ports (interfaces), and stage handlers.
- **Adapters** implement the ports: `Mt.Persistence` (EF Core 10 + Npgsql), `Mt.Source` / `Mt.Target`
  (simulations), `Mt.Transport` (message bus).
- **Entry points**: `Mt.Api` (writes to the outbox), `Mt.Outbox` (publishes), `Mt.Processor`
  (consumes → inbox → dispatch). `Mt.Host` runs all three in one process for the InMemory default.

Each handler does its work, mutates state, and **atomically writes the next event to the outbox in the
same transaction** (transactional outbox). The outbox worker publishes at-least-once; the processor
runs each message through the inbox for idempotency before dispatching.

### Projects

```
Core
  Mt.Results       result pattern library (no dependencies)
  Mt.Domain        value objects, aggregate, ports, stage handlers
Adapters
  Mt.Persistence   EF Core 10 + Npgsql; outbox, inbox, scheduler, command ports,
                   and the Postgres transport implementation (polling table)
  Mt.Source        simulated Source (configurable failures)
  Mt.Target        simulated Target (configurable failures)
  Mt.Transport     IMessagePublisher/IMessageConsumer ports + the InMemory transport
                   (no dependencies — a real broker slots in as another implementation)
Entry points
  Mt.Api           minimal API: Start / Approve / Cancel
  Mt.Outbox        BackgroundService: polls the outbox, publishes
  Mt.Processor     BackgroundService: consumes, runs the inbox, dispatches
  Mt.Host          combined host (all three, InMemory) for a one-command run
Tests
  Mt.Results.Tests, Mt.Domain.Tests, Mt.Persistence.Tests, Mt.Source.Tests, Mt.Target.Tests
```

Every project targets `net10.0` with `Nullable`, `ImplicitUsings`, and `TreatWarningsAsErrors`
enabled. `LangVersion` is **C# 14** — the `extension(...)` block syntax the guidelines mandate is a
C# 14 feature.

## Coding Guidelines

These are the rules the code follows; hold to them when extending it.

1. **Vertical slices, not layers.** A stage lives in one folder with its handler, event, ports and
   settings together (`Stages/LocksSource/{Handler,DomainEvent,ILockSource,Settings}.cs`) — not
   `Handlers/`, `Interfaces/`, `Settings/` split by kind.
2. **Short names in namespaces.** In `Mt.Domain.Stages.LocksSource` the class is `Handler`, not
   `LockSourceHandler`.
3. **Duplicate before you abstract.** Extract shared code only at the **fourth** repetition. The two
   lock slices and two unlock slices are intentional near-duplicates — do **not** DRY them up.
4. **`Result<T>` everywhere; no exceptions for control flow.** Chain with `Then`/`ThenAsync`.
   Propagate failures by chaining, not manual `if (x.IsFailed(...))` — unless the value is needed
   later on its own line. An async producer binds to a **named variable first**, never
   `await (await X).ThenAsync(...)`.
5. **No `null` in the domain; value objects, not primitives.** Public APIs take value objects.
   Optionality is modeled with subtypes (`Client.WithAddress` / `Client.WithoutAddress`), not
   nullables. Validate once in a static `Create` returning `Result<T>`.
6. **No mutation of input parameters.** Methods return new data; no accumulator-sink style.
7. **Manual mapping.** No AutoMapper. Hand-written `To<Target>()` extension methods with `required`
   members so the compiler catches missing mappings.
8. **C# 14 extension members.** Use the `extension(...)` block syntax, not the old `this`-parameter form.
9. **Persistence throws on corrupt data.** Reconstruction throws `IllegalValuesInDbException` when a
   `Create` fails — corrupt rows are bugs, not recoverable results.
10. **Structured logging** with named `EventId`s. Levels: Critical / Error / Warning / Information / Debug.
11. **The inner loop.** After every meaningful change: build (warnings are errors), run the relevant
    tests, then self-review the diff and delete anything not asked for.

## Local run & workshop demo

**Prerequisites:** .NET 10 SDK, Docker.

1. **Start Postgres** (host `localhost`, database `workshop`):
   ```bash
   docker compose up -d
   ```
   Connection string (also in each `appsettings.json`):
   `Host=localhost;Port=5432;Database=workshop;Username=workshop;Password=workshop`

2. **Apply EF migrations.** The combined host does this automatically on startup. To do it by hand:
   ```bash
   dotnet tool restore
   dotnet ef database update -p src/Mt.Persistence -s src/Mt.Persistence
   ```

3. **Choose a transport** (`Transport:Kind`):
   - **`InMemory`** (default) — run everything in one process, one log stream:
     ```bash
     dotnet run --project src/Mt.Host
     ```
   - **`Postgres`** — run the three entry points as separate processes (three terminals):
     ```bash
     dotnet run --project src/Mt.Api
     dotnet run --project src/Mt.Outbox
     dotnet run --project src/Mt.Processor
     ```

4. **Drive the flow** (the API listens on the URL printed at startup, e.g. `http://localhost:5xxx`).
   Start takes what each external system calls this migration (spec 8) — the adapters translate
   the migration id into these, so the logs show punch cards, holo-crystals and pigeons:
   ```bash
   curl -X POST http://localhost:5xxx/Migration/998877665/Start \
     -H 'Content-Type: application/json' \
     -d '{"punchCardNumber":"PC-1972-0042","holoCrystalId":"HOLO-7F3A-CAFE","carrierPigeonTag":"PIGEON-OSLO-9"}'
   curl -X POST http://localhost:5xxx/Migration/998877665/Approve   # after SAF-T uploaded
   curl -X POST http://localhost:5xxx/Migration/998877665/Cancel    # cancel while cancellable
   ```

5. **Flip the knobs** (in the host's `appsettings.json`) and watch the logs narrate:
   - `Source:Lock:FailUntilAttempt: 2` — transient failure, then a successful retry.
   - `Target:Unlock:AlwaysFail: true` — retry exhaustion → `OutOfRetriesFailure` → error handling.
   - `Client:HasAddress: false` — auto-cancellation on invalid data.
   - `Transport:RedeliverProbability: 0.15` — deliver some messages twice; the inbox dedups them.

Every log line makes the current stage, attempt, and fan-in progress obvious — the logs are the
workshop's narration.

## Testing

```bash
dotnet test
```

- `Mt.Results.Tests` — the combinators, including `Match` and `Done`.
- `Mt.Domain.Tests` — handlers with mocked ports: retry schedules `attempt+1`, exhaustion returns
  `OutOfRetriesFailure`, already-cancelled skips gracefully, fan-in emits only on the last sibling,
  the auto-cancel path, and aggregate transitions.
- `Mt.Source.Tests` / `Mt.Target.Tests` — `FailUntilAttempt` and `AlwaysFail` behavior.
- `Mt.Persistence.Tests` — **Testcontainers (Docker required)**: inbox duplicate-skip, the concurrent
  unique-violation race, abort recording, mapping-throws-on-corrupt, and the Start/Cancel commands.
- `Mt.EndToEnd.Tests` — **Testcontainers (Docker required)**: the composed pipeline (command →
  outbox worker → transport → processor → inbox → stages) for a retry-then-succeed happy path
  and the auto-cancel path.
