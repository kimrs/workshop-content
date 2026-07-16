# Spec 5 — Fold Skip Checks into the Port, `DoNotProceed`, and Port Messages Nested in the Interface

## 0. Context

Review of spec 4's implementation produced three changes:

1. The "already done" checks (`created.SourceLocked`, `unlocking.SourceUnlocked`, …) move out
   of the handlers **into** the per-slice `FetchMigration` ports, folded into the skip case
   (supersedes spec 4 D5).
2. The skip case is renamed — it no longer means only "cancelled".
3. **New convention (standard):** a port's `Request`/`Response` records are declared *inside*
   the interface itself, never in their own file, so the type name says what it is a response
   from / request to (`IFetchMigration.Response`, `INotifyCompletion.Request`). Applies
   retroactively to `INotifyCompletion`.

### Decisions made in this spec (flag to the reviewer if any feels wrong)

| # | Decision | Why |
|---|----------|-----|
| D1 | The skip case is named **`DoNotProceed`** (candidates: `Cease`, `Abstain`, `DoNotProceed`) | It is the direct negation of `Proceed`, so the union reads as a single question ("may this stage run?") with no room for misreading at construction sites |
| D2 | `Proceed` carries **what the stage's work needs**, not necessarily the state: `OrganizationNumber` for the lock/trigger/unlock slices, the typed state for `Transforms`/`UploadsPreSaft`/`UploadsSaft` (they call transitions on it) | With `alreadyUnlocked` folded into the port, the unlock handlers need only the org number — their three-case response collapses to `Proceed`/`DoNotProceed` like every other slice. The `Unlocking`/`Cancelling` distinction only matters at fan-in, which re-fetches anyway |
| D3 | The skip **logging moves into `FetchMigration`** (which gains `ILogger`), keeping the exact messages, levels, and `LogEvents` ids | Only the port knows *why* it said don't proceed (cancelled = Warning vs already-done = Debug). The handler's skip branch becomes a bare `return`. Handlers left with no log statements (`UploadsPreSaft`, `UploadsSaft`) drop `ILogger` entirely |
| D4 | The fan-in re-fetch goes back to **`Migrations.IFetch`** (supersedes spec 4 D6) | Once the stage's flag is set, the guard port answers `DoNotProceed` *by design* — it cannot serve fan-in, which needs the fresh state precisely when the flag is set. Fan-in is a state peek, not stage admission; it restores spec 1's quiet-tolerant matching. Handlers with fan-in (`LocksSource`, `LocksTarget`, `TriggersExport`, `UnlocksSource`, `UnlocksTarget`) therefore keep both ports |
| D5 | `NotifyCompletions/` is dissolved: `Request` nests inside `INotifyCompletion`, and the single remaining file moves to the `Mt.Domain` project root (namespace `Mt.Domain`) | Direct consequence of the nesting convention plus spec 2 §2's ≥2-files-per-namespace rule |

## 1. The response shape (new standard for stage guards)

```csharp
public interface IFetchMigration
{
    Task<Result<Response>> HandleAsync(Id migrationId, CancellationToken ct);

    public abstract record Response
    {
        public sealed record Proceed(OrganizationNumber OrganizationNumber) : Response;

        public sealed record DoNotProceed : Response;
    }
}
```

`FetchMigration` maps every state to `Proceed`, `DoNotProceed` (logging why), or
`MigrationHasIncorrectState` (unchanged per-stage message). The handler preamble is now:

```csharp
var fetched = await fetchMigration.HandleAsync(migrationId, ct);
if (fetched.IsFailed(out var response, out var fetchFailures))
{
    return fetchFailures;
}

if (response is not IFetchMigration.Response.Proceed(var organizationNumber))
{
    return default(ValueTuple);
}
```

## 2. Scope — per slice

| Slice | `Proceed` carries | `DoNotProceed` covers (logged by the port) |
|---|---|---|
| `LocksSource` | `OrganizationNumber` | `Cancelling`/`Cancelled` (Warn); `Created.SourceLocked` (Debug) |
| `LocksTarget` | `OrganizationNumber` | same, with `TargetLocked` |
| `TriggersExport` | `OrganizationNumber` | same, with `ExportTriggered` |
| `Transforms` | `Created` | `Cancelling`/`Cancelled` (Warn); readiness stays a handler failure (spec 4 D7) |
| `UploadsPreSaft` | `Transformed` | `Cancelling`/`Cancelled` (Warn) |
| `UploadsSaft` | `PreSaftUploaded` | `Cancelling`/`Cancelled` (Warn) |
| `UnlocksSource` | `OrganizationNumber` | `Completed`/`Cancelled` (Debug); `SourceUnlocked` already set (Debug) |
| `UnlocksTarget` | `OrganizationNumber` | same, with `TargetUnlocked` |

Every other state remains `MigrationHasIncorrectState` with the stage's existing message.

## 3. `INotifyCompletion`

`Request` nests inside the interface; the subtypes (`Migrated`, `Cancelled`) and their meaning
are unchanged (spec 2 D2's collision argument now doubles as the nesting convention). The file
moves to `src/Mt.Domain/INotifyCompletion.cs`, namespace `Mt.Domain`; `Request.cs` and the
`NotifyCompletions/` folder are deleted. Call sites (`UnlocksSource`/`UnlocksTarget` handlers,
`Mt.Persistence` `Cancel`, `Mt.Portal`) update to `INotifyCompletion.Request.…`.

## 4. Tests

1. Handler tests feed `Proceed`/`DoNotProceed` through the mocked port and arrange fan-in
   state through a mocked `Migrations.IFetch` again. The separate cancelled/already-done
   handler scenarios merge into one `DoNotProceed` no-op test (the distinction now lives in
   the port and is covered by `FetchMigrationTests`).
2. `FetchMigrationTests` grows the already-done/already-unlocked → `DoNotProceed` mappings.

## 5. Verification / definition of done

1. `dotnet build` clean; `dotnet test` green (Postgres suite needs `docker compose up`).
2. `grep -rln "class Response\|record Response\|record Request" src --include="*.cs"` (excluding
   `obj/`) matches only files that declare them inside an interface.
3. `grep -rn "NotifyCompletions" src tests --include="*.cs"` returns nothing.
4. `CLAUDE.md` records the nesting convention.

## 6. Implementation notes (added during implementation, 2026-07-14)

- `Mt.Persistence` `Cancel` also called `INotifyCompletion` (the nothing-was-locked fast
  path); its call site updated along with the unlock handlers and `Mt.Portal`.
- `UploadsPreSaft`/`UploadsSaft` handlers are no longer `partial` and lost their `ILogger`
  and `Log` class per D3 — their only log line moved into their `FetchMigration`.
- Test aliases settled on `using Response = …IFetchMigration.Response;` (and
  `Request = Mt.Domain.INotifyCompletion.Request;`) so test bodies stay short; files that
  import several slices also alias `IFetchMigration`.
- Net effect on tracked files: −157 lines despite the added port logging.
- `Mt.Persistence.Tests` (Testcontainers) could not run in the implementation environment
  (no docker socket); the other four suites pass (64 tests). Run `dotnet test` with Docker
  available before merging.

## 7. Out of scope

- No changes to the state machine, retry semantics, `ExecuteOnce`, transport, or schema.
- `Client` (returned by `IFetchClient`) is a domain entity, not a port message — it stays
  top-level.
- The intentional near-duplication of the lock/unlock slices still applies (spec 1 §4.3).
