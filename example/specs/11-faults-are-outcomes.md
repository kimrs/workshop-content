# 11 — Faults are outcomes, failures are aborts

When an external system fails to perform an operation we asked of it, that is not a failure of
*our* workflow — it is an expected event in a distributed system, and the domain already has a
whole plan for it (retry scheduling, `MaxAttempts`, `OutOfRetriesFailure`). Modeling it on the
`Result` failure track forced every retryable stage handler to `IsFailed` its way into the
failures, conflating "Source is having a bad day" with "the world is corrupt".

This spec adopts the dependability-theory vocabulary (Avizienis/Laprie): a **fault** is an
expected event that the system *tolerates* (here: by retrying); a **failure** is the system
giving up. Faults become response cases; the `Result` failure channel is reserved for aborts —
the processor logs a failed handler result and moves on, so a failure now always means "a human
should look at this".

It also finishes what the fold discussion started: handlers branch on business outcomes *inside*
the success channel (a `switch` inside `Then`/`ThenAsync`), never by unwrapping the `Result`.
After this spec, no stage handler calls `IsFailed`; the remaining call sites are boundaries
(adapters, the processor, HTTP mapping) and tests.

## Decisions

| # | Decision | Why |
|---|----------|-----|
| D1 | The five faultable ports — `ILockSource`, `ILockTarget`, `ITriggerExport`, `IUnlockSource`, `IUnlockTarget` — return `Result<Response>` with `Response = Locked/Triggered/Unlocked \| Faulted(string Reason)` | These are exactly the operations with an `OperationFailure` config (§7.1) and exactly the stages that schedule retries. A fault is domain input (it drives the retry decision), so it belongs in the value channel |
| D2 | The case is named `Faulted` | Rejected: `FailedAtTarget` (repeats context the nested position already gives, and "failed" is the word we are reserving for aborts), `Unavailable`/`Refused`/`Declined` (claim to know *why*; the port cannot distinguish an outage from a processed-but-failed operation), `TryAgain`/`Retryable` (prescribe the caller's reaction; retry policy belongs to the handler). `Faulted` names the fact with the standard fault-vs-failure distinction |
| D3 | `INotifyCompletion`, `IFetchClient`, `IDownloadSaft`, `IUploadPreSaft`, `IUploadSaft` are unchanged | None of them has a failure mode to model: the Portal notification and the uploads/download only log (§7.1 defines no failure config for them). Their failure tracks carry only external-id resolution failures, which are genuine aborts. If one of them ever gets a failure config, it gets a `Faulted` case then — not speculatively now |
| D4 | Handlers fold responses inside the chain: `result.ThenAsync(response => response switch { Case => Continue…, _ => Done.Task })` | Both prior styles leak: `IsFailed` exits the monad to branch; matching `Result<T>.Completed { Value: … }` names the wrapper's closed generic and is unreadably verbose. The fold keeps failure propagation in the combinator and business branching on the value, mirroring do-notation + `case` in FP. The `_` arm is required anyway (the hierarchies are open, CS8509 + warnings-as-errors) and follows the existing "unnamed case is a benign no-op" convention |
| D5 | `Mt.Results` gains `Match` (`Match.cs`): fold a `Result<T>` into a plain value with one continuation per case | The canonical catamorphism, and the tool adapters need to translate a simulator result into a `Response` at the boundary. Consumers: all five faultable adapters. Allowed in `Mt.Results` per spec 2 D3 — it is a combinator, not an app failure |
| D6 | `Mt.Results` gains `Done` (`Done.cs`): `Done.Result` / `Done.Task`, the completed unit result | Every handler fold needs a "handled, nothing further to do" arm; `Task.FromResult<Result<ValueTuple>>(default)` in a switch arm is noise. Immediate consumers: every stage handler. Rewritten handlers use `Done.Result` in place of `return default(ValueTuple);` for one vocabulary |
| D7 | The simulators and `OperationFailed`/`OperationThrew` are unchanged; adapters fold the simulator result into the `Response` with `Match` (external-id failures are *not* folded — they stay failures) | The failure records are the simulated external systems' own protocol (spec 1 §7, spec 10 D4). The adapter is the anti-corruption layer; through the port, the failed/threw distinction survives only as the `Faulted.Reason` text. The external-id fetch stays on the failure track because retrying cannot fix a broken id mapping |
| D8 | `ScheduleRetryAsync(Id, string reason, CancellationToken)` replaces `OnFailureAsync(Id, Failure[], CancellationToken)`; retry-log wording changes from "failed" to "faulted" | The retry path is now fed by `Faulted.Reason`, not by failures. The `IScheduleEvent` response is folded the same way (`Scheduled` → log + done, `Exhausted` → log + `OutOfRetriesFailure`). Out-of-retries stays a failure: it is precisely the point where a tolerated fault, having exhausted its tolerance budget, escalates into a failure |
| D9 | `LocksTarget` recovers the trio semantics: a failed fetch propagates immediately and is never retried | The interim pattern-matching rewrite accidentally routed fetch failures (e.g. `MigrationHasIncorrectState`) through retry scheduling. Incorrect state is a bug; retrying it is noise |

## 1. Mt.Results

1. Add `Match.cs`: `TRes Match<TRes>(Func<T, TRes> completed, Func<IReadOnlyList<Failure>, TRes> failed)`.
2. Add `Done.cs`: `Done.Result` (completed `Result<ValueTuple>`) and `Done.Task` (its cached task).

## 2. Ports (Mt.Domain)

`ILockSource`, `ILockTarget` (`Locked | Faulted`), `ITriggerExport` (`Triggered | Faulted`),
`IUnlockSource`, `IUnlockTarget` (`Unlocked | Faulted`): `HandleAsync` returns
`Result<Response>`; `Response` is a nested abstract record per spec 5, `Faulted` carries
`string Reason`.

## 3. Adapters (Mt.Source / Mt.Target)

`LockSource`, `TriggerExport`, `UnlockSource`, `LockTarget`, `UnlockTarget`: resolve the
external id as before (failures propagate), then fold only the simulator result:

```csharp
return punchCard.Then(id => simulator.Run(…)
    .Match(
        completed: ILockSource.Response (_) => new ILockSource.Response.Locked(),
        failed: failures => new ILockSource.Response.Faulted(failures[0].Message)));
```

(The explicit lambda return type stands in for a `Match<Response>(…)` type argument: the C# 14
compiler does not resolve explicit type arguments against extension-block members, so `TRes`
is supplied via inference instead.)

## 4. Stage handlers (Mt.Domain)

All eight handlers get the fold shape; none calls `IsFailed` afterwards.

- Head (all): `fetched.ThenAsync(response => response switch { Proceed… => <verb>Async(…), _ => Done.Task })`.
- Retryable five (`LocksSource`, `LocksTarget`, `TriggersExport`, `UnlocksSource`,
  `UnlocksTarget`): `<verb>Async` folds the operation response — `Faulted(var reason)` →
  `ScheduleRetryAsync`, otherwise `AdvanceAsync` (flag write + existing fan-in).
  `ScheduleRetryAsync` folds the schedule response into `RetryScheduled`/`OutOfRetries`
  helpers (log + result), following the `LogCompleted`/`LogCancelled` precedent.
- `Transforms`: `TransformAsync` folds the client — `WithoutAddress` → `AutoCancelAsync`,
  otherwise `AdvanceAsync`.
- `UploadsPreSaft`, `UploadsSaft`: head fold only; the existing chains move into the
  continuation method unchanged.

## 5. Tests

- `CombinatorTests`: `Match` folds both cases; `Done.Result` is completed.
- `LocksSourceHandlerTests`: lock arrangements return `Response.Locked` / `Response.Faulted`
  instead of `Ok` / `Failed()`; assertions unchanged.
- `UnlocksSourceHandlerTests`: the unlock arrangement returns `Response.Unlocked`.
- `Mt.Source.Tests` / `Mt.Target.Tests` `SimulationTests`: the lock/unlock tests assert
  `Faulted`/`Locked`/`Unlocked` through the port; the Throw-mode test asserts the reason
  carries the "threw" wording (D7: the distinction is reason text through the port).

## 6. CLAUDE.md

Update the Result-usage bullet: linear fallible sequences chain with `Then`/`ThenAsync`;
business branching folds the response inside the chain; `IsFailed` only at boundaries.
Add: external-operation faults are response cases (`Faulted`), never failures — the failure
channel means "abort; a human looks".

## 7. Verification

- `dotnet build` (warnings as errors); `Mt.Results.Tests`, `Mt.Domain.Tests`,
  `Mt.Source.Tests`, `Mt.Target.Tests` pass.
- `grep -rn "IsFailed" src --include="*.cs"` hits no file under `Mt.Domain/Stages/`.
