# Spec 3 — Rename All Port Methods to `Handle` / `HandleAsync`

## 0. Context

Spec 2 D7 scoped the `Handle` naming rule to message-receiving interfaces and deferred the rest.
The reviewer has since confirmed the standard applies to **all** ports: every single-method
interface names its method `Handle` (sync) or `HandleAsync` (async), regardless of what verb the
interface name carries. The interface name carries the verb (`IFetch`, `ISetCompleted`,
`IUnlockSource`); the method is always `Handle`/`HandleAsync`.

Implement this **after** spec 2 is merged — it touches every port and its fakes, and stacking it
on top of spec 2's moves would make both diffs unreviewable.

## 1. The rule (standard, supersedes spec 2 D7)

- Single-method port interfaces: the method is `HandleAsync` (or `Handle` if ever sync).
- Signatures, parameters, and return types are unchanged — this is a rename only.
- Applies to implementations, fakes/stubs in tests, and call sites.

## 2. Scope (verify with a fresh grep before starting)

All port interfaces in `Mt.Domain` (~34 as of spec 2), including per-slice duplicates:

| Current method | Interfaces (examples) |
|---|---|
| `FetchAsync` | `IFetch`, `IFetchClient` |
| `SetAsync` | all 11 `ISet*` ports |
| `IncrementAsync` | all 5 `IIncrementAttempts` ports |
| `LockAsync` / `UnlockAsync` | `ILockSource`, `ILockTarget`, `IUnlockSource`, `IUnlockTarget` |
| `UploadAsync` / `DownloadAsync` | `IUploadPreSaft`, `IUploadSaft`, `IDownloadSaft` |
| `TriggerAsync` | `ITriggerExport` |
| `ScheduleAsync` | `IScheduleEvent` |
| `AddAsync` | `IAdd` (outbox) |
| `StartAsync` / `ApproveAsync` / `CancelAsync` | `IStart`, `IApprove`, `ICancel` |

Already conforming (untouched): `IHandleDomainEvent.HandleAsync`, `INotifyCompletion.HandleAsync`.

## 3. Verification

1. `dotnet build` clean, `dotnet test` green (Postgres via `docker compose up`).
2. `grep -rnE "Task<Result<[^>]+>> (Fetch|Set|Increment|Lock|Unlock|Upload|Download|Trigger|Schedule|Add|Start|Approve|Cancel|Notify)Async\(" src tests --include="*.cs"` returns nothing.

## 4. Implementation notes (added during implementation, 2026-07-14)

- Non-port async methods were deliberately left alone: Testcontainers' `_container.StartAsync()`
  in `PostgresFixture.cs`, and the outbox infrastructure methods (`FetchPendingAsync`,
  `PromoteDueAsync`, `MarkProcessedAsync`, `MarkFailedAsync`) — they are not domain ports.
- `Mt.Persistence.Tests` could not run in the implementation environment (no docker socket);
  the other four suites pass. Run `dotnet test` with Docker available before merging.

## 5. Out of scope

- No signature or behavior changes; no interface renames; no folder moves.
