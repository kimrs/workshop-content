# Spec 2 — Fix Code-Review Findings (review.md, 2026-07-13)

## 0. Context

`review.md` (repo root) is a code review of the implementation of `specs/1-workshop-eda-migration.md`.
This spec turns each finding into a concrete, verifiable change. Where this spec and spec 1 conflict,
**this spec wins**; everything spec 1 says that is not touched here still applies (especially §4 coding
rules, §4.3 duplicate-don't-DRY, and §10 exclusions).

The working tree already contains a partial hand-edit of
`src/Mt.Domain/Stages/UnlocksSource/Handler.cs` — it is the reviewer's reference implementation for the
chaining style in §6 below. Keep its intent, finish it properly, and delete the commented-out leftovers
at the bottom of the file.

### Decisions made in this spec (flag to the reviewer if any feels wrong)

| # | Decision | Why |
|---|----------|-----|
| D1 | New frontend project is named **`Mt.Portal`** | Short, matches `Mt.*` scheme, "portal" is the thing users open to see status |
| D2 | `Request` subtypes are **nested** (`Request.Migrated`, `Request.Cancelled`) | A top-level `Cancelled` collides with the `Cancelled` migration state in `Mt.Domain.Migrations`, which the same handlers already use |
| D3 | Failures that Mt.Results' own combinators construct **stay in Mt.Results** (root namespace) | `Ensure`/`FailWhen`/`Validator`/`Try` create them; moving them out would force Mt.Results to depend on other projects |
| D4 | `Start` returns **`DuplicateFailure`** instead of `ConditionalFailure` | "A migration is already in progress" is a duplicate start; this is also what makes the 409 mapping self-explanatory |
| D5 | CA1873 is fixed with **`[LoggerMessage]` source-generated log methods** and enforced via `.editorconfig` | Verified: CA1873 fires on the current calls; the hand-written `LogCompleted` in review.md still triggers it. LoggerMessage is the canonical fix and also resolves CA1848 |
| D6 | Simulator logs in `Mt.Source`/`Mt.Target` **keep** `OrganizationNumber` | They model external systems that never see a `MigrationId`; org number is their only correlation key. Everywhere else org number leaves the logs. **Confirmed by reviewer** |
| D7 | `HandleAsync` naming applies to **message-receiving interfaces** under this spec | Reviewer confirmed the rule applies to *all* ports; the full rename is deliberately split out to `specs/3-rename-ports-to-handle.md` to keep this diff reviewable |

---

## 1. Mt.Results — dissolve the `Failures` junk drawer (review: Mt.Results #1)

1. Delete the `Mt.Results.Failures` namespace and folder. Files move to the project root with
   namespace `Mt.Results`:
   - `Failure.cs`, `Severity.cs`
   - `ValidationFailure.cs`, `NotFoundFailure.cs`, `DuplicateFailure.cs`,
     `UnexpectedNullFailure.cs`, `ExceptionalFailure.cs` — these stay **only** because Mt.Results'
     own combinators construct them (`Ensure`, `FailWhen`, `Validator`, `ResultUtilities.Try`).
2. **Rule (new standard):** Mt.Results defines only `Failure`, `Severity`, and failures its own
   combinators produce. Every other failure type is defined by the project that returns it
   (exemplar already in the codebase: `MigrationHasIncorrectState` in `Mt.Domain.Migrations`).
3. Move `OutOfRetriesFailure` → `src/Mt.Domain/Stages/OutOfRetriesFailure.cs`,
   namespace `Mt.Domain.Stages` (only the stage handlers create it).
4. Delete `ConditionalFailure`. Replace each usage:
   - `Mt.Persistence` `Start` ("already in progress") → `DuplicateFailure` (D4).
   - `Mt.Source/Simulator.cs` and `Mt.Target/Simulator.cs` → a new
     `public sealed record OperationFailed(string Message) : Failure(Message, Severity.Error);`
     defined **in each project's root** (intentional near-duplicate per spec 1 §4.3). Note: the
     existing `OperationFailure` class in those projects is the *failure-injection settings* —
     keep it, and make the doc comments distinguish the two.
   - `tests/Mt.Domain.Tests/TestData.cs` → `ValidationFailure`.
   - Update the simulator tests that assert `ConditionalFailure` to assert `OperationFailed`.
5. `Mt.Api/HttpResults.cs`: the 409 arm becomes `DuplicateFailure or MigrationHasIncorrectState`.
   Update the doc comment. (This answers the reviewer's "why does it resolve in a http conflict":
   the only API-reachable case was `Start` on an org with an active migration — a duplicate.)
6. Replace every `using Mt.Results.Failures;` with `using Mt.Results;` (or remove where redundant).

## 2. Flatten single-file namespaces (review: Mt.Domain #1)

**Rule (new standard):** a namespace (= folder) must contain at least two files; otherwise the file
moves up a level until the rule holds — usually the project root. Apply solution-wide, not just Mt.Domain:

- `Mt.Domain`:
  - `Commands/Approvals/IApprove.cs`, `Commands/Cancellations/ICancel.cs`, `Commands/Starts/IStart.cs`
    → `Commands/` (namespace `Mt.Domain.Commands`, now 3 files).
  - `Events/IScheduleEvent.cs` → project root (namespace `Mt.Domain`).
  - `Logging/LogEvents.cs` → project root (namespace `Mt.Domain`).
  - `NotifyCompletions/` gains `Request.cs` in §3 (2 files) → keeps its namespace.
- `Mt.Persistence` (mirror the same rule):
  - `Commands/Starts|Cancellations|Approvals/*.cs` → `Commands/`.
  - `Events/ScheduleEvent.cs`, `Inboxes/ExecuteOnce.cs`, `ScheduledEvents/Scheduler.cs` → project root.
  - `Migrations/Fetches/Fetch.cs` → `Migrations/`; `Outboxes/Adds/Add.cs` → `Outboxes/`.
  - `NotifyCompletions/` is deleted entirely (moves to `Mt.Portal`, §4).
- The per-slice folders under `Stages/` all have ≥2 files — unchanged. Do not rename the short
  class names (`Handler`, `Settings`); only namespaces/folders move.

## 3. `INotifyCompletion` becomes a message handler (review: Mt.Domain #2 + #3)

In `src/Mt.Domain/NotifyCompletions/`:

1. New `Request.cs`:

   ```csharp
   public abstract record Request(Id MigrationId)
   {
       public sealed record Migrated(Id MigrationId) : Request(MigrationId);

       public sealed record Cancelled(Id MigrationId) : Request(MigrationId);
   }
   ```

   Subtypes are nested (D2) because `Cancelled` already names a migration state used in the same
   handlers. No `OrganizationNumber` on the request — its only consumer was log output, which §5 bans.

2. Delete the `CompletionOutcome` enum.
3. The interface becomes:

   ```csharp
   public interface INotifyCompletion
   {
       Task<Result<ValueTuple>> HandleAsync(Request request, CancellationToken ct);
   }
   ```

4. Update both call sites (`UnlocksSource`/`UnlocksTarget` handlers) to pass
   `new Request.Migrated(...)` / `new Request.Cancelled(...)`.
5. **Rule (D7):** under this spec only `INotifyCompletion` is renamed. The reviewer has confirmed
   the standard is `Handle`/`HandleAsync` for **all** ports — that full rename is
   `specs/3-rename-ports-to-handle.md` and happens after this spec is merged.

## 4. New project `Mt.Portal` (review: Mt.Domain #4)

`NotifyCompletion` is not persistence; it represents the frontend where users see migration status.

1. Create `src/Mt.Portal/Mt.Portal.csproj` (class library, picks up `Directory.Build.props`),
   referencing `Mt.Domain` (which brings `Mt.Results`). Add it to `Workshop.sln`.
2. Move the `NotifyCompletion` adapter there (project root, namespace `Mt.Portal`). It stays a
   log-only adapter for the workshop: on `Request.Migrated` log that the migration finished, on
   `Request.Cancelled` that it was cancelled — `MigrationId` only, via the `[LoggerMessage]`
   pattern from §5.
3. Add `Registration.cs` with an `AddPortal(this IServiceCollection)` extension registering
   `INotifyCompletion` → `NotifyCompletion` (scoped, matching the current registration).
4. Remove the `INotifyCompletion` registration and the `NotifyCompletions` folder from
   `Mt.Persistence`. Call `AddPortal()` in every composition root that resolves the stage handlers
   (`Mt.Processor`, `Mt.Host`; add to `Mt.Api` only if it actually resolves handlers — verify by
   running, not by guessing).

## 5. Logging (review: General #1 + #2)

1. **MigrationId only.** Remove `OrganizationNumber` from every log statement in `Mt.Domain`,
   `Mt.Persistence`, and `Mt.Portal`; every migration-flow log line carries `MigrationId`.
   Exception (D6): the `Mt.Source`/`Mt.Target` simulators keep logging `OrganizationNumber` — they
   play external systems that never learn the `MigrationId`.
2. **CA1873.** Add to `.editorconfig` (create one at the repo root if absent):

   ```ini
   dotnet_diagnostic.CA1873.severity = warning
   ```

   With `TreatWarningsAsErrors` already on, violations fail the build.
3. Fix every flagged call by converting to `[LoggerMessage]` source-generated methods: a nested
   `private static partial class Log` inside each class that logs (the class itself becomes
   `partial`), with strongly-typed parameters and the existing `LogEvents` ids/names, e.g.:

   ```csharp
   private static partial class Log
   {
       [LoggerMessage(EventId = 1007, EventName = nameof(LogEvents.MigrationFinalized),
           Level = LogLevel.Information, Message = "Migration {MigrationId} cancelled.")]
       public static partial void MigrationCancelled(ILogger logger, Guid migrationId);
   }
   ```

   Keep the event-id numbering in `LogEvents` as the single source of the id values (reference the
   constants; if the attribute requires literals, add a comment in `LogEvents` that the ids are
   mirrored in `[LoggerMessage]` attributes and must stay in sync).
4. This conversion supersedes review.md's `LogCompleted` sketch, which still triggers
   CA1873/CA1848 — the *intent* (log via a small helper at the end of the chain) is kept, the
   mechanism is LoggerMessage.

## 6. Use Mt.Results the intended way (review: General #3)

Spec 1 §4.4 already mandated `Then`/`ThenAsync` chains; the implementation drifted into
`IsFailed`-and-return staircases. Restore the standard:

1. **Rule (clarified standard):** linear fallible sequences chain with `Then`/`ThenAsync`.
   `IsFailed(out value, out failures)` is reserved for genuine branch points — places where the
   failure changes the control flow (e.g. the retry scheduling in `OnFailureAsync`) or where both
   the value and the failures are needed for different paths.
2. Rewrite `FinalizeCancelledAsync` **and** `FinalizeCompletedAsync` in
   `Mt.Domain/Stages/UnlocksSource/Handler.cs` and `Mt.Domain/Stages/UnlocksTarget/Handler.cs` as
   chains, in the shape of review.md's example (adjusted for §3's `Request` and §5's logging):

   ```csharp
   private async Task<Result<ValueTuple>> FinalizeCancelledAsync(Cancelling cancelling, CancellationToken ct)
       => await cancelling.FinalizeCancellation()
           .ThenAsync(_ => setCancelled.SetAsync(cancelling.Id, ct))
           .ThenAsync(_ => notifyCompletion.HandleAsync(new Request.Cancelled(cancelling.Id), ct))
           .Then(_ => LogFinalized(cancelling.Id));
   ```

   where `LogFinalized` calls the `[LoggerMessage]` method and returns `default(ValueTuple)`.
3. Finish the reviewer's partial edit in `UnlocksSource/Handler.cs`: delete the commented-out block
   at the bottom of the file and the trailing whitespace.
4. Audit the remaining stage handlers and persistence adapters for the same staircase pattern in
   *linear* segments and convert them. Do **not** force branching flows (state `switch`, retry
   paths, already-done short-circuits) into chains — readability wins over point-free style.

## 7. Verification / definition of done

1. `dotnet build` clean — warnings are errors and CA1873 is now enabled, so this proves §5.
2. `dotnet test` — all existing suites pass (Postgres fixture via `docker compose up`, as before);
   spec 1 §12 coverage (happy path, auto-cancel, retry-then-succeed) still green.
3. Greps return nothing:
   - `grep -rn "Mt.Results.Failures\|ConditionalFailure\|CompletionOutcome\|NotifyAsync" src tests --include="*.cs"`
   - `grep -rn "OrganizationNumber" src/Mt.Domain src/Mt.Persistence src/Mt.Portal --include="*.cs" | grep -i "log"`
4. Folder rule holds: no namespace folder with a single `.cs` file under `src/` (excluding `obj/`,
   `Migrations/Generated`).
5. `review.md` has served its purpose once this spec is implemented; leave it in place — the
   reviewer decides when to delete it.

## 8. Implementation notes (added during implementation, 2026-07-14)

- `LogEvents` became **const ints** instead of `EventId` properties — attributes accept consts, so
  no literal-mirroring comment is needed and there is nothing to keep in sync. New ids 1008–1012
  were added for the inbox/scheduler/command milestones that previously logged without an id.
- `SYSLIB1006`/`SYSLIB1025` are NoWarn'd in `Directory.Build.props`: LogEvents ids name milestones,
  not messages, so several `[LoggerMessage]` methods legitimately share one id/name.
- The workers (`ProcessorWorker`, `OutboxWorker`) and the Source/Target simulators were also
  converted to `[LoggerMessage]` — CA1873 flagged them once their projects compiled. Simulator
  logs keep `OrganizationNumber` per D6; their log methods carry no LogEvents id (they are not
  migration milestones).
- Additional single-file namespaces found and flattened beyond the §2 list:
  `Mt.Domain/Outboxes/IAdd.cs` → project root, and
  `Mt.Persistence/Stages/UploadsPreSaft|UploadsSaft/*.cs` → `Stages/`.
- `Mt.Persistence.Tests` (Testcontainers) could not run in the implementation environment
  (no docker socket access); the other four suites pass. Run `dotnet test` with Docker available
  before merging.

## 9. Out of scope

- No changes to the state machine, retry semantics, transport, or database schema.
- No renaming of verb-based ports (D7).
- No new abstractions beyond `Request`, `OperationFailed`, `Mt.Portal`, and the `Log` classes.
- Spec 1 §10's exclusion list still applies in full.
