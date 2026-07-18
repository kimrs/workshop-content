import { defineConfig } from 'unocss'

export default defineConfig({
  // Speaker-note timestamps like [0:31] would otherwise be extracted as
  // arbitrary-property utilities and emit invalid CSS that breaks the build.
  blocklist: [/^\[\d+:\d+\]$/],
})
