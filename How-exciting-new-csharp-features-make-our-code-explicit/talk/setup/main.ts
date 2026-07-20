import { configs } from '@slidev/client'
import { defineAppSetup } from '@slidev/types'

// The slidev-polls addon reads the backend host from `pollServer` in the deck
// config (`$slidev.configs`). That host is an ephemeral Cloudflare tunnel URL
// that changes every talk, so we deliberately keep it OUT of the committed
// slides.md: set VITE_POLL_SERVER in talk/.env.local (git-ignored) instead, and
// inject it into the deck config here — this setup runs once at app boot, before
// any slide (and therefore any PollQr/PollResults) renders. See README → Poll
// runbook. When the var is unset (e.g. the GitHub Pages build) the addon just
// falls back to the browser origin, exactly as before.
export default defineAppSetup(() => {
  const pollServer = import.meta.env.VITE_POLL_SERVER
  if (typeof pollServer === 'string' && pollServer.length > 0)
    (configs as Record<string, unknown>).pollServer = pollServer
})
