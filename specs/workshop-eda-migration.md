# Spec: Workshop EDA Migration App (build from scratch)

## 0. Purpose & audience

This spec tells Claude how to generate a **small, self-contained event-driven
application from scratch** for a **public architecture & code-structure
workshop**.

The app is a stripped-down descendant of an internal data-migration tool. **It
must not leak the real domain.** Do not reintroduce vendor names, the rich
accounting model (vouchers, KID, VAT, dimensions, journal lines, budgets,
assets, products, etc.), or any proprietary API shapes. Keep to the generic
"Source → Target migration" story described here and nothing more.

The app is a teaching vehicle, so its value is in the **patterns**, not in
breadth. Generate exactly the structure below — resist the urge to add stages,
value objects, or abstractions that aren't specified.

---

## 1. What the app does

The app migrates a client (identified by an organization number) from a
**Source** system to a **Target** system. Both Source and Target are
**simulated** — they never make real network calls. They can be **configured to
fail** (transiently or permanently) so the workshop can demonstrate **retry**
and **error handling** live.

### 1.1 The flow

```
                 ┌─ Lock Source ─────────┐
Start  ─────────►├─ Trigger SAF-T export ─┤ (fan-in)
                 └─ Lock Target ─────────┘
      │
      ▼
Transform Source → Target   (cancel automatically if client data is invalid)
      │
      ▼
Upload pre-SAF-T work
      │
      ▼
Upload SAF-T file
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

Two fan-out/fan-in points are deliberate — they are the main architectural
lesson:

- **Setup fan-out** (`Lock Source`, `Trigger SAF-T export`, `Lock Target`) — three
  independent, individually-retryable steps. When all three have completed, the
  migration fans in and proceeds to `Transform`.
- **Teardown fan-out** (`Unlock Source`, `Unlock Target`) — two independent steps.
  When both complete, the migration fans in and notifies completion.

The retryable steps (both locks, the export trigger, both unlocks) demonstrate
**retry with a bounded attempt counter** against the simulated systems. The
`Transform` step demonstrates **validation-driven automatic cancellation**.

### 1.2 The one and only domain rule

The domain is intentionally trivial. **Pre-SAF-T work is nothing more than
checking that the client has an address.** In `Transform`:

- Fetch the client from Source.
- If the client has **no address**, the data is invalid → **automatically
  cancel** the migration (which unlocks Source and Target, then notifies).
- Otherwise continue to `Upload pre-SAF-T work`.

Do **not** model anything else about the client. Do **not** reintroduce the
`Permissive` pattern for the address — that pattern is explicitly abandoned (see
§10). A client either has an `Address` (a simple value object) or it does not.

### 1.3 Manual approval

After `Upload SAF-T file`, the migration sits in a terminal-until-approved state
(`SaftUploaded`). Nothing proceeds until a user calls `POST /Approve`, which
begins teardown. This mirrors the real system's human gate and gives the
workshop a natural pause point.

### 1.4 Cancellation

A migration can be cancelled two ways:

1. **Manually** via `POST /Cancel` while it is in a cancellable state.
2. **Automatically** by `Transform` when client data is invalid.

Either way the migration transitions to `Cancelling`, releases whatever was
locked (Source and/or Target), transitions to `Cancelled`, and notifies
completion. Once `Cancelled`, a new migration for the same organization number
may be started.

---

## 2. Architecture

**Hexagonal architecture (ports & adapters) with an event-driven core**, plus
the **transactional outbox** and **inbox** patterns.

- **Domain** is pure. It depends only on the result library. It defines **ports**
  (interfaces) and contains all business logic in **stage handlers**.
- **Adapters** implement ports: persistence (Postgres/EF Core), the simulated
  Source, the simulated Target, the message transport.
- **Entry points**: an API that writes to the outbox, an outbox worker that
  publishes messages, and a processor that consumes messages and dispatches to
  domain handlers through the inbox.

### 2.1 Message-driven state machine

Every stage transition is driven by a **domain event message**. A handler does
its work, mutates state, and **atomically writes the next event to the outbox**
in the same database transaction as the state change (transactional outbox). The
outbox worker publishes pending messages to a transport (at-least-once). The
processor receives each message and runs it through the **inbox** for
idempotency before dispatching to the matching handler.

### 2.2 Message envelope & idempotency (IMPORTANT — differs from legacy design)

A message carries:

| Field         | Meaning                                                        |
|---------------|----------------------------------------------------------------|
| `MigrationId` | Which migration this event is for                              |
| `DomainEvent` | Which stage/event (e.g. `SourceLockRequested`)                 |
| `Attempt`     | The stage's attempt counter for this migration+event           |
| `Payload`     | JSON string (usually `"{}"`)                                    |
| `TraceParent` | W3C trace context for correlation                              |

**Duplicate rule:** two messages are the **same** iff their `(MigrationId,
DomainEvent, Attempt)` are all equal. The **Inbox** primary key is exactly this
composite `(MigrationId, DomainEvent, Attempt)`.

Consequences (make sure the implementation honors these):

- **At-least-once redelivery** of the *same* attempt is deduped by the inbox — the
  handler runs at most once per `(MigrationId, DomainEvent, Attempt)`.
- **A retry** is a *new* attempt: when a handler fails a retryable step and
  reschedules, it emits the event again with `Attempt + 1`, producing a new
  dedup key that is *not* deduped.
- The attempt counter therefore lives with the migration/stage (persisted,
  incremented per try) and is stamped onto the outgoing message.

There is **no separate `MessageId`**. Do not reintroduce one. This is a
deliberate simplification and teaching point versus the legacy system.

---

## 3. Solution structure

Generate a single .NET solution. Suggested name `Workshop.sln`. Keep the `Mt.`
namespace/prefix (it reads as "migration tool" and carries no proprietary
meaning). One project per bullet:

**Core**
- `Mt.Results` — the result pattern library. **No dependencies.**
- `Mt.Domain` — business logic, ports, aggregate, value objects, stage handlers.
  Depends only on `Mt.Results` (and `Microsoft.Extensions.Logging.Abstractions`).

**Adapters**
- `Mt.Persistence` — EF Core 10 + Npgsql. Migration repository, outbox, inbox,
  scheduler. Implements domain persistence ports.
- `Mt.Source` — **simulated** Source system. Implements Source ports. Reads
  failure configuration.
- `Mt.Target` — **simulated** Target system. Implements Target ports. Reads
  failure configuration.
- `Mt.Transport` — message transport abstraction + a local implementation
  (see §8.3). Implements the publish/consume ports.

**Entry points**
- `Mt.Api` — minimal-API host. `Start`, `Approve`, `Cancel` endpoints. Writes to
  the outbox transactionally.
- `Mt.Outbox` — worker (`BackgroundService`) that polls the outbox and publishes.
- `Mt.Processor` — worker (`BackgroundService`) that consumes messages, runs the
  inbox, and dispatches to handlers.

**Tests** (one per non-trivial project)
- `Mt.Results.Tests`, `Mt.Domain.Tests`, `Mt.Persistence.Tests`,
  `Mt.Source.Tests`, `Mt.Target.Tests`.

> For local runnability the three entry-point workers may also be composed into
> one host if desired, but keep them as separate projects so the boundaries are
> visible. See §8.3 for the recommended local transport.

Every project: `net10.0`, `Nullable` enabled, `ImplicitUsings` enabled,
`TreatWarningsAsErrors` enabled, C# 13.

---

## 4. Coding guidelines (author these into `README.md`)

Generate a `README.md` whose "Coding Guidelines" section states the following.
These are the rules Claude must also *follow* while generating the app.

### 4.1 Vertical slices
Organize by **feature, not technical layer**. A stage lives in one folder with
its handler, its event, its ports, and its settings together.
```
Stages/
  LocksSource/
    Handler.cs
    DomainEvent.cs
    ILockSource.cs
    Settings.cs
```
Not `Handlers/`, `Interfaces/`, `Settings/` split by kind.

### 4.2 Short names in namespaces
No redundant prefixes. In `namespace Mt.Domain.Stages.LocksSource` the class is
`Handler`, not `LockSourceHandler`; the request record is `Command`, not
`LockSourceCommand`.

### 4.3 Duplicate before you abstract
**Prefer duplication over premature reuse.** Only extract shared code once the
same thing appears **four times**. The two lock slices and the two unlock slices
are intentionally near-duplicates — leave them duplicated. This is a core
workshop lesson; do not "DRY" them up.

### 4.4 Result<T> everywhere, no exceptions for control flow
All fallible operations return `Result<T>` (see §5). Chain with
`Then`/`ThenAsync`. Propagate failures by chaining, not by manual `if
(x.IsFailed(...)) return ...` unless the value is needed later on its own line.

- Inline single-use intermediates into the chain.
- **But** an async producer (`Task<Result<T>>`) binds to a **named variable
  first** — never `await (await X).ThenAsync(...)`.
```csharp
var recordedAttempt = await recordAttempt.HandleAsync(migrationId, ct);
var locked = await recordedAttempt
    .ThenAsync(_ => lockSource.HandleAsync(migrationId, ct));
```

### 4.5 No `null` in the domain; value objects, not primitives
`Mt.Domain` public APIs take **value objects**, never bare primitives. Model
optionality with subtypes/inheritance, not `null`. Validate **once** in a static
`Create` returning `Result<T>`; methods then assume validity and never
re-validate parameters.
```csharp
public static Result<Address> Create(string line, string city) => ...;
```

### 4.6 No mutation of input parameters
Methods return new data; they never mutate arguments. No `void Collect(...,
List<Failure> sink)` accumulator style.

### 4.7 Manual mapping
No AutoMapper. Hand-written extension methods named `To<Target>()`
(`request.ToCommand()`, `row.ToDomain()`). Use `required` members so the compiler
catches missing mappings.

### 4.8 C# 13 extension members
Use the C# 13 `extension(...)` block syntax for extension methods, not the old
`this`-parameter form.
```csharp
public static class Extensions
{
    extension(DomainEvent e)
    {
        public string ToMessageType() => e.ToString();
    }
}
```

### 4.9 Persistence throws on corrupt data
Domain reconstruction inside `Mt.Persistence` throws (e.g.
`IllegalValuesInDbException`) when a `Create` fails — corrupt rows are bugs, not
recoverable results.

### 4.10 Logging
Serilog-style structured logging. Levels: Critical (needs attention) / Error
(failed, unrecoverable) / Warning (unexpected but handled) / Information (normal
milestone) / Debug (dev diagnostics). Use named `EventId`s for important events.

### 4.11 The inner loop
After every meaningful change: **build** (warnings are errors), **run the
relevant tests**, then **self-review** the diff for the rules above and remove
anything not asked for.

---

## 5. `Mt.Results` — the result library

Implement a small, clean functional result type. **Include only what is listed
here.** The legacy library accreted domain leakage and dead ends — do not port
them (§10 lists exact exclusions).

### 5.1 Core type — `Result.cs`
- `IResult<out T>` with nested `IFailed { IReadOnlyList<Failure> Failures }` and
  `ICompleted { T Value }`.
- `abstract record Result<T>` carrying `IReadOnlyList<Failure> Warnings`, with
  nested `record Completed(T Value)` and `record Failed(IEnumerable<Failure>)`.
- Implicit conversions: `T -> Completed`, `Failure -> Failed`, `Failure[] ->
  Failed`.
- `WithWarnings(...)` that concatenates warnings and flows them through chains.
- `ToResult<T>()` helpers.

### 5.2 Combinators (one file each)
- **`IsFailed.cs`** — `IsFailed(out T? value, out Failure[]? failures)` and the
  `IsCompleted` mirror, with the right `[NotNullWhen]` annotations.
- **`Then.cs`** — `Then` (sync) and `ThenAsync` returning `AsyncResult<T>`;
  overloads for `Func<T,TRes>`, `Func<T,Result<TRes>>`, `Func<T,IResult<TRes>>`,
  `Func<T,Task<Result<TRes>>>`. `ThenAsync` uses `ConfigureAwait(false)`.
- **`AsyncResult.cs`** — an awaitable `AsyncResult<T>` wrapper over
  `Task<Result<T>>` so chains read fluently and can be `await`ed directly.
- **`Map.cs`** — `Map` to transform a success value (`Func<T,TRes>`).
- **`Tap.cs`** — run a side effect on success, return the result unchanged. (This
  is the only side-effect combinator. Do **not** add `Do`.)
- **`Flatten.cs`** — unwrap `Result<Result<T>>`.
- **`FailWhen.cs` + `Validator.cs`** — a minimal fluent validator:
  `value.FailWhen(predicate, "message")` chaining into `.Then(Create)`. Support
  only the `FailWhen` form. Do **not** port the `EmptyWhen*` variants.
- **`Ensure` helpers** — `EnsureNotNull`, `EnsureFound` (null → `NotFoundFailure`),
  `EnsureNoDuplicates`. One small file (`Ensure.cs`) is fine.
- **`ResultUtilities.cs`** — `WrapExceptions` / `WrapExceptionsAsync` that catch
  and turn exceptions into `ExceptionalFailure`.

### 5.3 Failures (`Failures/`)
Include exactly:
- `Failure` (abstract record; `string Message`, `Severity Severity`).
- `Severity` (enum).
- `ValidationFailure`, `NotFoundFailure`, `ExceptionalFailure`,
  `ConditionalFailure`, `DuplicateFailure`, `UnexpectedNullFailure`,
  `OutOfRetriesFailure`.

### 5.4 Explicitly NOT in `Mt.Results` (see §10 for the full list)
`Permissive`, `MigrationCancellingFailure`, `Do`, `SafeFirst`, `SafeGet`,
`SafeSingle`, `SafeParse`, `Curryable`/`Apply` (currying), LINQ
`Select`/`SelectMany` query-syntax support, and the match-failure types
(`NoMatchFailure`, `MoreThanOneMatchFailure`, `NoElementMatchedKeyFailure`).

---

## 6. `Mt.Domain`

### 6.1 Value objects
Keep the set tiny:
- `Migrations.Id` — wraps a positive `long`.
- `OrganizationNumber` — non-empty string (a simple format check is fine; do
  **not** port the Norwegian Mod-11 checksum or the `Unchecked`/`Norwegian`
  hierarchy).
- `Address` — a **plain** value object (e.g. `Line` + `City`, both non-empty via
  `Create`). No `Permissive`. This is the pre-SAF-T validation target.
- `Client` — carries an optional `Address`, modeled with subtypes (e.g.
  `Client.WithAddress` / `Client.WithoutAddress`) rather than a nullable
  property.
- `Attempt` — wraps a non-negative `int`; `Attempt.First`, `.Next()`.
- `SaftFile` — a filename + bytes (opaque; contents don't matter).

### 6.2 The aggregate — `Migrations/Migration.cs`
An abstract `Migration(Id id)` with sealed subclasses as a discriminated union of
states. Each transition is a method returning `Result<NextState>`; illegal
transitions return a failure (`MigrationHasIncorrectState`). Do not use `null`.

States:

| State           | Fan-in flags                                 | Transition method(s)                    |
|-----------------|----------------------------------------------|-----------------------------------------|
| `Created`       | `SourceLocked`, `TargetLocked`, `ExportTriggered` | `IsReadyToTransform`; `Transform()` → `Transformed` |
| `Transformed`   | —                                            | `UploadPreSaft()` → `PreSaftUploaded`   |
| `PreSaftUploaded`| —                                           | `UploadSaft()` → `SaftUploaded`         |
| `SaftUploaded`  | — (awaits approval)                          | `Approve()` → `Unlocking`               |
| `Unlocking`     | `SourceUnlocked`, `TargetUnlocked`           | `IsFullyUnlocked`; `Complete()` → `Completed` |
| `Completed`     | terminal                                     | —                                       |
| `Cancelling`    | `SourceUnlocked`, `TargetUnlocked`           | `IsFullyUnlocked`; `FinalizeCancellation()` → `Cancelled` |
| `Cancelled`     | terminal                                     | —                                       |

Cancellable states: `Created`, `Transformed`, `PreSaftUploaded`, `SaftUploaded`
implement `ICancellable` with `Cancel()` → `Cancelling`. When entering
`Cancelling`, initialize each unlock flag to `true` when that system was never
locked (nothing to release) — mirror the way `Created.Cancel()` seeds flags from
what actually happened.

### 6.3 Ports (interfaces the adapters implement)

**Persistence**
- `Migrations.IFetch` — fetch by id → `Result<Migration>`.
- Setter ports per fan-in flag, e.g. `LocksSource.ISetSourceLocked`,
  `TriggersExport.ISetExportTriggered`, `UnlocksSource.ISetSourceUnlocked`, etc.
  (small, single-purpose — vertical-slice friendly).
- Attempt counters per retryable stage, e.g.
  `LocksSource.IIncrementAttempts` → `Result<Attempt>`.
- `Outboxes.IAdd` — add the next event to the outbox (same tx).
- `Commands.Starts.IStart`, `Commands.Approvals.IApprove`,
  `Commands.Cancellations.ICancel` — the transactional command ports the API
  calls (create/transition + outbox write in one tx).

**Source (simulated)**
- `ILockSource`, `IUnlockSource`, `ITriggerExport`, `IFetchClient` (returns the
  `Client`, with or without address), `IDownloadSaft` (returns the exported
  `SaftFile`).

**Target (simulated)**
- `ILockTarget`, `IUnlockTarget`, `IUploadPreSaft`, `IUploadSaft`.

**Cross-cutting**
- `Events.IScheduleEvent` — reschedule a `(DomainEvent, MigrationId, Attempt,
  ScheduledAt)` for a bounded retry.
- `NotifyCompletions.INotifyCompletion` — final "done" notification (log it).

Each retryable adapter call returns a `Result<...>` whose failure the handler
inspects to decide "retry vs give up".

### 6.4 Domain events & dispatch
- `Stages/DomainEvent.cs` — abstract record base; each slice declares its own
  event subtype (e.g. `SourceLockRequested`) with a stable `ToString()`.
- `DomainEvent.FromString(string)` returns `Result<DomainEvent>` — look the name
  up in the known set and return a `NotFoundFailure` for unknown names. **Do not
  use `Single()`** (a legacy bug); use a safe lookup.
- `Stages/IHandleDomainEvent.cs`:
  ```csharp
  public interface IHandleDomainEvent
  {
      DomainEvent EventType { get; }
      Task<Result<ValueTuple>> HandleAsync(Migrations.Id migrationId, Attempt attempt, CancellationToken ct);
  }
  ```
  (Pass `Attempt` through so retries can increment it.)

### 6.5 Stage handlers (one vertical slice each)

**Retryable pattern** (used by `LocksSource`, `LocksTarget`, `TriggersExport`,
`UnlocksSource`, `UnlocksTarget`) — each is its own slice, intentionally
duplicated:
1. Fetch migration; ensure correct state; if already cancelled, log a warning and
   return success (no-op).
2. If the stage's flag is already set, short-circuit success (idempotent guard
   for the redelivery-with-new-attempt window).
3. Call the simulated adapter.
4. **On success:** set the flag, then run the **fan-in check** — refetch, and if
   all sibling flags are now set, emit the next event to the outbox.
5. **On failure:** if `attempt < MaxAttempts`, `IScheduleEvent` the same event
   with `attempt.Next()`; else return `OutOfRetriesFailure`.

Fan-in emission targets:
- All of `SourceLocked && TargetLocked && ExportTriggered` set → emit
  `TransformRequested`.
- Both `SourceUnlocked && TargetUnlocked` set → transition to
  `Completed`/`Cancelled` and call `INotifyCompletion`.

**`Transforms`** (`TransformRequested`):
1. Fetch migration (`Created`, ready), ensure not cancelled.
2. `IFetchClient`. If `Client.WithoutAddress` → **cancel**: transition to
   `Cancelling` and emit the unlock events (`SourceUnlockRequested`,
   `TargetUnlockRequested`). Log a clear warning.
3. Else transition `Created → Transformed`, emit `PreSaftUploadRequested`.

**`UploadsPreSaft`** (`PreSaftUploadRequested`): `IUploadPreSaft`; `Transformed →
PreSaftUploaded`; emit `SaftUploadRequested`.

**`UploadsSaft`** (`SaftUploadRequested`): `IDownloadSaft` from Source then
`IUploadSaft` to Target; `PreSaftUploaded → SaftUploaded`. **Emit nothing** —
this is the manual-approval gate.

**Teardown** is driven by the two unlock slices above; the second one to finish
fans in to `Completed`/`Cancelled` + notify.

Handlers that run mid-flight (`Transforms`, `UploadsPreSaft`, `UploadsSaft`)
check "not cancelled" and skip gracefully with a warning if the migration was
cancelled concurrently.

---

## 7. Simulated Source & Target (`Mt.Source`, `Mt.Target`)

These implement the Source/Target ports **without any real I/O**. A call:
1. Reads its per-operation failure config.
2. Decides success/failure (see below).
3. Logs what it "did" (e.g. "🔒 [SIM] Source locked for {OrganizationNumber}").
4. Returns `Result<...>` — a configured failure returns a `ConditionalFailure`
   (or `ExceptionalFailure` if simulating a thrown error).

### 7.1 Failure configuration
Bind from `appsettings.json` per system, per operation. Support two modes:

- **`FailUntilAttempt: N`** — the operation fails while `attempt < N`, then
  succeeds. This demonstrates **transient failure + successful retry**.
- **`AlwaysFail: true`** — the operation always fails. This demonstrates
  **retry exhaustion → `OutOfRetriesFailure` → error handling**.

Example:
```json
{
  "Source": {
    "Lock":         { "FailUntilAttempt": 2 },
    "TriggerExport":{ "AlwaysFail": false },
    "Unlock":       {}
  },
  "Target": {
    "Lock":   {},
    "Unlock": { "AlwaysFail": true }
  },
  "Client": { "HasAddress": true }
}
```
- To demo the **auto-cancel-on-invalid-data** path, set `Client.HasAddress:
  false` so `IFetchClient` returns a `Client.WithoutAddress`.
- The simulated adapter receives the current `Attempt` (from the handler) so
  `FailUntilAttempt` can compare against it.

Keep the simulation dumb and stateless — behavior is a pure function of config +
attempt. No hidden counters.

---

## 8. Messaging: outbox, transport, inbox

### 8.1 Outbox (transactional)
- Table `Outbox`: `Id` (PK), `MigrationId`, `DomainEvent`, `Attempt`, `Payload`,
  `TraceParent`, `CreatedAt`, `ProcessedAt?`, `FailedAt?`, `FailureReason?`.
- Every command/handler writes its next event **in the same EF transaction** as
  the state change. `Outboxes.IAdd` just adds the row; the surrounding unit of
  work commits.

### 8.2 Outbox worker (`Mt.Outbox`)
- `BackgroundService` polling for unprocessed rows in batches.
- Serializes each row to the envelope (§2.2) — including `TraceParent` — and
  publishes via the transport port.
- On success marks rows processed; on failure records `FailedAt`/`FailureReason`.
- **At-least-once**: a crash between publish and mark-processed re-publishes. This
  is *why* the inbox exists — keep the behavior, don't paper over it.

### 8.3 Transport (`Mt.Transport`) — two interchangeable local implementations
Define ports `IMessagePublisher` (used by `Mt.Outbox`) and `IMessageConsumer`
(used by `Mt.Processor`). For a self-contained workshop **avoid AWS**; ship
**both** of the following local transports behind those ports, selectable via
config (e.g. `"Transport": { "Kind": "InMemory" | "Postgres" }`). Having both is
itself a teaching point — the domain, outbox, and inbox code is identical
regardless of which transport is wired in.

- **`InMemory`** — an in-process `System.Threading.Channels`-backed bus. Runs the
  whole app in one host with just Postgres + `dotnet run`; the fastest way to see
  the flow in a single log stream. This is the **default** for the workshop.
- **`Postgres`** — a `Messages` polling table (`Id`, envelope columns,
  `PublishedAt`, `ConsumedAt?`). `Mt.Outbox` inserts rows; `Mt.Processor` polls,
  claims, and consumes them. This lets `Mt.Api`, `Mt.Outbox`, and `Mt.Processor`
  run as **separate processes**, which shows the real EDA boundaries.

Both transports must expose a configurable **redelivery probability** (e.g.
deliver some messages twice) so the inbox dedup is observably exercised during
the workshop. Keep the ports clean so a real broker could be dropped in later as
a third implementation.

### 8.4 Inbox (idempotency) — `Mt.Processor` + `Mt.Persistence`
- Table `Inbox`: composite PK `(MigrationId, DomainEvent, Attempt)`, plus
  `ReceivedAt`.
- `ExecuteOnce` flow, all in one DB transaction:
  1. Begin transaction.
  2. If a row with this `(MigrationId, DomainEvent, Attempt)` exists → log
     "duplicate, skipping", rollback, return success.
  3. Insert the inbox row; `SaveChanges`.
  4. Look up the handler by `EventType` **safely** (not `Single()` — return a
     failure if zero or many match) and run it.
  5. On handler failure → rollback → return failures.
  6. On success → `SaveChanges` → commit.
  7. Catch the unique-violation race (concurrent duplicate) → treat as
     already-processed → rollback → success.

### 8.5 Processor (`Mt.Processor`)
- `BackgroundService` consuming envelopes from `IMessageConsumer`.
- Parses the envelope, rebuilds `(Id, DomainEvent, Attempt)` as `Result<...>`,
  restores the trace context from `TraceParent`, and calls `ExecuteOnce`.
- Logs the outcome. A failed message surfaces the failures (log + non-fatal;
  don't crash the loop).

---

## 9. `Mt.Api`

Minimal API host. JSON camelCase. Endpoints map a request DTO → domain command →
`Result` → HTTP result via a shared `ToHttpResult()` extension
(Completed→200/201, `NotFoundFailure`→404, `ConditionalFailure`/incorrect-state
→409, `ValidationFailure`→400, else→500).

- `POST /Migration/{organizationNumber}/Start`
  Body: `{ }` is enough, or a tiny DTO — keep only fields the flow needs (an
  organization number from the route; add a `workItemId`-style field only if you
  actually use it — prefer not to). Creates the migration in `Created` and
  **atomically** writes the three setup events (`SourceLockRequested`,
  `TargetLockRequested`, `ExportRequested`) — each at `Attempt.First` — to the
  outbox in one transaction.
- `POST /Migration/{organizationNumber}/Approve`
  Requires `SaftUploaded`. Transitions to `Unlocking` and writes
  `SourceUnlockRequested` + `TargetUnlockRequested` (at `Attempt.First`) to the
  outbox in one transaction. 404 if unknown, 409 if not in `SaftUploaded`.
- `POST /Migration/{organizationNumber}/Cancel`
  Requires an `ICancellable` state. Transitions to `Cancelling` and writes the
  unlock events for whatever was locked, in one transaction. 404 if unknown, 409
  if not cancellable.

The API never calls Source/Target directly — it only writes to the outbox.

---

## 10. Explicit exclusions (do NOT generate these)

These are either domain leaks or abandoned ideas. Their absence is intentional
and is itself part of the workshop.

**From the result library:**
- `Permissive<T>` and everything that consumes it (the "degraded value"
  validation-accumulation pattern). **Abandoned — regretted.** The address check
  is a plain `Create` returning `Result<Address>`.
- `MigrationCancellingFailure` — a domain concept that leaked into the generic
  result library.
- `Do.cs` — abandoned side-effect combinator (keep only `Tap`).
- `SafeFirst`, `SafeGet`, `SafeSingle`, `SafeParse` — abandoned LINQ-over-Result
  helpers.
- `Curryable` / currying `Apply`, and LINQ `Select`/`SelectMany` query-syntax
  support — out of scope; `Then`/`ThenAsync` chaining is enough.
- Match-failure types (`NoMatchFailure`, `MoreThanOneMatchFailure`,
  `NoElementMatchedKeyFailure`) and `ParseFailure` — supported the removed
  helpers.

**From the domain:**
- All vendor/product names and the rich accounting model: vouchers, KID, VAT,
  dimensions/dimension types, journal entry lines, budgets, assets, products,
  customers, suppliers, number series, PostSAF-T, hand-picked exports, reportables,
  checkpoints/rollbacks, feature flags. Keep only the states, ports, value
  objects, and stages listed in §6.
- The Norwegian org-number checksum and `Unchecked`/`Norwegian` hierarchy — a
  plain non-empty check.
- `MessageId`-based inbox dedup — replaced by `(MigrationId, DomainEvent,
  Attempt)`.
- AWS specifics (EventBridge, SQS, Batch, Secrets Manager, S3) — replaced by the
  local transport and plain config/files.

**Legacy bugs — do not reproduce:**
- `Single()` for handler lookup or `DomainEvent.FromString` (return a failure
  instead of throwing).
- A `Finalize`/`Complete` handler that mutates external state before persisting
  the terminal state (persist the state transition and the notification within
  the same unit of work).

---

## 11. Persistence & data model (`Mt.Persistence`)

EF Core 10 + Npgsql. Postgres for local dev (host `localhost`, a `workshop`
database; document the connection string in the README). Tables:

- **Migrations** — `Id` (PK), `State` (enum int), `OrganizationNumber`, and the
  fan-in flags (`SourceLocked`, `TargetLocked`, `ExportTriggered`,
  `SourceUnlocked`, `TargetUnlocked`) + per-stage attempt counters. Unique index
  on `OrganizationNumber` filtered to exclude `Cancelled`/`Completed` so an org
  can be re-migrated after it finishes.
- **Outbox** — as §8.1.
- **Inbox** — composite PK `(MigrationId, DomainEvent, Attempt)` + `ReceivedAt`.
- **ScheduledEvents** — for `IScheduleEvent` retries: `(MigrationId, DomainEvent,
  Attempt, Payload, ScheduledAt, ProcessedAt?)`; the outbox worker (or a small
  scheduler poll) promotes due rows into the outbox.
- **Messages** — only used by the `Postgres` transport (§8.3): `Id`, the envelope
  columns, `PublishedAt`, `ConsumedAt?`. Absent/unused under the `InMemory`
  transport.

Reconstruction of domain objects from rows **throws** `IllegalValuesInDbException`
on invalid data (§4.9). Persistence adapters are organized as vertical slices
(e.g. `Outboxes/Adds/Add.cs`, `Migrations/Fetches/Fetch.cs`).

Use Testcontainers.PostgreSQL for persistence/integration tests.

---

## 12. Testing

- **xUnit + Moq.** Domain handler tests mock every port and assert each failure
  path in isolation (adapter fails → retry scheduled with `attempt+1`; retries
  exhausted → `OutOfRetriesFailure`; already-cancelled → graceful skip; fan-in
  emits the next event only when the last sibling flag flips).
- **Simulation tests** verify `FailUntilAttempt` and `AlwaysFail` behavior.
- **Inbox tests** (Testcontainers) verify a duplicate `(MigrationId, DomainEvent,
  Attempt)` is skipped and a concurrent duplicate loses the unique-violation race
  gracefully.
- **`Mt.Results.Tests`** cover the combinators and warning propagation.

Cover at least one **end-to-end happy path** and one **auto-cancel path** (client
without address) and one **retry-then-succeed path** (`FailUntilAttempt`).

---

## 13. Local run & workshop demo

Document in the README:
1. `docker compose up` for Postgres (provide a minimal `docker-compose.yml`).
2. Apply EF migrations.
3. Choose a transport (§8.3): `Transport.Kind = "InMemory"` (default — run the
   combined host in one process) or `"Postgres"` (run `Mt.Api`, `Mt.Outbox`, and
   `Mt.Processor` as three separate processes). Show both in the workshop.
4. `POST /Migration/{org}/Start`, watch the logs fan out, then `POST /Approve`.
5. Flip `Source.Lock.FailUntilAttempt` to watch retries; flip `Target.Unlock.
   AlwaysFail` to watch retry exhaustion + error handling; flip
   `Client.HasAddress:false` to watch auto-cancellation.

Every log line should make the current stage, attempt, and fan-in progress
obvious — the logs are the workshop's narration.

---

## 14. Build order (suggested)

1. `Mt.Results` (+ tests) — the foundation.
2. `Mt.Domain` value objects, aggregate, ports, domain events.
3. `Mt.Domain` stage handlers (+ tests with mocked ports).
4. `Mt.Persistence` (migrations, outbox, inbox, scheduler; + Testcontainers tests).
5. `Mt.Source` / `Mt.Target` simulations (+ tests).
6. `Mt.Transport` local bus.
7. `Mt.Api`, `Mt.Outbox`, `Mt.Processor`.
8. README (coding guidelines from §4 + run instructions) and `docker-compose.yml`.

Run the inner loop (§4.11) after each step. The app is done when the happy path,
the auto-cancel path, and the retry paths all run and are covered by tests.
