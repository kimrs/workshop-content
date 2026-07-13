# 7 — `Attempt` is pure infrastructure; handlers and simulators no longer see it

## Context

Spec 6 made the envelope `Attempt` the single source of truth, but handlers still received
it as a parameter. Continuing the domain discovery: the attempt number is not domain
knowledge at all. Handlers used it for exactly two things — forwarding it to the simulated
adapter and doing retry arithmetic (`< MaxAttempts`, `.Next()`) — and both are someone
else's job:

- A real external system does not know its caller's retry count. The simulators taking
  `Attempt` was a leak of our plumbing into the "external" world.
- Minting dedup keys (`attempt + 1`) is bookkeeping for the inbox/scheduling machinery,
  not a stage decision. The stage's decision is only the *policy*: how many attempts it
  is willing to spend.

## Decision

**This overrides spec 1 §6.4/§6.5 step 5 (attempt passed through the handler), §7
("the simulated adapter receives the current `Attempt`", "no hidden counters") and the
remaining §2.2 line about stamping the counter from the handler.** The dedup rule
`(MigrationId, DomainEvent, Attempt)` itself is unchanged.

1. `IHandleDomainEvent.HandleAsync(Id migrationId, CancellationToken ct)` — no `Attempt`.
   The envelope's attempt is consumed by `ExecuteOnce` for the inbox claim and goes no
   further.
2. `IScheduleEvent` owns retry numbering *and* budget enforcement:
   `HandleAsync(Id, DomainEvent, int maxAttempts, ct)` returns `Result<Response>` with
   nested outcomes `Response.Scheduled(Attempt Next)` / `Response.Exhausted`. The
   persistence adapter reads the current attempt as `MAX(Attempt)` from the inbox for
   `(MigrationId, DomainEvent)` — the current message's claim row is already flushed in
   the same transaction, so this is exactly the attempt being processed. If it is at or
   past `maxAttempts` it schedules nothing and reports `Exhausted`; the handler turns
   that into `OutOfRetriesFailure`. The stage keeps the policy (`Settings.MaxAttempts`);
   the adapter keeps the arithmetic. `maxAttempts` stays a bare `int` — precedent:
   `Settings.MaxAttempts` already is one.
3. `IAdd.HandleAsync(Id, DomainEvent, ct)` — fan-out events always start a stage at its
   first attempt, so the adapter stamps `Attempt.First` itself.
4. Simulator ports (`ILockSource`, `ILockTarget`, `ITriggerExport`, `IUnlockSource`,
   `IUnlockTarget`) drop the `Attempt` parameter. Each `Simulator` (Source and Target —
   still intentional near-duplicates) becomes a singleton that counts its *own* calls per
   `(operation, organization number)`, like a real flaky external system would.
   `FailUntilAttempt` keeps its name and config shape: each handler attempt makes exactly
   one adapter call (duplicates are deduped and no-op paths return before the adapter),
   so "call count" and "attempt" coincide — but the count is now the simulator's.

## Consequences

- The workshop teaching point flips: instead of "no hidden counters", the lesson is that
  the retrying app and the flaky external system keep *independent* state that lines up
  only because the inbox guarantees at-most-once handling per attempt.
- Retry-scheduling logs move to the numbers the handler actually has: the next attempt
  (from `Response.Scheduled`) and the exhausted budget (`MaxAttempts`).
- Simulator call counts are per process lifetime, keyed by organization number; restarting
  the host resets them. Fine for a workshop, noted for honesty.

## Changes

1. `Mt.Domain`: shrink `IHandleDomainEvent`; reshape `IScheduleEvent` (nested `Response`
   per spec 5) and `IAdd`; drop `Attempt` from the five simulator ports; update the seven
   stage handlers.
2. `Mt.Persistence`: `ExecuteOnce` dispatch, `ScheduleEvent` (max-from-inbox + budget),
   `Outboxes/Add` (stamp `Attempt.First`).
3. `Mt.Source`/`Mt.Target`: stateful singleton `Simulator`, adapters without `Attempt`,
   registration.
4. Tests: handler tests mock the new `IScheduleEvent` contract; simulation tests drive
   repeated calls against a fresh `Simulator` instance per test; inbox tests update the
   test handler signature. The exhaustion *arithmetic* moves to `ScheduleEvent`, which is
   Postgres-backed — covered by a new persistence test.
