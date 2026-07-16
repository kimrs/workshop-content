# workshop-content

New C# constructs — `closed` hierarchies and `union` types (.NET 11 preview 6) — explored
through a talk, a slide-sized demo, and a real example app whose switches never need a discard.

## Layout

```
How-exciting-new-csharp-features-make-our-code-explicit/
├── talk/    Slidev deck + speaker notes + poll runbook (see talk/README.md)
└── demo/    Slide-sized solution for the live demos (Demo.slnx):
             Mt.Domain (the port + handler), Mt.DistinctComics (the "other
             assembly" that derives Throttled), Mt.Runner (console runner)
example/     The full migration app the talk's patterns come from: an event-driven
             pipeline migrating heroes from Distinct Comics to Marble
             (Workshop.sln — hexagonal architecture, outbox/inbox, bounded retries)
```

## Quick start

```bash
# the example app
dotnet test example/Workshop.sln          # persistence/e2e suites need Docker

# the demo used live on stage
dotnet run --project How-exciting-new-csharp-features-make-our-code-explicit/demo/Mt.Runner

# the deck
cd How-exciting-new-csharp-features-make-our-code-explicit/talk && npm install && npm run dev
```

Everything targets **.NET 11 preview 6** with `LangVersion=preview` (see `Directory.Build.props`).
