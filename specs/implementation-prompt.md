# Implementation Prompt — Workshop EDA Migration App

You are building a .NET solution from scratch. The complete, authoritative specification is at `specs/workshop-eda-migration.md`. **Read that file in full before writing any code, and treat it as the contract** — every section (§0–§14) is binding. This prompt tells you *how to execute*; the spec tells you *what to build*.

## What this is

A small, self-contained, event-driven "Source → Target migration" app for a public architecture workshop. It's a teaching vehicle: the value is in the **patterns**, not breadth. The single hardest instruction to follow well: **generate exactly what §1–§9 and §11 specify and nothing more.** Do not add stages, abstractions, value objects, helpers, or "nice to have" infrastructure that isn't named. §10 lists things that must NOT exist — respect it literally; their *absence* is part of the lesson.

## Non-negotiable guardrails (read §0 and §10)

- **No domain leakage.** Generic migration story only. No vendor/product names, no accounting model (vouchers, KID, VAT, dimensions, journal lines, budgets, assets, products, etc.), no proprietary API shapes.
- **No `MessageId`.** Idempotency key is exactly `(MigrationId, DomainEvent, Attempt)` — the Inbox composite PK (§2.2, §8.4).
- **No `Permissive`, no `Do`, no `Single()` for lookups, no currying / LINQ query-syntax, none of the excluded failure types.** See §5.4 and §10 for the exact exclusion list.
- **Duplicate, don't DRY** (§4.3): the two lock slices and two unlock slices are intentional near-duplicates. Leave them duplicated. Extract only at the 4th repetition.

## Coding rules you must both author into README and follow (§4)

Vertical slices by feature (§4.1); short names in namespaces (`Handler`, not `LockSourceHandler`) (§4.2); `Result<T>` everywhere, chain with `Then`/`ThenAsync`, async producers bind to a named variable first (§4.4); no `null` in the domain — value objects with static `Create` returning `Result<T>`, optionality via subtypes (§4.5); no input mutation (§4.6); manual `To<Target>()` mapping with `required` members (§4.7); C# 13 `extension(...)` blocks (§4.8); persistence *throws* on corrupt rows (§4.9); structured Serilog-style logging with named `EventId`s (§4.10).

Every project: `net10.0`, C# 13, `Nullable` + `ImplicitUsings` + `TreatWarningsAsErrors` enabled.

## Build order and the inner loop

Follow the suggested build order in §14: `Mt.Results` → domain value objects/aggregate/ports/events → stage handlers → persistence → simulations → transport → entry points → README + docker-compose.

**After every meaningful change, run the inner loop (§4.11):** build (warnings are errors), run the relevant tests, then self-review the diff against §4 and *delete anything not asked for*. Do not batch this up to the end.

## How I want you to work

1. Start by restating, in a short plan, the project list (§3) and the state machine (§6.2) in your own words so we confirm shared understanding before you scaffold.
2. Build one project at a time in the §14 order. After each, stop and report: what you built, build status, test results. Don't run ahead to the next project until the current one builds clean and its tests pass.
3. Prefer the spec's exact names (ports, states, events, tables). When the spec says "e.g.", pick the obvious concrete name and move on — don't invent alternatives.
4. If something in the spec seems ambiguous or self-contradictory, ask me rather than guessing — but first check whether §10's exclusions already resolve it.
5. When you think you're done, verify against §12's required coverage: an end-to-end happy path, an auto-cancel path (client without address), and a retry-then-succeed path (`FailUntilAttempt`) must all run and be tested.

## Definition of done (§14)

The happy path, the auto-cancel path, and the retry paths all run and are covered by tests, with the `InMemory` transport runnable via `docker compose up` + `dotnet run`.

---

Begin by reading `specs/workshop-eda-migration.md`, then give me the confirmation plan from step 1.
