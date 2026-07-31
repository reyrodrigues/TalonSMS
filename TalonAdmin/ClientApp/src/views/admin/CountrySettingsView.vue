<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import * as api from '@/api/talon'
import type { OrganizationCountry } from '@/api/types'
import { notifyError, notifySaved } from '@/composables/useToast'

/**
 * Per-country SMS configuration: which gateway to use, its credentials, and the message templates
 * the voucher workflow sends. Every country the organization operates in is edited from here.
 *
 * `SMS_BACKENDS` is the contract with `SmsBackendType` on the server. The list used to be wrong in
 * both directions: it offered gateways that were never implemented, and its numbering disagreed
 * with the dispatch, so picking "RescueSMS" actually configured TurboSMS. RescueSMS and TurboSMS
 * have since been removed; Clickatell keeps the value it already had.
 */
const SMS_BACKENDS = [{ value: 2, label: 'Clickatell' }]
const DEFAULT_BACKEND = SMS_BACKENDS[0].value

/** The template names the server looks up when it builds an outbound message. */
const TEMPLATES = [
  'Beneficiary Voucher Message',
  'Beneficiary Confirmed Message',
  'Vendor Confirmed Message',
  'Beneficiary Cancelled Voucher Message',
  'Invalid Voucher Message',
  'Wrong National Id Message',
  'Vendor Cannot Accept Message',
  'Beneficiary Cannot Accept Message',
  'Vendor Already Used Message',
  'Beneficiary Already Used Message',
  'Vendor Cancelled Message'
]

const rows = ref<OrganizationCountry[]>([])
const selectedId = ref<number | null>(null)
const gateway = ref({ SmsBackendType: DEFAULT_BACKEND, ServiceUrl: '', ServiceUser: '', ServicePassword: '' })
const templates = ref<Record<string, string>>({})
const present = ref(new Set<string>())

const loading = ref(true)
const saving = ref(false)

const selected = computed(() => rows.value.find(r => r.Id === selectedId.value) ?? null)

onMounted(async () => {
  try {
    rows.value = await api.organizationCountries()
    select(rows.value[0]?.Id ?? null)
  } catch (error) {
    notifyError(error, 'Could not load the country settings.')
  } finally {
    loading.value = false
  }
})

function select(id: number | null) {
  selectedId.value = id
  const settings = rows.value.find(r => r.Id === id)?.Settings

  gateway.value = {
    SmsBackendType: settings?.SmsBackendType ?? DEFAULT_BACKEND,
    ServiceUrl: settings?.ServiceUrl ?? '',
    ServiceUser: settings?.ServiceUser ?? '',
    ServicePassword: settings?.ServicePassword ?? ''
  }

  const stored = settings?.PropertyCollection ?? []
  present.value = new Set(stored.map(p => p.Name))
  templates.value = Object.fromEntries(
    TEMPLATES.map(name => [name, stored.find(p => p.Name === name)?.Value ?? ''])
  )
}

async function save() {
  const settings = selected.value?.Settings
  if (!settings) {
    notifyError(null, 'This country has no settings record to update.')
    return
  }

  saving.value = true
  try {
    // Only templates that were given a value are written; blank ones are left as they were.
    const changed = TEMPLATES.filter(name => templates.value[name].trim()).map(name => ({
      Name: name,
      Value: templates.value[name],
      existed: present.value.has(name)
    }))

    await api.saveCountrySettings(settings, { ...gateway.value }, changed)

    notifySaved('Country settings')
    rows.value = await api.organizationCountries()
    select(selectedId.value)
  } catch (error) {
    notifyError(error, 'Could not save the country settings.')
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <h1>Country Settings</h1>

  <p v-if="loading" class="empty"><span class="spinner" aria-label="Loading" /></p>

  <p v-else-if="rows.length === 0" class="empty">
    This organization is not yet operating in any country.
  </p>

  <form v-else @submit.prevent="save">
    <div class="panel panel__head">
      <label class="row-gap">
        Country
        <select
          class="input"
          style="width: auto"
          :value="selectedId ?? ''"
          @change="select(Number(($event.target as HTMLSelectElement).value))"
        >
          <option v-for="row in rows" :key="row.Id" :value="row.Id">{{ row.Country?.Name }}</option>
        </select>
      </label>
      <div class="panel__spacer" />
      <button class="btn btn--primary" type="submit" :disabled="saving">
        {{ saving ? 'Saving…' : 'Save' }}
      </button>
    </div>

    <div class="panel">
      <div class="panel__head"><h2 style="margin: 0">SMS gateway</h2></div>
      <div class="panel__body grid-2">
        <div class="field">
          <label class="field__label" for="backend">SMS Service Type</label>
          <select id="backend" v-model.number="gateway.SmsBackendType" class="field__control">
            <option v-for="option in SMS_BACKENDS" :key="option.value" :value="option.value">
              {{ option.label }}
            </option>
          </select>
        </div>

        <div class="field">
          <label class="field__label" for="url">SMS Service URL</label>
          <input id="url" v-model="gateway.ServiceUrl" class="field__control" />
          <p class="muted" style="font-size: 12px; margin: 4px 0 0">
            A URL template: <code>{0}</code> is the recipient, <code>{1}</code> the message.
            Nothing is sent until this is set.
          </p>
        </div>

        <div class="field">
          <label class="field__label" for="user">SMS Service User Name</label>
          <input id="user" v-model="gateway.ServiceUser" class="field__control" autocomplete="off" />
        </div>

        <div class="field">
          <label class="field__label" for="password">SMS Service Password</label>
          <input
            id="password"
            v-model="gateway.ServicePassword"
            type="password"
            class="field__control"
            autocomplete="new-password"
          />
        </div>
      </div>
    </div>

    <div class="panel">
      <div class="panel__head"><h2 style="margin: 0">Message templates</h2></div>
      <div class="panel__body">
        <div v-for="name in TEMPLATES" :key="name" class="field">
          <label class="field__label" :for="`tpl-${name}`">{{ name }}</label>
          <input :id="`tpl-${name}`" v-model="templates[name]" class="field__control" />
        </div>
      </div>
    </div>
  </form>
</template>
