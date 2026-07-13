# Spec 9 — `Prepared`, Fetch Adapters Move to Persistence, Fan-In Without Refetch

## 0. Context

Three connected changes, agreed in design discussion:

1. **New `Prepared` state.** `Transforms` currently admits any `Created` and re-checks
   `IsReadyToTransform` in the handler, failing with "ready but setup is incomplete" — a
   wrong-state call the port should own. Rather than moving the check, the state machine gains
   `Prepared` (`Created` whose three setup flags are all set): the fan-in transitions into it,
   `Transforms` admits only it, and the readiness check disappears from the handler, the port,
   *and* `Created.Transform()` — the illegal state becomes unrepresentable.
2. **Per-slice fetch adapters move to `Mt.Persistence`.** The `Stages.*.FetchMigration`
   classes are the only non-model, non-port classes in `Mt.Domain`. Spec 4 D1 kept them there
   because "state admission is domain policy" — but after spec 5 (and with `Prepared`
   absorbing readiness) every guard is a pure state→response *mapping*, the same kind of work
   `Migrations/Mapping.cs` already does in persistence. Each slice gets
   `Mt.Persistence.Stages.<Slice>.FetchMigration : Mt.Domain.Stages.<Slice>.IFetchMigration`
   loading the row directly; the shared `Migrations.IFetch`/`Fetch` pair is deleted.
3. **Fan-in stops refetching.** Deleting `Migrations.IFetch` takes away the fan-in refetch
   (spec 5 D4). Its replacement is better than a substitute port: the five flag-setting
   `ISet*` ports return what the write produced, so fan-in is answered by the same operation
   that set the flag — one round trip, and for setup the `Created → Prepared` transition
   happens atomically with the final flag write.

Supersedes: spec 4 D1, D2, D7 (and D6, already superseded once); spec 5 D4.

### Decisions made in this spec (flag to the reviewer if any feels wrong)

| # | Decision | Why |
|---|----------|-----|
| D1 | `Prepared` sits between `Created` and `Transformed`; `Transforms` admits only `Prepared` (supersedes 4-D7) | Readiness becomes state admission once it is a state. The handler's readiness failure and `Transform()`'s internal guard both disappear |
| D2 | `Created → Prepared` is performed by the setup `ISet*` persistence ops, guarded on the domain's `Created.IsReadyToTransform` — there is no `Prepare()` transition method | Nothing outside the three setup ops would ever call it, and its `Result` failure would be the *normal* "not last flag yet" flow — a failed `Result` as routine control flow reads wrong. Deviation from spec 1 §6.2 ("every transition is a method") recorded here; readiness itself stays defined on the domain type |
| D3 | `FetchMigration` implementations move to `Mt.Persistence.Stages.<Slice>` and compose `LoadMigrationAsync` + `ToDomain()` directly; `Migrations.IFetch` and `Migrations/Fetch.cs` are deleted (supersedes 4-D1, 4-D2) | The guards are state→response mapping, which is adapter work by this codebase's own conventions. Domain slices keep only ports and the handler; port-to-adapter with no intermediary hop |
| D4 | The five flag-set ports (`ISetSourceLocked`, `ISetTargetLocked`, `ISetExportTriggered`, `ISetSourceUnlocked`, `ISetTargetUnlocked`) return a nested `Response` union; all other `ISet*` ports keep `Result<ValueTuple>` (supersedes 5-D4) | Only fan-in needs the post-write state. 5-D4's objection ("the guard port answers `DoNotProceed` once the flag is set") does not apply: the *setter* reports the state it just produced |
| D5 | Setup responses are `SetupComplete` / `SetupIncomplete`; unlock responses are `Complete(Unlocking)` / `Cancel(Cancelling)` / `TeardownIncomplete` | Like `Proceed`/`DoNotProceed` (5-D1), they read as the answer to the handler's one question. The unlock cases carry the typed state because finalization (`Complete()`/`FinalizeCancellation()`, notify, external-id release) stays in the handler — it orchestrates external ports the setter must not know |
| D6 | `MigrationState` is renumbered in lifecycle order (`Prepared = 1`, everything after shifts) | Not in production — no persisted rows to keep faithful (decision 2026-07-14). No schema change: `Prepared` needs no new columns (its flag columns are all `true` by construction) |
| D7 | `FetchMigrationTests` moves to `Mt.Persistence.Tests` (Postgres-backed, seeded rows) | The logic it covers moves projects. Cost accepted: with `Prepared`, the switches are trivial type matches, and the persistence harness already exists |
| D8 | Persistence `Stages/` reorganizes into per-slice folders; the per-slice `IFetchMigration` registrations move from `Mt.Processor/StageHandlers.cs` into `Mt.Persistence/Registration.cs` | The ≥2-files rule forbids a folder holding only `FetchMigration.cs`, so the flat `Set*` files join their slice — persistence ends up mirroring the domain's slice layout. Registrations live with the implementations, like every other persistence adapter |
| D9 | Wrong-state and skip messages, log levels, and `LogEvents` ids move verbatim (spec 4 D8 still applies), with one forced edit: `Transforms`' wrong-state text becomes "expected migration {id} to be **Prepared** but was {state}" | Greppability. The handler's "ready but setup is incomplete" failure is deleted outright — that situation is now the port's wrong-state arm (`Created` is a wrong state for `Transforms`) |

## 1. Domain — the `Prepared` state

In `Migrations/Migration.cs`:

```csharp
/// <summary>
/// Setup complete: all three fan-in flags were set. Entered by whichever setup stage
/// lands the last flag — the flag-set persistence op performs the transition (spec 9 D2),
/// so there is no Prepare() method here. Ready for Transform.
/// </summary>
public sealed record Prepared(Id Id, OrganizationNumber OrganizationNumber)
    : Migration(Id), ICancellable
{
    public Result<Transformed> Transform() => new Transformed(Id, OrganizationNumber);

    // Everything was locked on the way in, so both systems need unlocking.
    public Result<Cancelling> Cancel() =>
        new Cancelling(Id, OrganizationNumber, SourceUnlocked: false, TargetUnlocked: false);
}
```

- `Created` keeps `IsReadyToTransform` (the setup ops guard on it, §4) and `Cancel()`;
  its `Transform()` is **deleted** — `Created` can no longer transform.
- `Transform()` stays `Result`-typed despite being unconditional, like `UploadPreSaft()` /
  `UploadSaft()`, for uniform `Then` chaining.
- `Prepared : ICancellable` means the `Cancel` command works untouched.

## 2. Persistence — state and mapping

- `MigrationState` gains `Prepared = 8` (D6).
- `Migrations/Mapping.cs` gains `MigrationState.Prepared => new Prepared(id, organizationNumber)`.
- No `MigrationRow` change, no EF migration needed (int column, existing values keep meaning).

## 3. Fetch adapters move to persistence (D3, D8)

Each `src/Mt.Domain/Stages/<Slice>/FetchMigration.cs` moves to
`src/Mt.Persistence/Stages/<Slice>/FetchMigration.cs`, rewritten to load the row itself —
the `ToResponse` switch, `DoNotProceed` logging, and nested `Log` class move verbatim (D9):

```csharp
public sealed partial class FetchMigration(WorkshopDbContext db, ILogger<FetchMigration> logger)
    : IFetchMigration
{
    public async Task<Result<IFetchMigration.Response>> HandleAsync(Id migrationId, CancellationToken ct)
    {
        var loaded = await db.LoadMigrationAsync(migrationId.Value, ct);
        return loaded.Then(row => ToResponse(migrationId, row.ToDomain()));
    }
    // ToResponse + Log: unchanged from the domain version.
}
```

`Transforms`' switch changes admission: `Prepared prepared => Proceed(prepared)`; `Created`
falls through to the wrong-state arm with the updated message (D9). The `Transforms` handler
then works on `Prepared` (`prepared.Transform()` / `prepared.Cancel()`) and its readiness
`if` block is deleted.

Deleted: `src/Mt.Domain/Migrations/IFetch.cs`, `src/Mt.Persistence/Migrations/Fetch.cs`, all
eight `src/Mt.Domain/Stages/*/FetchMigration.cs`.

Resulting persistence layout (every folder satisfies the ≥2-files rule):

| Folder | Files |
|---|---|
| `Stages/LocksSource` | `FetchMigration`, `SetSourceLocked` |
| `Stages/LocksTarget` | `FetchMigration`, `SetTargetLocked` |
| `Stages/TriggersExport` | `FetchMigration`, `SetExportTriggered` |
| `Stages/Transforms` | `FetchMigration`, `SetCancelling`, `SetTransformed` |
| `Stages/UploadsPreSaft` | `FetchMigration`, `SetPreSaftUploaded` |
| `Stages/UploadsSaft` | `FetchMigration`, `SetSaftUploaded` |
| `Stages/UnlocksSource` | `FetchMigration`, `SetSourceUnlocked` |
| `Stages/UnlocksTarget` | `FetchMigration`, `SetTargetUnlocked` |

`Mt.Domain/Migrations/` keeps ≥2 files after losing `IFetch.cs`; so does every domain slice
folder after losing `FetchMigration.cs`.

## 4. Flag-set ports return the outcome (D4, D5)

### Setup slices (`LocksSource`, `LocksTarget`, `TriggersExport`)

```csharp
public interface ISetSourceLocked
{
    Task<Result<Response>> HandleAsync(Id migrationId, CancellationToken ct);

    public abstract record Response
    {
        /// <summary>This flag was the last one; the migration is now Prepared.</summary>
        public sealed record SetupComplete : Response;

        /// <summary>Another setup stage is still pending (or the state has moved on).</summary>
        public sealed record SetupIncomplete : Response;
    }
}
```

The persistence op sets its flag and, when that completed setup, performs the transition in
the same tracked write:

```csharp
var loaded = await db.LoadMigrationAsync(migrationId.Value, ct);
return loaded.Map(row =>
{
    row.SourceLocked = true;
    if (row.ToDomain() is not Created { IsReadyToTransform: true })
    {
        return (ISetSourceLocked.Response)new ISetSourceLocked.Response.SetupIncomplete();
    }

    row.State = MigrationState.Prepared;
    return new ISetSourceLocked.Response.SetupComplete();
});
```

The handler's `FanInAsync` (and its `IFetch` dependency) is replaced by a match on the
response — `SetupComplete` → `Log.FanInAdvanced` + outbox `TransformRequested`, exactly the
existing log/event. Any non-`Created` state yields `SetupIncomplete`, preserving today's
quiet tolerance of the refetch's default arm.

Visibility is unchanged from the refetch it replaces: both run inside the same inbox
transaction that set the flag, so no new race is introduced (and the window shrinks —
flag write and transition are one statement apart instead of a query apart).

### Unlock slices (`UnlocksSource`, `UnlocksTarget`)

```csharp
public interface ISetSourceUnlocked
{
    Task<Result<Response>> HandleAsync(Id migrationId, CancellationToken ct);

    public abstract record Response
    {
        /// <summary>Both systems unlocked on the approve path; finalize to Completed.</summary>
        public sealed record Complete(Unlocking Migration) : Response;

        /// <summary>Both systems unlocked on the cancel path; finalize to Cancelled.</summary>
        public sealed record Cancel(Cancelling Migration) : Response;

        /// <summary>The other unlock is still pending (or the state has moved on).</summary>
        public sealed record TeardownIncomplete : Response;
    }
}
```

The op sets its flag, maps the row, and returns `Complete`/`Cancel` only when
`IsFullyUnlocked` — mirroring the handler's current `switch` arms. The handler keeps
`FinalizeCompletedAsync`/`FinalizeCancelledAsync` unchanged (domain transition, `Set*`,
notify, external-id release, logs) and switches on the response instead of a refetched
migration; `TeardownIncomplete` is the quiet `default`.

All five handlers drop their `IFetch` constructor dependency.

## 5. Registration

- `Mt.Persistence/Registration.cs`: remove `Migrations.IFetch → Migrations.Fetch`; add the
  eight per-slice `IFetchMigration → Stages.<Slice>.FetchMigration` registrations (scoped).
- `Mt.Processor/StageHandlers.cs`: remove the eight `FetchMigration` registrations and the
  "composing the shared `Migrations.IFetch`" comment; handlers and settings stay.

## 6. Tests

1. **Handler tests** (`Mt.Domain.Tests`): mock the `ISet*` response instead of `IFetch` —
   fan-in scenarios feed `SetupComplete` / `Complete(unlocking)` / `Cancel(cancelling)` /
   the incomplete cases. `TransformsHandlerTests` works on `Prepared`; its "Created but not
   ready is a failure" scenario is deleted (that is port behavior now).
2. **`FetchMigrationTests`** moves to `Mt.Persistence.Tests`: seed a row per state, assert
   the response case, including `Transforms`: `Prepared` → `Proceed`, `Created` →
   `MigrationHasIncorrectState`.
3. **New persistence tests**: setup op with last flag → row state `Prepared` +
   `SetupComplete`; earlier flag → `SetupIncomplete`; unlock op → `Complete` / `Cancel` /
   `TeardownIncomplete`.
4. **Domain tests**: `Prepared.Transform()` / `Prepared.Cancel()` (both unlocks seeded
   `false`); `Created` no longer exposes `Transform()`.

## 7. Verification / definition of done

1. `dotnet build` clean (warnings are errors); `dotnet test` green (Postgres suite needs
   `docker compose up`).
2. `grep -rn "class FetchMigration" src/Mt.Domain --include="*.cs"` → nothing; the classes
   exist only under `src/Mt.Persistence/Stages/`.
3. `src/Mt.Domain/Migrations/IFetch.cs` and `src/Mt.Persistence/Migrations/Fetch.cs` are gone;
   `grep -rn "Migrations.IFetch" src tests --include="*.cs"` → nothing.
4. `grep -rn "IsReadyToTransform" src --include="*.cs"` matches only `Migration.cs` and the
   three setup `Set*` ops.
5. Every namespace folder in `Mt.Domain` and `Mt.Persistence` still holds ≥2 files.
6. `CLAUDE.md` records: state admission mapping lives in per-slice persistence
   `FetchMigration` adapters; `Mt.Domain` contains only the model and ports.

## 8. Implementation notes (added during implementation, 2026-07-14)

- D6 was revised during review: `MigrationState` is renumbered in lifecycle order
  (`Prepared = 1`, later states shift) since nothing is in production. No EF migration
  needed — the state is a plain int column, and `Prepared` adds no columns.
- The `Mt.Results` `extension<T>` members do not accept explicit type arguments
  (`loaded.Map<Response>(…)` fails CS1061), so the flag-set ops cast one branch/arm to the
  response base type to steer lambda and switch-expression inference.
- The unlock handlers' `FanInAsync` no longer takes `migrationId` — the response carries the
  typed state. The setup handlers keep it for the outbox write and log line.
- The persistence `FetchMigration` copies drop the unused pattern variables the domain
  versions had (`case Created created:` → `case Created:`).
- README's state-machine table gained the `Prepared` row; `Created`'s transition column now
  points at the flag-write-performed transition.
- `SetFlagTests` uses `SetSourceLocked`/`SetSourceUnlocked` as stand-ins for their
  intentionally near-duplicated siblings rather than testing all five ops.
- The four non-Postgres suites pass (66 tests). `Mt.Persistence.Tests` (Testcontainers)
  could not run in the implementation environment (no docker socket) — run `dotnet test`
  with Docker available before merging; it includes the moved `FetchMigrationTests` (now
  15 scenarios incl. Transforms admission) and the new `SetFlagTests` (6 scenarios).

## 9. Out of scope

- The remaining `ISet*` ports (`SetTransformed`, `SetCancelling`, `SetPreSaftUploaded`,
  `SetSaftUploaded`, `SetCompleted`, `SetCancelled`) keep `Result<ValueTuple>` — nothing
  fans in on them.
- Commands, API, transport, outbox/inbox, `ExecuteOnce`, and retry semantics are untouched.
- The pre-existing theoretical blind spot where two setup stages commit simultaneously and
  neither sees the other's uncommitted flag is unchanged by this spec (the check runs in the
  same transaction as before); it is inherent to the current read-committed single-row design.
- The intentional near-duplication of the lock/unlock slices still applies (spec 1 §4.3) —
  the new per-slice persistence classes are duplicated the same way.
- No per-stage `Migration` types: the state subtypes plus per-slice responses already give
  each stage its view; fan-out stages legitimately share flag-carrying states (design
  discussion, 2026-07-14).
