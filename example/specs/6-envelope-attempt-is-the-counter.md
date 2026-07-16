# 6 — Drop persisted attempt counters; the envelope `Attempt` is the counter

## Context

Spec 1 (§2.2, §6.3) put a persisted attempt counter on the migration row for each
retryable stage, incremented through an `IIncrementAttempts` port at the start of every
handler run and stamped onto outgoing messages. Working the domain, this turned out to be
redundant:

- The message envelope already carries `Attempt`; the handler receives it as a parameter
  and ignored it in favor of the counter.
- The counter and the envelope value stay in lockstep by construction — the inbox
  guarantees one handler run per `(MigrationId, DomainEvent, Attempt)` — so the same
  number was stored twice.
- Retry policy (`MaxAttempts`) is an engineering decision, not domain state; nothing in
  the business reads the counters.

## Decision

The envelope `Attempt` is the single source of truth. **This overrides spec 1 §2.2
("the attempt counter lives with the migration/stage, persisted, incremented per try")
and the §6.3 per-stage counter ports.** The dedup rule itself is unchanged: two messages
are the same iff `(MigrationId, DomainEvent, Attempt)` are equal.

- Fan-out mints `Attempt.First`. The only place a new attempt number is created is the
  handler's failure branch — `IScheduleEvent` with `attempt.Next()` — which commits
  atomically with the inbox claim, so dedup-key uniqueness is preserved.
- Handlers use the `attempt` parameter for the adapter call (the simulators'
  `FailUntilAttempt` compares against it, §7) and for the `MaxAttempts` check.
- Behavior is unchanged: the first delivery is attempt 1 either way, and a retryable
  failure at attempt `N < MaxAttempts` schedules attempt `N + 1`.

## Rejected alternative (recorded for the workshop)

Moving the increment / retry decision into `ExecuteOnce` ("increment when the handler
returns `Failure`") does not work with the transaction design (§8.4): a handler `Failure`
rolls back the whole transaction, so any increment made there would be rolled back with
it. Retry scheduling must commit *together with* the inbox claim, which is why a retryable
miss is a *successful* outcome from `ExecuteOnce`'s point of view. Which failures are
retryable, which event to re-emit, and the stage's `MaxAttempts` are per-slice knowledge.
`ExecuteOnce` stays a pure idempotency wrapper.

## Changes

1. Delete the five `IIncrementAttempts` domain ports and the five `IncrementAttempts`
   persistence adapters; drop the five `…Attempts` columns from `MigrationRow` and
   regenerate the `InitialCreate` migration (the database is disposable — no upgrade
   path needed).
2. Stage handlers: delete the increment block and use the `attempt` parameter throughout.
3. Namespace-rule follow-up (spec 2): the five persistence stage folders drop to one file
   each, so the `Set…` adapters move up to `Mt.Persistence/Stages`, beside
   `SetPreSaftUploaded` / `SetSaftUploaded`.
4. Tests updated accordingly; no new test surface — existing handler tests now drive the
   attempt through the `HandleAsync` parameter.
