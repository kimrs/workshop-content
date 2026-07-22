#!/usr/bin/env bash
#
# present.sh — one command to launch the talk (deck + live poll), and a guided
# first-time setup. Replaces the multi-step "Poll runbook" dance in README.md.
#
#   ./present.sh          Launch: poll backend + Cloudflare tunnel + dev deck
#   ./present.sh setup    First-time: install, create poll, wire the ids
#   ./present.sh warm     Pre-warm the dotnet live-demo builds (do before stage)
#
# Everything the script writes lands in talk/.env.local (git-ignored), so nothing
# ephemeral (the per-talk tunnel URL, your poll ids) ever gets committed.

set -euo pipefail

# --- locate ourselves; run from the talk/ dir regardless of caller's cwd -------
TALK_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$TALK_DIR"

ENV_FILE="$TALK_DIR/.env.local"
ENV_EXAMPLE="$TALK_DIR/.env.example"
BACKEND_URL="http://localhost:8080"
ADMIN_URL="$BACKEND_URL/admin/"

TUNNEL_PID=""
TUNNEL_LOG=""

# --- pretty output -------------------------------------------------------------
info()  { printf '\033[1;34m•\033[0m %s\n' "$*"; }
ok()    { printf '\033[1;32m✓\033[0m %s\n' "$*"; }
warn()  { printf '\033[1;33m!\033[0m %s\n' "$*"; }
die()   { printf '\033[1;31m✗\033[0m %s\n' "$*" >&2; exit 1; }

# --- small helpers -------------------------------------------------------------
have() { command -v "$1" >/dev/null 2>&1; }

# docker compose v2 subcommand, falling back to the legacy binary.
dc() {
  if docker compose version >/dev/null 2>&1; then docker compose "$@"
  elif have docker-compose;              then docker-compose "$@"
  else die "Docker Compose not found. Install Docker Desktop."; fi
}

open_url() {
  if   have open;     then open "$1"      >/dev/null 2>&1 || true
  elif have xdg-open; then xdg-open "$1"  >/dev/null 2>&1 || true
  fi
}

# set_env KEY VALUE — upsert a KEY=VALUE line in .env.local (portable, no sed -i).
set_env() {
  local key="$1" val="$2" tmp
  touch "$ENV_FILE"
  tmp="$(mktemp)"
  grep -v "^${key}=" "$ENV_FILE" > "$tmp" || true
  printf '%s=%s\n' "$key" "$val" >> "$tmp"
  mv "$tmp" "$ENV_FILE"
}

# get_env KEY — echo the value of KEY from .env.local, or empty.
get_env() {
  [ -f "$ENV_FILE" ] || return 0
  grep -E "^$1=" "$ENV_FILE" | tail -1 | cut -d= -f2- || true
}

# Wait until the poll backend answers on :8080 (H2 boot takes a few seconds).
wait_for_backend() {
  info "Waiting for the poll backend on $BACKEND_URL ..."
  local i
  for i in $(seq 1 60); do
    if curl -sf -o /dev/null "$ADMIN_URL"; then ok "Poll backend is up."; return 0; fi
    sleep 1
  done
  die "Poll backend did not come up. Check: $(printf '%q' "docker compose logs backend")"
}

# --- teardown ------------------------------------------------------------------
cleanup() {
  if [ -n "$TUNNEL_PID" ] && kill -0 "$TUNNEL_PID" 2>/dev/null; then
    info "Closing Cloudflare tunnel ..."
    kill "$TUNNEL_PID" 2>/dev/null || true
  fi
  [ -n "$TUNNEL_LOG" ] && rm -f "$TUNNEL_LOG"
  # The poll backend is left running on purpose: `restart: unless-stopped` keeps
  # your poll data and admin account alive between talks. Stop it with:
  #   docker compose down
}
trap cleanup EXIT INT TERM

# Start a Cloudflare quick tunnel, capture its trycloudflare URL, return via stdout.
start_tunnel() {
  have cloudflared || die "cloudflared not found. Install it once: brew install cloudflared"
  TUNNEL_LOG="$(mktemp)"
  cloudflared tunnel --url "$BACKEND_URL" >"$TUNNEL_LOG" 2>&1 &
  TUNNEL_PID=$!

  local i url=""
  for i in $(seq 1 30); do
    url="$(grep -Eo 'https://[a-z0-9-]+\.trycloudflare\.com' "$TUNNEL_LOG" | head -1 || true)"
    [ -n "$url" ] && break
    if ! kill -0 "$TUNNEL_PID" 2>/dev/null; then
      warn "cloudflared exited early. Output:" >&2
      cat "$TUNNEL_LOG" >&2
      die "Could not open the tunnel."
    fi
    sleep 1
  done
  [ -n "$url" ] || die "Timed out waiting for the tunnel URL. See: $TUNNEL_LOG"
  printf '%s\n' "$url"
}

# ==============================================================================
# setup — first-time, run at home; survives tunnel restarts
# ==============================================================================
cmd_setup() {
  info "First-time setup for the talk."

  have node || die "Node.js not found. Install Node 18+."
  have docker || die "Docker not found. Install Docker Desktop."

  # 1. dependencies
  if [ ! -d node_modules ]; then
    info "Installing npm dependencies ..."
    npm install
  else
    ok "npm dependencies already installed."
  fi

  # 2. .env.local from the committed template
  if [ ! -f "$ENV_FILE" ]; then
    cp "$ENV_EXAMPLE" "$ENV_FILE"
    ok "Created talk/.env.local from .env.example"
  else
    ok "talk/.env.local already exists."
  fi

  # 3. poll backend
  info "Starting the poll backend (docker compose up -d) ..."
  dc up -d
  wait_for_backend

  # 4. account + poll — manual steps in the browser wizard
  echo
  info "Opening the admin UI: $ADMIN_URL"
  open_url "$ADMIN_URL"
  cat <<EOF

  In the browser:
    a) The wizard creates your presenter account (first run only).
    b) Create a poll — slug '$(get_env VITE_POLL_SLUG)' — with one single-choice
       question "Which one is correct?" and options: closed / union / how should I know?
    c) In the question editor click "Copy snippet" to get the pollId / questionId.

  Then paste the ids below (Enter to keep the current value in brackets).

EOF

  local slug pid qid cur
  cur="$(get_env VITE_POLL_SLUG)";         read -r -p "Poll slug        [${cur}]: " slug; slug="${slug:-$cur}"
  cur="$(get_env VITE_POLL_ID)";           read -r -p "Poll id          [${cur}]: " pid;  pid="${pid:-$cur}"
  cur="$(get_env VITE_POLL_QUESTION_ID)";  read -r -p "Question id      [${cur}]: " qid;  qid="${qid:-$cur}"

  set_env VITE_POLL_SLUG "$slug"
  set_env VITE_POLL_ID "$pid"
  set_env VITE_POLL_QUESTION_ID "$qid"
  ok "Wrote poll identity to talk/.env.local"

  echo
  ok "Setup complete. On talk day just run:  ./present.sh"
  have cloudflared || warn "Install the tunnel before the talk:  brew install cloudflared"
}

# ==============================================================================
# warm — pre-warm the dotnet live-demo builds so they don't stall on stage
# ==============================================================================
cmd_warm() {
  have dotnet || die "dotnet not found."
  local root="$TALK_DIR/.."
  info "Pre-warming demo build ..."
  dotnet build "$root/demo/Demo.slnx"
  info "Pre-warming example build ..."
  dotnet build "$root/../example/Workshop.sln"
  ok "Builds warmed. (Remember: run the demo once so restore is cached.)"
}

# ==============================================================================
# launch (default) — before the talk: backend + tunnel + dev deck
# ==============================================================================
cmd_launch() {
  # preflight: setup must have run
  if [ ! -d node_modules ] || [ ! -f "$ENV_FILE" ] || [ -z "$(get_env VITE_POLL_ID)" ]; then
    die "Not set up yet. Run:  ./present.sh setup"
  fi
  have docker || die "Docker not found."

  # 1. poll backend (idempotent — no-op if already running)
  info "Ensuring the poll backend is up ..."
  dc up -d
  wait_for_backend

  # 2. tunnel + wire the ephemeral URL into .env.local BEFORE Vite starts
  info "Opening a Cloudflare tunnel ..."
  local url; url="$(start_tunnel)"
  set_env VITE_POLL_SERVER "$url"
  ok "Tunnel live: $url"
  ok "Wrote VITE_POLL_SERVER to talk/.env.local"

  # 3. reminders that are still on you
  cat <<EOF

  $(printf '\033[1mSanity check\033[0m') from your phone on mobile data:
      ${url}/admin/     (this is also your private live tally)

  On stage:
    • Click the poll sign-in button in the Slidev toolbar (mints the deck token).
    • The question opens automatically when the QR slide shows; the admin UI is
      your manual Active/Closed fallback.

  Starting the deck now — Ctrl-C here stops the deck and closes the tunnel.

EOF

  # 4. deck (foreground). Vite reads .env.local at start, so the URL above is live.
  npm run dev
}

# --- dispatch ------------------------------------------------------------------
case "${1:-launch}" in
  setup) cmd_setup ;;
  warm)  cmd_warm ;;
  launch|"") cmd_launch ;;
  -h|--help|help)
    cat <<'EOF'
present.sh — one command to launch the talk (deck + live poll).

  ./present.sh          Launch: poll backend + Cloudflare tunnel + dev deck
  ./present.sh setup    First-time: install, create poll, wire the ids
  ./present.sh warm     Pre-warm the dotnet live-demo builds (before stage)

Everything the script writes lands in talk/.env.local (git-ignored).
EOF
    ;;
  *) die "Unknown command '$1'. Use: setup | warm | (no arg to launch)" ;;
esac
