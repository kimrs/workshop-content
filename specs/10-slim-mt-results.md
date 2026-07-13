# 10 — Slim Mt.Results: delete the unused surface

A focused review of `Mt.Results` (2026-07-14) traced every public member to its call sites.
Several spec-1 §5 members turned out to have **zero production consumers**: they are exercised
only by `Mt.Results.Tests` itself. Per the project ethos ("the value is in the patterns, not
breadth"), this spec deletes them and records the deviations from spec 1 §5.1–§5.3.

## Decisions

| # | Decision | Why |
|---|----------|-----|
| D1 | Delete `IResult<out T>` (with nested `IFailed`/`ICompleted`), the `Then(Func<T, IResult<TRes>>)` overload, and `ResultConversions.ToResult()` — **deviation from spec 1 §5.1/§5.2** | Nothing implements the interface except `Result<T>`; no consumer anywhere declares an `IResult<T>`; the covariance it exists for is never exploited. The `Then` overload and `ToResult()` have zero call sites and only exist to serve the interface. Removing it also ends the name clash with ASP.NET's `IResult` in `Mt.Api` |
| D2 | Delete the warnings channel: `Result<T>.Warnings`, `WithWarnings` (both overloads), internal `PrependWarnings`, and the warning-flow plumbing in every combinator — **deviation from spec 1 §5.1** | No production code ever attaches or reads a warning; only the library's own tests do. The feature taxes every combinator with `{ Warnings = … }` plumbing, and no `Severity.Warning` failure was ever constructed — the tests fake warnings with `Severity.Error` failures |
| D3 | Delete `Severity` and `Failure.Severity`; `Failure` becomes `abstract record Failure(string Message)` — **deviation from spec 1 §5.3** | Severity is write-only: every subtype hard-codes it and nothing anywhere reads it (no logging, no HTTP mapping, no retry logic branches on it). Wiring it into logging instead was considered and rejected as speculative |
| D4 | Delete `ResultUtilities` (`WrapExceptions`/`WrapExceptionsAsync`); `ExceptionalFailure` becomes a per-simulator `OperationThrew(string Message)` in `Mt.Source`/`Mt.Target` — **deviation from spec 1 §5.2/§5.3 and from the review** | `WrapExceptions` has zero production call sites (its docstring claimed adapters use it; they don't — persistence deliberately *throws* on corrupt rows, spec 1 §4). The review claimed `ExceptionalFailure` was unused too; that was wrong — the simulators return it for `OperationFailure.Throw` (spec 1 §7). With its constructing combinator gone, spec 2 D3 says it cannot stay in Mt.Results, so the simulators get their own failure, mirroring their existing `OperationFailed` near-duplicates. The wrapped `Exception` payload is dropped: nothing ever read it |
| D5 | Delete `Flatten` — **deviation from spec 1 §5.2** | Zero production call sites |
| D6 | `Result<T>.Failed` guards against an empty failure list (throws `ArgumentException`) | A "failed" result with zero failures was representable; `HttpResults.ToHttpResult` indexes `Failures[0]` and `IsFailed` promises a non-empty array. Corrupt construction is a bug, not control flow, so throwing is right (spec 1 §4) |
| D7 | Merge `Validator.cs` into `FailWhen.cs`; delete `Validator.cs` — **deviation from spec 1 §5.2's file split** | The file contained only the `Result<T>` overload of `FailWhen`; nothing named "Validator" existed as a concept. One file per concept, and the concept is `FailWhen` |
| D8 | Keep `IsFailed`/`IsCompleted` out-param as `Failure[]` (review item 8 **rejected**) | Handlers return the failures directly (`return fetchFailures;`) via the `Failure[] → Result<T>` implicit operator. C# forbids user-defined conversions from interface types, so an `IReadOnlyList<Failure>` out-param would break every such site |
| D9 | Remove the redundant `using Mt.Results;` from files inside `namespace Mt.Results` | Spec 2 item 6 already called for this; seven files still had it |

## 1. Mt.Results — deletions

1. `Result.cs`: delete `IResult<out T>` and `ResultConversions`; delete `Warnings`,
   `WithWarnings` ×2, `PrependWarnings`; guard `Failed` against an empty failure list.
2. `Then.cs`: delete the `Func<T, IResult<TRes>>` overload; strip warning plumbing.
3. `Map.cs`: strip warning plumbing.
4. Delete `Flatten.cs`, `Validator.cs`, `ResultUtilities.cs`, `ExceptionalFailure.cs`,
   `Severity.cs`.
5. `FailWhen.cs`: absorb the `Result<T>` overload from `Validator.cs`.
6. `Failure.cs`: drop the `Severity` positional parameter.
7. `NotFoundFailure`, `DuplicateFailure`, `UnexpectedNullFailure`, `ValidationFailure`:
   drop the `Severity` argument.

## 2. Ripple into other projects

1. Failure subtypes lose the `Severity` argument (no other change):
   - `src/Mt.Source/OperationFailed.cs`, `src/Mt.Target/OperationFailed.cs`
   - `src/Mt.Domain/Migrations/MigrationHasIncorrectState.cs`
   - `src/Mt.Domain/ExternalIds/NoExternalIdFailure.cs`,
     `ExternalIdConflictFailure.cs`, `ConflictingExternalIdFailure.cs`
   - `src/Mt.Domain/Stages/OutOfRetriesFailure.cs`
2. D4: add `src/Mt.Source/OperationThrew.cs` and `src/Mt.Target/OperationThrew.cs`; the
   simulators return it (instead of `ExceptionalFailure`) when `OperationFailure.Throw` is set.

## 3. Tests

- Delete `WarningPropagationTests.cs`.
- `CombinatorTests.cs`: delete the `Flatten` and `WrapExceptions`/`WrapExceptionsAsync` tests;
  add one test asserting the empty-failures guard throws.
- `Mt.Source.Tests/SimulationTests.cs`: the Throw-mode test asserts `OperationThrew`.

## 4. Verification

- `dotnet build` (warnings as errors) and the non-Postgres test projects pass.
- `grep -rn "IResult<\|Warnings\|Severity\|ExceptionalFailure\|Flatten\|WrapExceptions" src tests --include="*.cs"`
  returns no hits outside `Mt.Api`'s ASP.NET `IResult` usage.
