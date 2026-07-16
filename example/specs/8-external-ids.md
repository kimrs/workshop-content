# 8 — External ids: adapters translate `MigrationId` themselves

## Context

`external-ids-pattern.md` (repo root) describes how a sibling migration app keeps a per-system
id ledger so the orchestration only ever speaks its own `MigrationId`, and each adapter
translates it into the external system's identifier at the boundary. We adopt that pattern here.

Today the domain leaks the business identity everywhere: nine adapter ports take
`OrganizationNumber`, the handlers fetch the migration mostly to dig it out, and the simulators
key their state and logs on it. After this spec, everything inside the app speaks `MigrationId`;
each external system gets its own id, minted by the caller of `POST /Start`, stored once, and
resolved only inside that system's adapter.

## The systems and their ids

Each external system knows the migration by exactly one id type. The values are supplied as the
`Start` request body and are opaque tokens to us (validated only as non-empty, ≤ 100 chars):

| System   | IdType          | Start body field    | Sample value      | Flavor                                        |
|----------|-----------------|---------------------|-------------------|-----------------------------------------------|
| `Source` | `PunchCard`     | `punchCardNumber`   | `PC-1972-0042`    | the legacy ERP files everything on punch cards |
| `Target` | `HoloCrystal`   | `holoCrystalId`     | `HOLO-7F3A-CAFE`  | the shiny cloud ERP stores tenants in crystals |
| `Portal` | `CarrierPigeon` | `carrierPigeonTag`  | `PIGEON-OSLO-9`   | completion notices are delivered by pigeon     |

Use these sample values in appsettings/README walkthroughs so retries read as
"💥 [SIM] Source lock failed for PC-1972-0042 on call 1".

## Model — `Mt.Domain/ExternalIds/`

One aggregate, all fields value objects:

```
ExternalId := (MigrationId, System, IdType, IdValue)
```

- `ExternalSystem` — closed value object: fixed singletons `Source`, `Target`, `Portal`;
  `Create(string)` returns a failure for anything else. (Not an enum — but also not the
  outcome-record shape from spec 2: these are a closed *set of values* with a string
  round-trip, so singleton value objects with a total `Create` fit §4.5 better.)
  *Implementation deviation:* named `ExternalSystem`, not the pattern doc's `System` — a type
  named `System` collides with the `System` namespace in every file that touches it.
- `IdType` — same shape: `PunchCard`, `HoloCrystal`, `CarrierPigeon`.
- `IdValue` — bounded string (non-empty, ≤ 100), validated once in `Create`.
- `ExternalId.Create(migrationId, system, type, value)` is the only constructor.

Also three tiny input value objects — `PunchCardNumber`, `HoloCrystalId`, `CarrierPigeonTag`
(each wrapping a validated string) — because `IStart` is a public domain API and never takes
bare strings (§4.5). Each knows nothing about storage; the mapping to `(System, IdType)` lives
in persistence (see Start below).

**Deviations from the pattern doc, recorded:**

- **No year-scoped ids.** This app has no per-year multiplicity, so the `IdType.Saft`
  discriminated subtype and the `Year` PK column are dropped, exactly as the doc allows.
  PK collapses to `(MigrationId, System, Name)`.
- **No orchestration cache-through port** (`IFetchDatabaseId`). Every id we need is known at
  Start; nothing is lazily fetched. No `Mt.Orchestration` project appears.
- **No LINQ-comprehension `Result` syntax.** Our `Mt.Results` chains with `Then`/`ThenAsync`
  (§4.4); the doc's `from … select` sketches translate to chains.
- `IdType` is kept even though it is 1:1 with `System` today — the pair
  `(System, IdType)` is the contract surface, and collapsing them would bake today's
  coincidence into the schema.

## Persistence

Table `ExternalIds`:

```
MigrationId  bigint  not null  FK → Migrations.Id  on delete cascade
System       text    not null
Name         text    not null   -- IdType serialized
Value        text    not null   -- IdValue serialized
IsCancelled  boolean not null   default false

Primary key: (MigrationId, System, Name)
Unique index (System, Name, Value) WHERE NOT "IsCancelled"
```

- The filtered unique index is the whole reuse story: no two *active* migrations may claim the
  same id, but after cancellation a new migration can reclaim the same punch card / crystal /
  pigeon. No purge job, no archive table.
- `ExternalIdRow` goes in `Mt.Persistence/Rows/` like the other rows.
- Rehydration (`row → ExternalId`) chains the `Create`s; a corrupt row (unknown system, unknown
  name, over-long value) surfaces as a `Result` failure per this app's corrupt-row rule —
  except that unlike `LoadMigrationAsync` (which throws `IllegalValuesInDbException`), the
  external-ids read path returns failures, matching the pattern doc. Record which rule wins
  here: **failures, not throws** — the pattern's rehydration contract is part of what we are
  adopting.
- Regenerate the `InitialCreate` EF migration (disposable database, precedent spec 6).

## Ports — `Mt.Domain/ExternalIds/`

Three ports, port messages nested inside the interfaces (spec 5), one method named
`HandleAsync` each (spec 3). The names collide with the outbox `Mt.Domain.IAdd` — that is fine,
namespaces disambiguate (precedent: the per-slice `IFetchMigration`s from spec 4).

- **`IAdd`** — `HandleAsync(Request, ct) → Result<ValueTuple>` with nested
  `Request(Id MigrationId, System System, IdType Type, IdValue Value)`. Idempotent on the PK:
  same value → success; different value → `ConflictingExternalIdFailure` (defined in
  `Mt.Domain`, never in `Mt.Results`).
- **`IFetch`** — `HandleAsync(Request, ct) → Result<ExternalId>` with nested
  `Request(Id MigrationId, System System, IdType Type)`. Reads **active rows only**
  (`WHERE NOT IsCancelled`). Not-found → `NoExternalIdFailure`, never `null`.
- **`ICancel`** — `HandleAsync(Id migrationId, ct)`: bulk `ExecuteUpdate` flipping every row of
  the migration to `IsCancelled = true`, **no** `IsCancelled` filter in the WHERE — re-cancelling
  is a no-op, not an error.

Adapters live in `Mt.Persistence/ExternalIds/` (Add, Fetch, Cancel, the row mapper — ≥ 2 files,
so the namespace rule is satisfied).

## Translation at the adapter boundary

The nine ports that today take `OrganizationNumber` take `Id migrationId` instead:

- Source-facing: `ILockSource`, `ITriggerExport`, `IUnlockSource`, `IFetchClient`,
  `IDownloadSaft`
- Target-facing: `ILockTarget`, `IUnlockTarget`, `IUploadPreSaft`, `IUploadSaft`
- Portal-facing: `INotifyCompletion` already carries `MigrationId` — unchanged interface, new
  behavior below.

Each simulated adapter (`Mt.Source`, `Mt.Target`, `Mt.Portal`) injects `ExternalIds.IFetch`,
resolves *its own* id first (`Source`+`PunchCard`, `Target`+`HoloCrystal`,
`Portal`+`CarrierPigeon`), and only then simulates. A resolution failure propagates as the
call's `Result` failure — for retryable stages that means the normal retry path, which is
correct: a missing id is indistinguishable from a broken external call.

Consequences:

- The `Simulator`s key their call counters by `(operation, external id value)` instead of
  org number, and their logs show the external id — the workshop now *sees* the translation.
- The five retryable slices' `IFetchMigration.Response.Proceed(organizationNumber)` loses its
  payload (it only fed the adapter): `Proceed` becomes an empty record there. The
  Transforms/Uploads slices keep `Proceed(aggregate)` but stop reading `OrganizationNumber`
  from it.
- `OrganizationNumber` remains the migration's business identity: on the row, in the API route,
  in the duplicate-active check at Start. It no longer flows past the command layer.
- **Logging rule update (supersedes the CLAUDE.md exception):** the simulators no longer know
  the org number at all. Flow logs carry `MigrationId`; simulator logs carry the simulator's
  own external id; `OrganizationNumber` appears in no log anywhere. Update the CLAUDE.md
  bullet when implementing.

## Start

`POST /Migration/{organizationNumber}/Start` gains a JSON body:

```json
{ "punchCardNumber": "PC-1972-0042", "holoCrystalId": "HOLO-7F3A-CAFE", "carrierPigeonTag": "PIGEON-OSLO-9" }
```

`IStart` gets a nested `Request` record (spec 5) carrying `OrganizationNumber` plus the three
input value objects; the endpoint parses all four via their `Create`s before calling the port.

The Start command, in its existing single transaction:

1. Duplicate-active check on org number (unchanged).
2. Insert the migration row.
3. Map each input to an `ExternalId` via a per-type `ToExternalId(migrationId)` extension in
   `Mt.Persistence` (`extension(...)` blocks, §4) — the input-type → `(System, IdType)` mapping
   is a storage decision and lives there.
4. **Pre-check** the three ids against active rows and return a single
   `ExternalIdConflictFailure` naming the clashing id — the API maps it to `409 Conflict`. The
   filtered unique index is the belt; the pre-check is the suspenders (a constraint violation
   is hostile to explain, a domain failure is not).
5. Insert the id rows + the three setup outbox events; commit.

## Cancellation and reuse

**Deviation from the pattern doc, recorded:** the doc flips id rows when cancellation is
*requested*. Here cancellation is a two-phase teardown — `Cancelling` still runs the unlock
stages, and those adapters must still translate. So:

- `ICancel` is called next to `ISetCancelled` in the unlock fan-in finalizers
  (`UnlocksSource`/`UnlocksTarget`), inside the same `ExecuteOnce` transaction that persists the
  terminal `Cancelled` state. Ids stay active for exactly as long as any stage might need them.
- *Implementation additions:* (1) the `Cancel` command's immediate-finalize path (cancelled
  straight from `Created`, nothing locked) also reaches terminal `Cancelled` without the unlock
  handlers — it releases the ids there, in its own transaction. (2) In every finalizer the
  ordering is notify-then-release: the Portal notification resolves its pigeon tag from *active*
  rows, so `ICancel` must run after `INotifyCompletion`.
- The `Completed` path leaves ids active — a completed migration's ids are *permanently* claimed
  (starting a second migration for the same punch card should 409; the customer already moved).

## Tests (day-one invariants, adapted from the pattern checklist)

- `System`/`IdType`/`IdValue` `Create` reject unknown/over-long input with failures.
- `IAdd`: same-value replay succeeds, different value returns `ConflictingExternalIdFailure`
  (Postgres).
- `IFetch`: hit, not-found failure, and cancelled-row invisibility (Postgres).
- Reuse-after-cancel: cancel migration A's ids, start migration B with the same values —
  succeeds; with A still active — 409 path (Postgres).
- `ICancel` is idempotent.
- Start is atomic: an id conflict rolls back the migration row and outbox rows (Postgres).
- One handler test per changed slice shape (adapter receives `Id`, `Proceed` is empty).
- Simulator test: lock failure log/counter is keyed by the punch-card value.
