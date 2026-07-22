<script setup lang="ts">
import { nextTick, onMounted, ref, watch } from "vue";
import QRCodeStyling, { type Options } from "qr-code-styling";

// A styled QR for an arbitrary URL (e.g. a Google Form). Unlike the poll addon's
// <PollQr> — which is hard-wired to the poll voter URL — this takes the target as
// a prop. The look is a local copy of @slidev-polls' buildQrOptions so this
// feedback QR reads the same as the live-poll QR (rounded dots, extra-rounded
// finder squares, white background), without importing the addon's internals.
const props = defineProps<{
  url: string;
  caption?: string;
}>();

function buildQrOptions(url: string): Options {
  return {
    width: 512,
    height: 512,
    type: "svg" as const,
    data: url,
    margin: 8,
    qrOptions: { errorCorrectionLevel: "M" },
    dotsOptions: { type: "rounded" as const, color: "#111111" },
    cornersSquareOptions: { type: "extra-rounded" as const, color: "#111111" },
    cornersDotOptions: { type: "dot" as const, color: "#111111" },
    backgroundOptions: { color: "#ffffff" }
  };
}

const qrHost = ref<HTMLDivElement | null>(null);
let qr: QRCodeStyling | null = null;

onMounted(async () => {
  await nextTick();
  if (!qrHost.value) return;
  qrHost.value.replaceChildren();
  qr = new QRCodeStyling(buildQrOptions(props.url));
  qr.append(qrHost.value);
});

// Re-render when the URL prop changes.
watch(() => props.url, (v) => {
  if (qr) qr.update(buildQrOptions(v));
});
</script>

<template>
  <div class="feedback-qr" data-testid="feedback-qr">
    <div ref="qrHost" class="feedback-qr__svg" />
    <p v-if="caption" class="feedback-qr__caption">{{ caption }}</p>
  </div>
</template>

<style scoped>
/* Compact white card — smaller than <PollQr> because it shares the center-layout
   Thank-you slide with the title, repo link, and bullets. */
.feedback-qr {
  width: 100%;
  max-width: 200px;
  box-sizing: border-box;
  background: #fff;
  padding: 16px 16px 12px;
  border-radius: 12px;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 10px;
}
.feedback-qr__svg {
  width: 100%;
  aspect-ratio: 1 / 1;
}
.feedback-qr__svg :deep(svg) {
  width: 100%;
  height: 100%;
  display: block;
}
.feedback-qr__caption {
  margin: 0;
  font-size: 14px;
  color: #111;
  text-align: center;
}
</style>
