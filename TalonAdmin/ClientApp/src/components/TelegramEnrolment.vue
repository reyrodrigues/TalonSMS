<script setup lang="ts">
import { ref, watch } from 'vue'
import * as api from '@/api/talon'
import { notify, notifyError } from '@/composables/useToast'

/**
 * A Telegram bot cannot message anyone who has not written to it first, so every recipient has to
 * tap an enrolment link once. This is where staff get that link.
 *
 * It renders nothing unless the active country actually delivers over Telegram — the endpoint
 * answers 404 otherwise, which is what keeps the control out of the way everywhere else.
 */
const props = defineProps<{ mobileNumber: string | null | undefined }>()

const link = ref<string | null>(null)
const enrolled = ref(false)
const loading = ref(false)

watch(() => props.mobileNumber, load, { immediate: true })

async function load() {
  link.value = null

  if (!props.mobileNumber?.trim()) return

  loading.value = true
  try {
    const result = await api.telegramEnrolmentLink(props.mobileNumber)
    link.value = result?.Link ?? null
    enrolled.value = result?.Enrolled ?? false
  } catch {
    // A 404 means this country is not on Telegram, which is the common case and not an error.
    link.value = null
  } finally {
    loading.value = false
  }
}

async function copy() {
  if (!link.value) return

  try {
    await navigator.clipboard.writeText(link.value)
    notify('success', 'Enrolment link copied.')
  } catch (error) {
    notifyError(error, 'Could not copy the link.')
  }
}
</script>

<template>
  <div v-if="link" class="field" style="grid-column: 1 / -1">
    <label class="field__label">Telegram enrolment</label>

    <p v-if="enrolled" class="muted" style="margin: 0 0 6px; font-size: 13px">
      <span class="badge badge--success">Enrolled</span>
      This household already receives voucher codes on Telegram.
    </p>
    <p v-else class="muted" style="margin: 0 0 6px; font-size: 13px">
      Not enrolled yet — until they open this link and tap Start, the bot cannot message them.
    </p>

    <div class="field__prefix">
      <input class="field__control" :value="link" readonly @focus="($event.target as HTMLInputElement).select()" />
      <button class="btn" type="button" :disabled="loading" @click="copy">Copy</button>
    </div>
  </div>
</template>
