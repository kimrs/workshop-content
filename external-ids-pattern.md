# Spec: The ExternalIds pattern

**Audience:** an engineer (or Claude) building an integration/ETL/migration app that talks to
multiple external systems and needs to remember "for this migration, what is its id over in
system X?". This spec captures the shape of the pattern as implemented here so it can be lifted
into a similar app. It is portable — no code from this app needs to come with it — but it does
assume a domain layer with a `Result<T>` monad, value objects, and hexagonal ports/adapters.

## Problem

An orchestration writes to several external systems on behalf of one logical unit of work (here
called a *Migration*). Each of those systems mints or accepts its own identifier for the same
logical thing:

- The source ERP knows the customer by an `Fnumber` and a `DatabaseId`.
- The target ERP knows the same customer by a `CompanyKey`.
- The project-management tool knows it by a `WorkItemId`.
- The validation service knows it by a `CustomerNumber`.
- Files exported to S3 are named by the source system and each carries a fiscal `Year`.

Handlers running later in the pipeline (fetch pre-SAF-T, upload post-SAF-T, roll back, …) need to
resolve "what is the CompanyKey / DatabaseId / … for migration N?" repeatedly and cheaply. Two
naive shapes are wrong:

1. **Columns on the Migration row** — grows every time a new system is integrated, mixes
   heterogeneous keys with unrelated lifetimes, and can't hold multiplicity (one migration owns
   *many* SAF-T files, one per year).
2. **A per-system table** — you write the same insert/fetch/cancel/reuse-after-cancel plumbing
   over and over, and cross-system uniqueness ("no two active migrations share a CompanyKey") has
   nowhere to live.

The pattern below solves both with a single table plus a small set of value objects.

## Model

One aggregate:

```
ExternalId := (MigrationId, System, IdType, IdValue)
```

Each field is a **value object**, not a primitive:

- `System` — closed enum-style: fixed `static readonly` members (`Connect`, `Complete`, `Pmt`,
  `Validate`). `Create(string)` returns `OutOfBoundsFailure` for anything unknown.
- `IdType` — closed enum-style (`Fnumber`, `DatabaseId`, `CompanyKey`, `CustomerNumber`,
  `ClientId`, `VoucherId`, `WorkItemId`), plus a discriminated `Saft` subtype (see "Year-scoped
  ids" below).
- `IdValue` — a bounded string (100 chars here); validated once at construction; `Create` returns
  `Result<IdValue>`.
- `MigrationId` — the FK; a value object, not a raw `long`.

`ExternalId` itself is a `record` with a `Create(migrationId, system, type, value) → Result<ExternalId>`
factory. There is no other way to build one.

### Why closed enums for `System` and `IdType`

Both are the *contract surface* between the orchestration and the outside world. Anything else
that appears in a `System` or `IdType` column is corrupted data, not a new case to handle at
runtime. Modeling them as closed value objects means:

- Deserialization from the DB (`Create(string)`) is total and returns a `Result` — no silent
  string round-trip.
- Handlers written today can pattern-match against the fixed set with no default branch.
- Adding a new system or id type is an explicit edit + PR review, not a data-driven surprise.

## Persistence

One table, `ExternalIds`, with these columns:

```
MigrationId  bigint     not null  fk → Migrations.Id  on delete cascade
System       text       not null
Name         text       not null   -- IdType.Value serialized as a string
Value        text       not null   -- IdValue serialized
Year         bigint/int not null   default 0
IsCancelled  boolean    not null   default false

Primary key: (MigrationId, System, Name, Year)
Unique index (System, Name, Value)  WHERE IsCancelled = false
Foreign key MigrationId → Migrations.Id  on delete cascade
```

Three details carry weight; each one solves a specific problem.

### 1. `Year` is in the PK, defaults to 0

Almost every id type has exactly one row per `(MigrationId, System, Name)`. But SAF-T files are
per-year: one migration owns one row *per fiscal year*. Putting `Year` in the PK admits that
multiplicity without a second table and without nullable columns.

Non-year-scoped rows carry `Year = 0` as the "not applicable" sentinel. The sentinel is a
**storage concern only**; the domain never sees it — the mapping layer decides whether to
populate `Year` from an `IdType.Saft` instance, or leave it 0.

### 2. Unique index is filtered on `IsCancelled = false`

Two constraints have to hold simultaneously:

- **No two *active* migrations** can claim the same `(System, Name, Value)` — e.g. two live
  migrations pointing at the same `CompanyKey` would corrupt the target ERP.
- **Reuse after cancellation** must work — if migration A was cancelled, its ids should be
  reclaimable by a new migration B for the same customer.

A plain unique index breaks reuse. Dropping cancelled rows breaks audit. A filtered unique index
(`WHERE IsCancelled = false`, PostgreSQL syntax; the equivalent in SQL Server is a filtered
index, in others a computed uniqueness or partial index) gives you both. Every read path that
cares about "current" ids must include `WHERE IsCancelled = false`.

### 3. FK cascades from `Migrations`

The `ExternalIds` rows have no life independent of their Migration. If the migration row is
deleted, the ids go with it. Cancellation is a *soft* delete on the id rows, not the migration;
migrations get their own state machine and are not deleted.

### DbRow

Mirror the columns in a persistence-side record (not the domain record) so the domain type
doesn't leak the storage sentinel:

```csharp
internal record DbRow(
    long MigrationId,
    string System,
    string Name,
    string Value,
    uint Year = 0,
    bool IsCancelled = false);
```

Keep `Year` and `IsCancelled` defaulted so existing call-sites that don't care compile unchanged.

## Rehydration (`DbRow → ExternalId`)

The mapper has to rebuild the closed value objects from raw strings. Every step returns
`Result<T>`; a corrupted row surfaces as a domain failure, not an exception:

```csharp
internal static Result<ExternalId> ToDomain(this DbRow row) =>
    from migrationId in Id.Create(row.MigrationId)
    from system      in System.Create(row.System)
    from type        in row.ToIdType()          // handles Saft specially
    from value       in IdValue.Create(row.Value)
    select ExternalId.Create(migrationId, system, type, value);
```

For SAF-T rows, `ToIdType` reads the `Year` column and constructs `IdType.Saft.Create(year)`;
`IdType.Create(string)` intentionally does **not** accept `"SAF-T"`, because a bare Name string
can't supply a year. This forces the SAF-T path to go through the column.

Going the other way (`ExternalId → DbRow`), an extension on `IdType` computes the storage year:

```csharp
internal static uint YearKey(this IdType type)
    => type is IdType.Saft saft ? saft.Year.Value : 0u;
```

Kept in the persistence layer, not the domain, because the `0` sentinel is a storage concern.

## Year-scoped ids: the `IdType.Saft` trick

The interesting case that shaped this design. SAF-T file names are minted by the source system
(one per fiscal year) and later consumed by the migration pipeline. They are cross-system
identifiers with per-year multiplicity — exactly what `ExternalIds` is for, if you allow one
`IdType` to carry a parameter.

Shape:

```csharp
public class IdType : IValueObject<string, IdType>
{
    public const string SaftName = "SAF-T";

    public static readonly IdType Fnumber = new("Fnumber");
    // … other fixed singletons …

    public sealed class Saft : IdType
    {
        public Year Year { get; }
        private Saft(Year year) : base(SaftName) => Year = year;
        public static Result<Saft> Create(Year year) => new Saft(year);

        // Base relies on singleton reference identity; Saft instances are minted per-year,
        // so it MUST provide value equality over the year for record comparisons to be correct.
        public override bool Equals(object? obj)
            => obj is Saft other && Year.Value == other.Year.Value;
        public override int GetHashCode() => HashCode.Combine(SaftName, Year.Value);
    }

    public static Result<IdType> Create(string value) => value switch
    {
        "Fnumber" => Fnumber,
        // … fixed cases …
        _         => new OutOfBoundsFailure()
    };
}
```

Three subtle rules:

1. **`Saft` shares the `Value` token** (`"SAF-T"`) with every other Saft instance — that's what
   goes in the `Name` column. The differentiator is the `Year` column, not the token.
2. **`Saft` overrides `Equals`/`GetHashCode`**. The fixed singletons compare by reference (there
   is only ever one `IdType.Fnumber` instance in the process). `Saft` is minted per-year, so
   reference identity would fail for two `Saft(2026)` instances; `ExternalId` is a `record`, so
   this would silently break record equality.
3. **`IdType.Create(string)` does not accept `"SAF-T"`**. The string path can't supply a year;
   the only ways to get an `IdType.Saft` are `Saft.Create(year)` (mint path) or the rehydration
   mapper reading the `Year` column.

If your similar app has no year-scoped case, drop the `Saft` subtype and the `Year` column and
you have a plain external-id table. But if you have *any* variant identifier that carries an
extra dimension (year, month, region), this discriminated-subtype shape scales cleanly.

## Ports (domain-side interfaces)

Four handler-visible ports, each with its own vertical slice folder:

```
Mt.Domain/ExternalIds/
    Adds/IAdd.cs              — insert-or-idempotent-no-op; conflict on value mismatch
    Fetches/IFetch.cs         — single-row lookup by (MigrationId, System, Name)
    Fetches/IFetchDatabaseId.cs — orchestration-level "get or lazily fetch from source"
    Cancellations/ICancel.cs  — mark all rows of a migration IsCancelled = true
```

### `IAdd` semantics

```csharp
public interface IAdd
{
    Task<Result<ValueTuple>> HandleAsync(Command command, CancellationToken cancellationToken);

    record Command
    {
        public Id MigrationId { get; }
        public System System { get; }
        public IdType Type { get; }
        public IdValue Value { get; }
        // private ctor + static factory returning Result<Command>
    }
}
```

Adapter (`Add.cs`) is idempotent on the PK:

- If **no row** exists for `(MigrationId, System, Type, YearKey)`: insert.
- If a row exists with the **same** `Value`: return success (idempotent replay).
- If a row exists with a **different** `Value`: return `ConflictingValueFailure(type, attempted,
  existing)`.

`ConflictingValueFailure` should extend a base failure type that your event pipeline recognizes
as "cancel the migration" (here: `MigrationCancellingFailure`). A conflict at add-time means the
outside world has diverged from what we recorded and downstream state is compromised.

### `IFetch` semantics

Single-row lookup by `(MigrationId, System, Name)`. Query is a value object with a factory
returning `Result<Query>` so callers can build it fluently:

```csharp
var query = IFetch.Query.Create.ToResult()
    .Apply(migrationId)
    .Apply(System.Connect)
    .Apply(IdType.Fnumber);

var externalId = await query.ThenAsync(q => fetch.HandleAsync(q, cancellationToken));
```

Not-found returns a domain failure (`NoExternalIdFailure`) that extends the migration-cancelling
base. Rehydration failures bubble as the same `Result<T>` failure — a poisoned row shows up as a
domain problem, never a thrown exception.

For collection reads (e.g. "all SAF-T rows for this migration"), add a **separate port** rather
than overloading `IFetch`. Here that's `IFetchSaftIds` returning
`Result<IReadOnlyList<ExternalId>>` and filtering `IsCancelled = false`. Keeps each port narrow.

### `IFetchDatabaseId` — the cache-through pattern

Sits in the orchestration layer (`Mt.Orchestration`, not `Mt.Domain`), because it composes two
adapters:

```csharp
internal class FetchDatabaseId(
    IFetch fetchExternalId,
    IAdd addExternalId,
    ISourceSystemFetchId fetchFromSource) : IFetchDatabaseId
{
    public async Task<Result<ExternalId>> HandleAsync(Id migrationId, CancellationToken ct)
    {
        // 1) Try the persistence cache first.
        var cached = await BuildQuery(migrationId, IdType.DatabaseId)
            .ThenAsync(q => fetchExternalId.HandleAsync(q, ct));
        if (cached is Result<ExternalId>.Completed { Value: var hit }) return hit;

        // 2) Miss: fetch the Fnumber we stored at Start, use it to query the source system.
        var fetched = await BuildQuery(migrationId, IdType.Fnumber)
            .ThenAsync(q => fetchExternalId.HandleAsync(q, ct))
            .Then(row => FNumber.Create(row.Value.Value))
            .ThenAsync(fn => fetchFromSource.HandleAsync(fn, ct));

        // 3) Persist the answer so we never re-fetch.
        return await fetched
            .Then(dbId => IdValue.Create(dbId.ToString()))
            .Then(v => IAdd.Command.Create(migrationId, System.Connect, IdType.DatabaseId, v))
            .ThenAsync(cmd => addExternalId.HandleAsync(cmd, ct))
            .Then(_ => /* return the new ExternalId */);
    }
}
```

Rules of thumb for this shape:

- **Cache-through, not read-through**: the port hides the miss path. Handlers just say "give me
  the DatabaseId" and never see the source-system fallback.
- **Lives in the orchestration layer** because it depends on both persistence (`IAdd`, `IFetch`)
  and an external-system adapter port. Neither of those is a domain concern in isolation.
- **The miss path uses another `ExternalId`** as its input (the `Fnumber` recorded at Start).
  This is how you avoid stashing raw external inputs in memory — everything a later handler needs
  gets written to `ExternalIds` at Start time.

### `ICancel` semantics

```csharp
public interface ICancel { Result<ValueTuple> Handle(Id migrationId); }
```

Bulk `ExecuteUpdate` sets `IsCancelled = true` for every row of the migration. **No filter on
`IsCancelled` in the WHERE** — re-cancelling a cancelled migration is a no-op, not an error.
Called at every cancellation site alongside the migration-state transition, in the same
transaction where possible.

## Bootstrapping at Start

The `Start` command is the single place where multiple external ids appear at once. It has to
insert the migration row, all of its known external ids, and the initial outbox event(s) —
**all in one transaction**. If the unique index rejects any id, the whole thing rolls back.

Sketch:

```csharp
await using var tx = await db.BeginTransactionAsync(ct);

var migrationId = await AddMigrationAsync(db, request.OrganizationNumber, ct);

var externalIds = from id in migrationId
    from a in request.Fnumber.ToExternalId(id)          // one extension method per
    from b in request.CompanyKey.ToExternalId(id)       // input value object; each returns
    from c in request.CustomerNumber.ToExternalId(id)   // Result<ExternalId>
    from d in request.WorkItemId.ToExternalId(id)
    select new[] { a, b, c, d };

var saved = await externalIds
    .Then(ids => AddExternalIds(db, ids))       // pre-check active-row conflicts → ExternalIdConflict
    .Then(_ => migrationId)
    .Then(id => AddOutbox(db, id, DomainEvents.LockRequested, time.GetUtcNow())
        .Then(_ => id))
    .Then(id => AddOutbox(db, id, DomainEvents.NextRequested, time.GetUtcNow()))
    .ThenAsync(_ => SaveChangesAsync(db, ct));

var committed = await saved.ThenAsync(_ => tx.CommitAsync(ct));
```

Two conventions to steal:

- **`ToExternalId(migrationId)` per input value object.** Each input primitive (Fnumber,
  CompanyKey, …) knows which `System`/`IdType` it maps to; expressing that as a per-type
  extension keeps `Start` readable and gives you one place to change the mapping. Lives in the
  persistence layer, not the domain, because the mapping is a storage decision:

  ```csharp
  extension(CompanyKey companyKey)
  {
      public Result<ExternalId> ToExternalId(Id migrationId)
          => IdValue.Create(companyKey.ToString())
              .Then(x => ExternalId.Create(migrationId, System.Complete, IdType.CompanyKey, x));
  }
  ```

- **Pre-check conflicts before insert.** `AddExternalIds` LINQ-scans the pending rows against the
  active-row set, returns a specific `ExternalIdConflict` failure (not a DB constraint
  violation), so the API layer can map it to HTTP `409 Conflict` cleanly. The unique index is the
  belt; the pre-check is the suspenders — a DB error is hostile to explain, a domain failure is
  not.

## Cancellation and reuse

When a migration is cancelled (from any state):

1. Transition the migration record to a terminal `Cancelled` state.
2. Call `ICancel.Handle(migrationId)` in the **same transaction** — every id row flips to
   `IsCancelled = true`.
3. The filtered unique index now permits a new migration to claim the same `(System, Name,
   Value)` triples.

That is the entire reuse story. There is no purge job, no archive table, no "if the migration is
Cancelled, ignore its ids in this query" logic — the filtered index enforces it declaratively.
Any code that reads external ids to *use them* must include `WHERE IsCancelled = false`
(e.g. `FetchSaftIds`); any code that reads them for *audit* leaves the filter off.

Handlers running mid-pipeline follow the same rule: check "is the migration cancelled?" once at
handler entry, and if so, log-and-skip. Don't try to reason about half-cancelled state.

## Rules of the pattern (checklist for the port)

If you're building this from scratch in a similar app, these are the invariants worth writing
tests for on day one:

- [ ] `System` and `IdType` are closed value objects; unknown strings from the DB return a
      `Result` failure, not a thrown exception.
- [ ] `IdValue` validates length once at construction. Public APIs never take `string`.
- [ ] Persistence PK includes any per-instance dimension (`Year` here). If your app has none,
      collapse to `(MigrationId, System, Name)`.
- [ ] Unique index on `(System, Name, Value)` is **filtered** on `IsCancelled = false`.
- [ ] `IAdd` is idempotent on same value, fails with a distinguishable failure type on value
      mismatch (so downstream can cancel).
- [ ] `IFetch` returns a domain failure on not-found (not `null`, not an exception).
- [ ] `ICancel` is a bulk `ExecuteUpdate` set-IsCancelled=true, no `WHERE IsCancelled = false`.
- [ ] Rehydration is a `Result`-returning mapper; a corrupted row is a domain failure, not an
      exception.
- [ ] Cancellation runs in the same transaction as the migration-state transition.
- [ ] "Get-or-lazily-fetch" (like `IFetchDatabaseId`) lives in the orchestration layer, above
      both persistence and the external-system adapter.

## Reference implementation

Files in this repo, if you want to pattern-match:

- Domain: `migration-tool/Mt.Domain/ExternalIds/` (aggregate, value objects, port interfaces)
- Persistence: `migration-tool/Mt.Persistence/ExternalIds/` (DbRow, EntityTypeConfiguration,
  Add/Fetch/Cancel adapters, rehydration mapper)
- Orchestration: `migration-tool/Mt.Orchestration/ExternalIds/Fetches/FetchDatabaseId.cs`
- Bootstrapping at Start: `migration-tool/Mt.Persistence/Commands/Start.cs` and the
  `ToExternalId` mappers in `migration-tool/Mt.Persistence/Commands/Extensions.cs`
- The `IdType.Saft` year-scoped case: `migration-tool/Mt.Domain/ExternalIds/IdType.cs` and the
  companion spec `saftfiles-as-external-ids.md`
