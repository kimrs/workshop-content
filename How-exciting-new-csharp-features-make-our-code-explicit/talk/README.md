# How Exciting New C# Features Make Our Code Explicit — slide deck

[Slidev](https://sli.dev) deck for the `closed` / `union` talk. Speaker notes live
inside `slides.md` as the HTML comment at the end of each slide — they show up in
presenter view, not on screen.

## Run it

```bash
cd talk
npm install
npm run dev        # opens the deck at localhost:3030
```

- **Presenter view** (notes + next slide + timer): open `localhost:3030/presenter`
- **Export to PDF**: `npm run export` (needs `npx playwright install chromium` once)

> **If the build fails with "Cannot find native binding"**: that's a known npm bug
> with optional dependencies ([npm/cli#4828](https://github.com/npm/cli/issues/4828)).
> Fix: `rm -rf node_modules package-lock.json && npm install`. Run the deck once on
> the presentation machine *before* the day.

## Before walking on stage

1. From `How-exciting-new-csharp-features-make-our-code-explicit/`: `dotnet build demo/Demo.slnx`
   (and `dotnet build ../example/Workshop.sln` if you demo the example app) — pre-warm so live demos don't stall on restore.
2. `dotnet run --project demo/Mt.Runner` — confirm the demo crashes with
   `ArgumentOutOfRangeException` (the defensive `_ => throw` firing at runtime).
3. Have `demo/Mt.Domain/ILockSource.cs` open in the editor for the `abstract → closed` live edit.
4. After the CS9382 demo, **revert** `closed` back to `abstract` — the checked-in
   demo stays in the "before" state so the repo always compiles.

## Poll runbook (slidev-polls)

The "Which one is correct?" slide runs a live audience poll via
[slidev-polls](https://github.com/asm0dey/slidev-polls): audience scans the QR,
votes on their phones, and the next slide ("The verdict") reveals the live tally.

### One-time setup

1. **Deploy the backend** somewhere the audience's phones can reach (Fly.io,
   a small VPS, …). Single container, no Postgres needed:

   ```yaml
   # compose.yml
   services:
     backend:
       image: ghcr.io/asm0dey/slidev-polls-backend:latest
       environment:
         SPRING_DATASOURCE_URL: 'jdbc:h2:file:/data/polls;DATABASE_TO_LOWER=TRUE;CASE_INSENSITIVE_IDENTIFIERS=TRUE'
         SPRING_DATASOURCE_USERNAME: sa
         SPRING_DATASOURCE_PASSWORD: ''
         SPRING_PROFILES_ACTIVE: prod   # only with TLS in front
       volumes:
         - polls-data:/data
       ports:
         - "8080:8080"
       restart: unless-stopped
   volumes:
     polls-data:
   ```

2. **First run**: open `https://<your-host>/admin/`, the wizard creates your
   presenter account.
3. **Create the poll**: slug `csharp15` (must match the `<PollQr slug>` in
   `slides.md` — change both if you pick another), one single-choice question
   "Which one is correct?" with options `closed` / `union` / `how should I know?`.
4. **Wire the deck**: in `slides.md` frontmatter, replace
   `pollServer: https://polls.example.com` with your real URL. In the admin
   question editor click **Copy snippet** and paste it over the placeholder
   `<PollResults …/>` tag on "The verdict" slide.

### On the day

- In the running deck, click the poll **sign-in button in the Slidev toolbar**
  and enter your admin credentials (it mints a deck token).
- Keep the **admin UI open on your phone** — it's your private live tally
  *and* the button that flips the question to **Active** while the QR slide is up.
- Advancing to "The verdict" closes the question and animates the tally on screen.
- **Network fallback** (scripted in the slide notes): hands vote, then skip
  "The verdict" slide.

## Live-demo cheat sheet

| Beat | Command / edit | Expected |
|---|---|---|
| Other assemblies, before | `dotnet run --project demo/Mt.Runner` | Distinct Comics' `Throttled` hits the defensive `_ => throw` → **runtime** `ArgumentOutOfRangeException` |
| Other assemblies, after | `abstract` → `closed` in `demo/Mt.Domain/ILockSource.cs`, `dotnet build demo/Mt.Runner` | **compile-time** `CS9382` in **Mt.DistinctComics** |
| Insiders | add `public sealed record Paused : Response;` to `../example/src/Mt.Domain/Steps/LocksSource/IFetchMigration.cs`, build | `CS8509` naming `Paused` at `Handler.cs` |
| Shared data | add `public sealed record Failed : Request;` to `../example/src/Mt.Domain/INotifyCompletion.cs`, build | `CS1729` — case can't exist without `MigrationId` |

Every expected output is also on the slide as a click-reveal, so a broken demo
never strands you.
