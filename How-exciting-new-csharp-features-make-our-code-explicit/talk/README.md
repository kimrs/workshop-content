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

## Hosting (GitHub Pages)

The deck is served at **https://solstad.dev/workshop-content/** from this repo's own
GitHub Pages. `solstad.dev` is the custom domain of the `kimrs.github.io` user site, so
every project repo with Pages enabled publishes under `solstad.dev/<repo-name>/`.

`.github/workflows/deploy-slides.yml` rebuilds and redeploys on every push to `main`
that touches `talk/`, using the official Pages actions — no tokens or secrets. The base
path is derived from the repo name, so a copy of this repo under a new name (the
per-talk workflow) publishes at `solstad.dev/<new-name>/` without edits. The only
per-repo setup is enabling Pages once: **Settings → Pages → Source: GitHub Actions**.

> The poll backend can **not** run on Pages (static hosting only) — see the poll runbook
> below for where it lives.

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

The backend runs **on the presentation laptop** (`talk/compose.yml`), exposed through a
Cloudflare quick tunnel. The tunnel matters for two reasons: venue Wi-Fi usually has
client isolation (phones can't reach the laptop's LAN IP), and the poll calls must be
HTTPS or the browser blocks them as mixed content. Present from the local dev deck
(`npm run dev`) — you need it for the live demos anyway; the Pages deck is the
shareable copy.

### One-time setup (do at home, survives the tunnel restarting)

1. **Start the backend**: `docker compose up -d` in `talk/`. Poll definitions and your
   admin account live in the `polls-data` volume, so this is once.
2. **First run**: open `http://localhost:8080/admin/`, the wizard creates your
   presenter account.
3. **Create the poll**: slug `csharp15` (must match the `<PollQr slug>` in
   `slides.md` — change both if you pick another), one single-choice question
   "Which one is correct?" with options `closed` / `union` / `how should I know?`.
4. **Wire the poll identity**: in the admin question editor click **Copy snippet** and
   copy its `pollId`/`questionId` into `talk/.env.local` (`VITE_POLL_ID` /
   `VITE_POLL_QUESTION_ID`; `VITE_POLL_SLUG` matches step 3). `components/Poll.vue`
   feeds these to both `<Poll />` tags in `slides.md` — nothing to paste into the deck,
   and the ids stay out of the committed slides. `.env.local` is git-ignored;
   `talk/.env.example` is the committed template.

### Before the talk (tunnel URL changes every time)

1. `docker compose up -d` (if the laptop rebooted), then
   `cloudflared tunnel --url http://localhost:8080`
   (`brew install cloudflared` once). It prints an `https://….trycloudflare.com` URL.
2. Put that URL in `talk/.env.local` as `VITE_POLL_SERVER=https://….trycloudflare.com`
   (copy `talk/.env.example` the first time). `setup/main.ts` injects it into the deck
   config, so **`slides.md` is never touched at startup**. `.env.local` is git-ignored,
   so the ephemeral URL can't be committed. Set it *before* `npm run dev` — Vite reads
   `.env` files at server start (change it later → restart the dev server).
3. Sanity check from your **phone on mobile data**: open
   `https://….trycloudflare.com/admin/` — that's also the tally view you'll keep open.

### On the day

- In the running deck, click the poll **sign-in button in the Slidev toolbar**
  and enter your admin credentials (it mints a deck token). Without it the deck
  can't open questions and every phone stays on "Waiting for question to open".
- The question **opens automatically when the QR slide shows** (a hidden
  `PollResults` panel on that slide does the activation — `PollQr` itself is
  display-only). Fallback: flip it to **Active** from the admin UI.
- Keep the **admin UI open on your phone** — it's your private live tally and
  the manual Active/Closed switch if the automatic path misbehaves.
- Advancing to "The verdict" reveals the live tally; the question closes for
  good once you move past that slide.
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
