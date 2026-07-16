# Spec 4 — Per-Slice `IFetchMigration`: Move the Stage Preamble Out of the Handlers

## 0. Context

Every stage handler opens with the same ~18-line preamble: fetch the migration, return the
failures if the fetch failed, skip if the migration is cancelled, cast to the state the stage
works on. The repetition was flagged as overhead; a shared base class was considered and
rejected (composition over inheritance, and the unlock slices don't fit the common shape —
see D4). Instead, each slice gets its own `IFetchMigration` port whose response is a small
discriminated union: either the typed state to work on, or a signal that there is nothing
left to do.

This is not a violation of spec 1 §4.3 (duplicate before you abstract): the preamble appears
in all eight handlers, well past the four-repetition threshold, and the slices keep their own
port/response/implementation — nothing is merged across slices.

### Decisions made in this spec (flag to the reviewer if any feels wrong)

| # | Decision | Why |
|---|----------|-----|
| D1 | The per-slice `FetchMigration` implementations live in **`Mt.Domain`** (in the slice folder), composing the shared `Migrations.IFetch` | Which states mean "skip" vs "wrong" for a stage is domain policy; it must not leak into `Mt.Persistence`. Persistence keeps exactly one `Fetch` adapter |
| D2 | `Migrations.IFetch` **stays** as the single shared persistence port | All eight slices fetch the same aggregate the same way; eight identical persistence ports would be duplication with no potential to diverge. Spec 1 §6.3 defines it as shared |
| D3 | `Response` subtypes are **nested** (`Response.Proceed`, `Response.Cancelled`, …) | Top-level `Cancelled`/`Unlocking`/`Cancelling` collide with the migration states used in the same files; same reasoning as spec 2 D2 (`Request.Cancelled`) |
| D4 | The unlock slices get a **three-case** response (`Unlocking` / `Cancelling` / `Finalized`) instead of `Proceed`/`Cancelled` | Their skip set is `Completed or Cancelled` (for them `Cancelling` is a *working* state), and they accept two states. Per-slice response shapes absorb this variance naturally |
| D5 | The "already done" flag checks (`created.SourceLocked`, `unlocking.SourceUnlocked`, …) **stay in the handlers** | They are stage-progress checks on the typed state, one line each, logged at Debug (idempotency) — different meaning and level than the cancellation skip (Warning). The port stays a fetch |
| D6 | The fan-in re-fetch uses the **same** `IFetchMigration` port | The handler keeps one fetch dependency. Behavior change: a wrong state during fan-in now returns `MigrationHasIncorrectState` (rolling back the stage) where it previously returned quiet success. Unreachable in practice — the re-fetch runs inside the same inbox transaction that observed the state a few statements earlier — so the stricter behavior is accepted |
| D7 | `Transforms` keeps its `IsReadyToTransform` check in the handler | Readiness is stage progress (like D5), not state admission. The port admits any `Created` |
| D8 | Wrong-state messages keep their existing per-stage text | They move verbatim from the handlers into the `FetchMigration` classes; log output and failure messages stay greppable |

## 1. The pattern (new standard for stage handlers)

Each slice folder under `Stages/` gains three files:

1. **`Response.cs`** — the union of what the fetch can find, per the no-enums rule:

   ```csharp
   public abstract record Response
   {
       public sealed record Proceed(Created Migration) : Response;

       public sealed record Cancelled : Response;
   }
   ```

2. **`IFetchMigration.cs`** — the port (verb in the interface name, method is `HandleAsync`):

   ```csharp
   public interface IFetchMigration
   {
       Task<Result<Response>> HandleAsync(Id migrationId, CancellationToken ct);
   }
   ```

3. **`FetchMigration.cs`** — the domain-side implementation composing `Migrations.IFetch`
   (D1). Wrong state stays a *failure*; cancellation is a normal outcome:

   ```csharp
   public sealed class FetchMigration(IFetch fetch) : IFetchMigration
   {
       public async Task<Result<Response>> HandleAsync(Id migrationId, CancellationToken ct)
       {
           var fetched = await fetch.HandleAsync(migrationId, ct);
           return fetched.Then(migration => ToResponse(migrationId, migration));
       }

       private static Result<Response> ToResponse(Id migrationId, Migration migration) => migration switch
       {
           Cancelling or Cancelled => new Response.Cancelled(),
           Created created => new Response.Proceed(created),
           _ => new MigrationHasIncorrectState(
               $"SourceLock expected migration {migrationId.Value} to be Created but was {migration.GetType().Name}."),
       };
   }
   ```

The handler preamble collapses to (no cast, no state switch):

```csharp
var fetched = await fetchMigration.HandleAsync(migrationId, ct);
if (fetched.IsFailed(out var response, out var fetchFailures))
{
    return fetchFailures;
}

if (response is not Response.Proceed(var created))
{
    Log.SkippedCancelled(logger, migrationId.Value);
    return default(ValueTuple);
}
```

Handlers drop their `IFetch` constructor dependency entirely; `fetchMigration` replaces it,
including in `FanInAsync` (D6).

## 2. Scope — response shape per slice

| Slice | Skip states → case | Working state(s) → case |
|---|---|---|
| `LocksSource` | `Cancelling`, `Cancelled` → `Response.Cancelled` | `Created` → `Response.Proceed` |
| `LocksTarget` | same | same |
| `TriggersExport` | same | same |
| `Transforms` | same | same (readiness stays in handler, D7) |
| `UploadsPreSaft` | same | `Transformed` → `Response.Proceed` |
| `UploadsSaft` | same | `PreSaftUploaded` → `Response.Proceed` |
| `UnlocksSource` | `Completed`, `Cancelled` → `Response.Finalized` | `Unlocking` → `Response.Unlocking`, `Cancelling` → `Response.Cancelling` |
| `UnlocksTarget` | same | same |

Every other state is `MigrationHasIncorrectState` with the stage's existing message (D8).

The unlock handlers switch on the response instead of the migration; the `default` arm is the
`Finalized` skip. Their fan-in matches `Response.Unlocking({ IsFullyUnlocked: true } …)` /
`Response.Cancelling({ IsFullyUnlocked: true } …)`.

## 3. Registration

`Mt.Processor/StageHandlers.cs` (`AddStageHandlers`, also used by `Mt.Host`) registers each
slice's `IFetchMigration` → `FetchMigration` as scoped, alongside the existing handler
registrations.

## 4. Tests

1. Handler tests (`LocksSourceHandlerTests`, `TransformsHandlerTests`,
   `UnlocksSourceHandlerTests`) mock `IFetchMigration` and feed `Response` values; the
   existing scenarios (fan-in, retry, cancelled-skip, already-done) are preserved.
   Note: test files that import more than one slice namespace need a `using Response = …`
   alias, since every slice now defines a `Response`.
2. New `FetchMigrationTests` covers the guard mapping for the two shapes — `LocksSource`
   (forward) and `UnlocksSource` (teardown): working state → proceed case, skip states →
   skip case, wrong state → `MigrationHasIncorrectState`, fetch failure propagates.

## 5. Verification / definition of done

1. `dotnet build` clean (warnings are errors).
2. `dotnet test` green (Postgres suite needs `docker compose up`).
3. `grep -rn "IFetch" src/Mt.Domain/Stages/*/Handler.cs` matches only `IFetchMigration` /
   `IFetchClient` — no handler talks to the shared fetch port directly (the per-slice
   `FetchMigration` classes are the only `Migrations.IFetch` consumers under `Stages/`).
4. Slice folders still satisfy the ≥2-files-per-namespace rule (they grow, none shrink).

## 6. Implementation notes (added during implementation, 2026-07-14)

- `Transforms`' wrong-state message split in two: the port keeps the per-stage
  "expected … to be Created but was {state}" text (D8), and the handler's remaining
  readiness check now says "expected migration {id} to be ready but setup is incomplete"
  (the old combined message named a state that is no longer in scope at that point).
- The unlock handlers kept their `switch` shape; the `Finalized` skip became the `default`
  arm, replacing the now-impossible wrong-state arm (the port owns that failure).
- Handler tests gained two scenarios while converting: `Transforms` "Created but not ready
  is a failure" and `UnlocksSource` "Finalized migration is a graceful no-op".
- `Mt.Persistence.Tests` (Testcontainers) could not run in the implementation environment
  (no docker socket); the other four suites pass (62 tests). Run `dotnet test` with Docker
  available before merging.

## 7. Out of scope

- No changes to the state machine, retry semantics, `ExecuteOnce`, transport, or schema.
- No changes to the other `Migrations.IFetch` consumers — the persistence `Fetch` adapter
  remains the only implementation of the shared port.
- No merging of the intentionally duplicated slices (spec 1 §4.3 still applies).
