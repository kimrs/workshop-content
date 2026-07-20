<script setup lang="ts">
import { PollResults } from '@slidev-polls/component'

// The poll's identity (slug + the pollId/questionId from the admin "Copy
// snippet") is specific to the poll you created, so it lives in env instead of
// slides.md: real values in talk/.env.local (git-ignored), placeholders in
// talk/.env.example. See README → Poll runbook. `slug` must match the <PollQr>
// tag on the QR slide and the poll in the admin UI. Renders nothing until the
// ids are set (e.g. the GitHub Pages build has no .env.local), so the deck stays
// clean off-stage.
const slug = import.meta.env.VITE_POLL_SLUG || 'csharp15'
const pollId = import.meta.env.VITE_POLL_ID
const questionId = import.meta.env.VITE_POLL_QUESTION_ID
const configured = Boolean(pollId && questionId)
</script>

<template>
  <PollResults v-if="configured" :slug="slug" :poll-id="pollId" :question-id="questionId" />
</template>
