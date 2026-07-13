# 12 — Honest delivery semantics and boundary repairs

## 0. Context

A spec-blind code review (2026-07-14, reviewing only the code and its patterns) confirmed the
design holds together but found two bugs, one demo-defeating configuration mix-up, and several
places where the code's own comments promise stronger guarantees than the implementation
delivers. This spec turns the agreed findings into changes. The review's "HEAD does not
compile" finding was resolved before this spec (commit `3f7e1b5` added the missing
`Done.cs`/`Match.cs`/spec 11).

### Decisions made in this spec (flag to the reviewer if any feels wrong)

| # | Decision | Why |
|---|----------|-----|
| D1 | `Mt.Api` references `Mt.Portal` and calls `AddPortal()` | `Commands.Cancel` injects `INotifyCompletion` (since spec 5's immediate-finalize path); without the registration, standalone `POST /Cancel` 500s with an unresolvable-service error — proven by running the API |
| D2 | The consume side becomes genuinely at-least-once: `IMessageConsumer` changes from `IAsyncEnumerable<MessageEnvelope>` to a callback shape — `ConsumeAsync(Func<MessageEnvelope, CancellationToken, Task> handler, ct)` — and the Postgres consumer marks `ConsumedAt` **after** the handler returns, not at claim time | The old claim-before-yield made consumption at-most-once (a crash mid-handling lost the message forever) while the comments told the at-least-once story. The callback shape lets the consumer bracket the handler without leaking a `MessageId` into the envelope (there deliberately is none). A crash between handling and marking now redelivers, and the inbox dedups — the machinery finally exercises the pattern it exists for |
| D3 | The InMemory consumer keeps fire-and-forget semantics under the new signature | An in-process channel dies with the process; there is nothing durable to redeliver from. Documented instead of pretended away |
| D4 | Processing aborts get a durable record **on the inbox row**: `InboxRow` gains nullable `FailedAt`/`FailureReason`; on handler failure `ExecuteOnce` rolls back the transaction, then records the claim with the failure in a fresh transaction; a redelivered attempt that previously aborted is skipped ("previously failed") instead of re-run | "Failure = abort; a human looks" was only a log line — message consumed, outbox processed, nothing in the database to look at. The inbox *is* the per-attempt ledger, so the abort belongs there; recording it also stops redeliveries from pointlessly re-running an attempt that already aborted. The record is written after the rollback, so a crash in between just means the redelivery re-aborts and records then — it converges |
| D5 | `RedeliverProbability` moves to where it acts: `Mt.Outbox` (publisher) gets `0.15`, `Mt.Processor` loses the key | Duplicate injection lives in the publishers; the split-mode config had `0.0` on the publisher and `0.15` on the consumer, where it is never read — the inbox dedup demo was dead in exactly the mode meant to show it |
| D6 | The Postgres transport implementation moves to `Mt.Persistence/Transport/`; `Mt.Transport` keeps only the ports, envelope, settings, redelivery helper, and the InMemory implementation; the `AddTransport` selection extension moves to `Mt.Persistence.Registration` | `Mt.Transport` referencing `Mt.Persistence` fused the abstraction to one implementation's storage and dragged EF Core into anything touching the ports. Persistence implementing a transport port is ordinary adapter work; the reference now points the right way (`Mt.Persistence → Mt.Transport`, `Mt.Transport → nothing`). Hosts already import `Mt.Persistence`, so call sites only lose a `using` |
| D7 | `LocksTarget/Handler.cs` is realigned to the two-arm fold shape of its siblings | The intentional near-duplicates only pay off if the copies stay diff-clean; `LocksTarget` carried redundant `DoNotProceed =>` and `Locked =>` arms left over from an interim rewrite — noise for anyone diffing the trio |
| D8 | Scheduled retries carry the original trace: `ScheduledEventRow` gains `TraceParent`, captured by `ScheduleEvent` (where the restored activity is ambient) and copied by `Scheduler` on promotion | `Scheduler.PromoteDueAsync` stamped `Activity.Current?.Id` from the outbox worker's poll loop — always null — so exactly the messages most worth correlating (retries of a flaky operation) fell out of the trace |
| D9 | `ScheduleEvent` guards the empty-inbox case with a typed failure instead of letting `MaxAsync` throw | The MAX-from-inbox trick has an implicit contract (must run inside the inbox transaction that flushed the claim); violating it exploded with an unrelated `InvalidOperationException`. A `NotFoundFailure` naming the contract is diagnosable |
| D10 | Stage settings config keys are renamed to match the slice names (`Stages:LocksSource`, `Stages:TriggersExport`, …) | The old keys (`LockSource`, `ExportTrigger`) matched nothing in the code, and the `?? new()` fallback silently swallows a miss — a typo yields default retry budgets with no symptom. The fallback itself stays: absent-section-means-defaults is workshop-friendly; the fix is making the keys greppable |
| D11 | `Result<T>.Failed.Failures` becomes get-only; `Attempt.Create` requires `>= 1` | The `init` accessor let `failed with { Failures = [] }` bypass the constructor's non-empty guard. Attempt 0 was constructible but nothing can legitimately produce it (`First` is 1) — a zero-attempt envelope minted a phantom dedup key |
| D12 | Transport publishers convert to the `[LoggerMessage]` + nested `Log` idiom | The one logging convention in the codebase, applied everywhere except two files that CA1873 happens not to catch |
| D13 | New `tests/Mt.EndToEnd.Tests` project (Testcontainers): full pipeline — Start command → outbox worker → transport → processor → inbox → stages — for a retry-then-succeed happy path and an auto-cancel path | Bugs D1 and D5 lived precisely in the composition no test exercised; every layer was tested in isolation. A separate project keeps `Mt.Persistence.Tests` from referencing half the solution |
| D14 | Domain-owns-`Prepared` review point: **no code change, recorded** | The rule ("all three flags set ⇒ ready") already lives in the domain as `Created.IsReadyToTransform`; the flag-set op only maps the domain's answer onto the state column — the same class of work as `Mapping.ToDomain`. The alternatives (a `Result`-returning `Prepare()` whose failure is routine control flow, or a single-consumer union type) cost more than they clarify |
| D15 | Outbox publish failures stay fail-fast (`FailedAt` on first exception): **no change, recorded** | Retrying forever floods the log every 500 ms; a bounded counter is a new column and policy for a path the workshop never exercises. The failure *is* durable and inspectable (`FailedAt`/`FailureReason`), unlike the processing aborts D4 fixes |
| D16 | `review.md` (four-line stub) is deleted; `.DS_Store` untracked and ignored; `CLAUDE.md` stays gitignored | The stub is an accidental overwrite of a served-its-purpose file. `.DS_Store` is noise. CLAUDE.md's ignore status is the owner's call — flagged, left alone |

## 1. Bug fixes

1. `Mt.Api.csproj` references `Mt.Portal`; `Mt.Api/Program.cs` calls `AddPortal()` (D1).
2. `Mt.Outbox/appsettings.json`: `Transport:RedeliverProbability` = `0.15`;
   `Mt.Processor/appsettings.json`: the key is removed (D5).
3. `LocksTarget/Handler.cs`: head fold becomes `Proceed => LockAsync…, _ => Done.Task`;
   `LockAsync` becomes `Faulted(var reason) => ScheduleRetryAsync…, _ => AdvanceAsync…` (D7).

## 2. Consume side: at-least-once (D2, D3)

1. `IMessageConsumer`:

   ```csharp
   public interface IMessageConsumer
   {
       Task ConsumeAsync(Func<MessageEnvelope, CancellationToken, Task> handler, CancellationToken ct);
   }
   ```

2. `PostgresConsumer`: per poll, load a batch of unconsumed rows (no write); for each row,
   invoke the handler, then set `ConsumedAt` and save — one row at a time, so a crash
   redelivers only in-flight work. The inbox absorbs the redelivery.
3. `InMemoryConsumer`: `await foreach` over the channel, invoking the handler per envelope.
   Doc comment states plainly: in-flight messages die with the process.
4. `ProcessorWorker.ExecuteAsync` becomes `await consumer.ConsumeAsync(ProcessAsync, stoppingToken);`
   — `ProcessAsync` never throws (failures are logged), so a handled failure still consumes
   the message; that is the abort semantics, now backed by D4's record.

## 3. Durable abort record (D4)

1. `InboxRow` gains `DateTimeOffset? FailedAt` and `string? FailureReason`.
2. `ExecuteOnce`, on handler-lookup or handler failure: roll back, `ChangeTracker.Clear()`,
   insert the claim row with `FailedAt`/`FailureReason` (first failure's message) in a new
   transaction; swallow a unique-violation race as already-recorded.
3. The duplicate pre-check distinguishes: existing row with `FailedAt` logs
   "previously failed, skipping" (Warning, new `LogEvents.InboxAbortRecorded = 1013`);
   a clean row keeps the existing duplicate-skip log.
4. Regenerate the `InitialCreate` migration (disposable database, precedent spec 6 — one
   regeneration also covers D8).

## 4. Transport layering (D6)

1. `PostgresPublisher`/`PostgresConsumer` move to `src/Mt.Persistence/Transport/`
   (namespace `Mt.Persistence.Transport`, ≥2 files).
2. `Mt.Transport` loses its `Mt.Persistence` reference and its `Registration.cs`;
   `Redelivery` becomes public (used from persistence now).
3. `Mt.Persistence/Registration.cs` gains `AddTransport(this IServiceCollection, IConfiguration)`
   with the same kind-selection logic; `Mt.Persistence.csproj` references `Mt.Transport` and
   `Microsoft.Extensions.Options.ConfigurationExtensions`.
4. Host `Program.cs` files drop the now-unneeded `using Mt.Transport;`.

## 5. Retry traces and the schedule contract (D8, D9)

1. `ScheduledEventRow` gains `string? TraceParent`; `ScheduleEvent` stamps
   `Activity.Current?.Id` (the processor restored the activity, so this is the original
   trace); `Scheduler.PromoteDueAsync` copies it onto the outbox row instead of reading
   `Activity.Current` in the poll loop.
2. `ScheduleEvent` computes `MaxAsync(row => (int?)row.Attempt)`; `null` returns
   `NotFoundFailure("No inbox claim for … — IScheduleEvent must run inside the inbox transaction.")`.

## 6. Small repairs (D10–D12)

1. `StageHandlers.cs` + `Mt.Host`/`Mt.Processor` appsettings: `Stages:LocksSource`,
   `Stages:LocksTarget`, `Stages:TriggersExport`, `Stages:UnlocksSource`, `Stages:UnlocksTarget`.
2. `Result<T>.Failed.Failures` → `{ get; }`; `Attempt.Create` fails for values `< 1`
   ("must be positive").
3. `InMemoryPublisher`/`PostgresPublisher` redelivery logs via `[LoggerMessage]` in a nested
   `Log` class.

## 7. Tests (D13 + coverage from the review)

1. New `tests/Mt.EndToEnd.Tests` (xUnit + Testcontainers): builds a real host
   (`AddPersistence` + simulators + transport (InMemory) + stage handlers + portal + both
   workers) against a container database.
   - **Retry-then-succeed happy path**: `Source:Lock:FailUntilAttempt = 2`,
     `RedeliverProbability = 0.25`; Start → poll to `SaftUploaded` → Approve → poll to
     `Completed`; asserts the lock retry actually happened (inbox has attempt 2).
   - **Auto-cancel path**: `Client:HasAddress = false`; Start → poll to `Cancelled`;
     external ids released.
2. `InboxTests`: aborted handler → inbox row carries `FailedAt`/`FailureReason`; redelivery
   of that attempt does **not** re-run the handler.
3. `ScheduleEventTests`: empty inbox → typed `NotFoundFailure` (D9).
4. `MappingAndCommandTests`: `Cancel` command immediate-finalize path (cancel from `Created`
   with nothing locked → row `Cancelled`, ids released, notification sent, no unlock events).

## 8. Docs & hygiene (D16)

1. README: testing section drops "warning propagation", gains end-to-end suite; project list
   reflects the transport split; the idempotency section states the delivery guarantees
   honestly (at-least-once end-to-end on the Postgres transport; InMemory loses in-flight
   messages with the process).
2. CLAUDE.md: transport layering bullet (ports + InMemory in `Mt.Transport`, Postgres
   implementation in `Mt.Persistence/Transport/`), inbox-records-aborts bullet.
3. Delete `review.md`; `git rm --cached .DS_Store` + `.gitignore` entry.

## 9. Verification / definition of done

1. `dotnet build` clean (warnings as errors); non-Docker suites green; `Mt.Persistence.Tests`
   and `Mt.EndToEnd.Tests` need Docker — run before merging if this environment lacks a socket.
2. Standalone `Mt.Api`: `POST /Migration/{org}/Cancel` no longer fails with an unresolvable
   `INotifyCompletion` (returns 404/409/200 per state).
3. `grep -rn "Mt.Persistence" src/Mt.Transport --include="*.cs" *.csproj` → nothing;
   `Mt.Transport.csproj` has no project references.
4. `grep -rn "RedeliverProbability" src/Mt.Processor` → nothing.
5. The `LocksSource` vs `LocksTarget` handler diff shows only naming differences.

## 10. Out of scope

- Multi-consumer safety (fan-in flag races, the inbox loser-assumes-success race) — the
  single-sequential-processor assumption stands, now noted where it matters.
- Outbox publish retry policy (D15) and richer HTTP error bodies (first failure only).
- Git history rewriting (the "," commit messages stay).
- The intentional near-duplication of slices and simulators (unchanged as a convention).
