<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import * as api from '@/api/talon'
import type { OrganizationCountry } from '@/api/types'
import { notifyError, notifySaved } from '@/composables/useToast'

const { t } = useI18n()

/**
 * Per-country messaging configuration: which channels to send through and in what order, each
 * channel's credentials, and the message templates the voucher workflow uses.
 *
 * A country used to pick exactly one gateway. That is fine for SMS, where everyone is reachable,
 * but not for Telegram: a bot can only message people who have enrolled. So this edits an ordered
 * chain — Telegram first, say, with SMS behind it for everyone else — and each channel carries its
 * own credentials, because a bot token and a gateway URL cannot share one field.
 */
interface ChannelDefinition {
  key: string
  label: string
  /** The SmsBackendType value, kept in step so anything reading it still resolves. */
  backend: number
  urlLabel: string
  urlHint: string
  needsUser: boolean
  passwordLabel: string
  /** Extra country properties this channel needs beyond the credential triple. */
  properties?: Array<{ name: string; label: string; hint: string; placeholder?: string }>
}

const CHANNELS: ChannelDefinition[] = [
  {
    key: 'sms.clickatell',
    label: 'Clickatell',
    backend: 2,
    urlLabel: 'Service URL',
    urlHint: 'A URL template: {0} is the recipient, {1} the message.',
    needsUser: true,
    passwordLabel: 'Password'
  },
  {
    key: 'sms.android',
    label: 'Android SMS Gateway',
    backend: 4,
    urlLabel: 'Gateway API root',
    urlHint: 'The phone on the office LAN, or the relay it connects out to.',
    needsUser: true,
    passwordLabel: 'Gateway password'
  },
  {
    key: 'telegram',
    label: 'Telegram bot',
    backend: 5,
    urlLabel: 'API base',
    urlHint: 'Leave blank for api.telegram.org.',
    needsUser: false,
    passwordLabel: 'Bot token',
    properties: [
      {
        name: 'Telegram Bot Username',
        label: 'Bot username',
        hint: 'Used to build the enrolment links staff send to beneficiaries.',
        placeholder: 'TalonJordanBot'
      }
    ]
  }
]

/** Matches MessageChannelRegistry.ChainPropertyName on the server. */
const CHAIN_PROPERTY = 'Message Channels'

/**
 * Stored template names, which are data: renaming one would orphan every country that has already
 * filled it in. `TEMPLATE_LABELS` relabels the ones whose stored name has aged badly.
 */
const TEMPLATE_LABELS: Record<string, string> = {
  'Wrong National Id Message': 'Wrong Personal Identifier Message'
}

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

/** Ordered channel keys the country will try. */
const chain = ref<string[]>([])
const credentials = ref<Record<string, { url: string; user: string; password: string }>>({})
const properties = ref<Record<string, string>>({})
const templates = ref<Record<string, string>>({})
const present = ref(new Set<string>())

const loading = ref(true)
const saving = ref(false)

const selected = computed(() => rows.value.find(r => r.Id === selectedId.value) ?? null)

const definition = (key: string) => CHANNELS.find(c => c.key === key)!

/** Channels not yet in the chain, offered for adding. */
const available = computed(() => CHANNELS.filter(c => !chain.value.includes(c.key)))

const propertyNames = computed(() => CHANNELS.flatMap(c => c.properties?.map(p => p.name) ?? []))

onMounted(async () => {
  try {
    rows.value = await api.organizationCountries()
    select(rows.value[0]?.Id ?? null)
  } catch (error) {
    notifyError(error, t('settings.loadFailed'))
  } finally {
    loading.value = false
  }
})

function select(id: number | null) {
  selectedId.value = id

  const settings = rows.value.find(r => r.Id === id)?.Settings
  const stored = settings?.PropertyCollection ?? []
  const value = (name: string) => stored.find(p => p.Name === name)?.Value ?? ''

  present.value = new Set(stored.map(p => p.Name))

  // The chain property wins; without it the country is on the single backend it always was.
  const configured = value(CHAIN_PROPERTY)
    .split(',')
    .map(k => k.trim())
    .filter(k => CHANNELS.some(c => c.key === k))

  const fromBackend = CHANNELS.find(c => c.backend === settings?.SmsBackendType)
  chain.value = configured.length > 0 ? configured : fromBackend ? [fromBackend.key] : []

  credentials.value = Object.fromEntries(
    CHANNELS.map(c => {
      const scoped = {
        url: value(`Channel ${c.key} Url`),
        user: value(`Channel ${c.key} User`),
        password: value(`Channel ${c.key} Password`)
      }

      // A channel with no overrides of its own uses the country's original three fields, which is
      // how every single-channel country was configured before chains existed.
      const hasOwn = scoped.url || scoped.user || scoped.password
      return [
        c.key,
        hasOwn
          ? scoped
          : {
              url: settings?.ServiceUrl ?? '',
              user: settings?.ServiceUser ?? '',
              password: settings?.ServicePassword ?? ''
            }
      ]
    })
  )

  properties.value = Object.fromEntries(propertyNames.value.map(name => [name, value(name)]))
  templates.value = Object.fromEntries(TEMPLATES.map(name => [name, value(name)]))
}

function add(key: string) {
  if (key && !chain.value.includes(key)) chain.value.push(key)
}

function remove(index: number) {
  chain.value.splice(index, 1)
}

function move(index: number, by: number) {
  const target = index + by
  if (target < 0 || target >= chain.value.length) return

  const [moved] = chain.value.splice(index, 1)
  chain.value.splice(target, 0, moved)
}

async function save() {
  const settings = selected.value?.Settings
  if (!settings) {
    notifyError(null, t('settings.noSettings'))
    return
  }

  saving.value = true
  try {
    const primary = chain.value[0]

    // The first channel's credentials also go in the country's own fields, so anything still
    // reading those — and the server's fallback when the chain property is absent — stays coherent.
    const head = primary ? credentials.value[primary] : { url: '', user: '', password: '' }

    const gateway = {
      SmsBackendType: primary ? definition(primary).backend : settings.SmsBackendType,
      ServiceUrl: head.url,
      ServiceUser: head.user,
      ServicePassword: head.password
    }

    const changed: Array<{ Name: string; Value: string; existed: boolean }> = []
    const write = (name: string, value: string) => {
      if (value?.trim()) changed.push({ Name: name, Value: value, existed: present.value.has(name) })
    }

    write(CHAIN_PROPERTY, chain.value.join(','))

    // Every channel after the first needs its own credentials; the first reads the country fields.
    for (const key of chain.value.slice(1)) {
      const c = credentials.value[key]
      write(`Channel ${key} Url`, c.url)
      write(`Channel ${key} User`, c.user)
      write(`Channel ${key} Password`, c.password)
    }

    for (const name of propertyNames.value) write(name, properties.value[name])
    for (const name of TEMPLATES) write(name, templates.value[name])

    await api.saveCountrySettings(settings, gateway, changed)

    notifySaved(t('settings.saved'))
    rows.value = await api.organizationCountries()
    select(selectedId.value)
  } catch (error) {
    notifyError(error, t('settings.saveFailed'))
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <h1>{{ t('settings.title') }}</h1>

  <p v-if="loading" class="empty"><span class="spinner" :aria-label="t('common.loading')" /></p>

  <p v-else-if="rows.length === 0" class="empty">
    {{ t('settings.noCountries') }}
  </p>

  <form v-else @submit.prevent="save">
    <div class="panel panel__head">
      <label class="row-gap">
        {{ t('country.singular') }}
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
        {{ saving ? t('action.saving') : t('action.save') }}
      </button>
    </div>

    <div class="panel">
      <div class="panel__head">
        <h2 style="margin: 0">{{ t('settings.channels') }}</h2>
        <div class="panel__spacer" />
        <select
          v-if="available.length > 0"
          class="input"
          style="width: auto"
          value=""
          @change="add(($event.target as HTMLSelectElement).value); ($event.target as HTMLSelectElement).value = ''"
        >
          <option value="">{{ t('settings.addChannel') }}</option>
          <option v-for="option in available" :key="option.key" :value="option.key">
            {{ option.label }}
          </option>
        </select>
      </div>

      <div class="panel__body">
        <p v-if="chain.length === 0" class="muted" style="margin: 0">
          {{ t('settings.noChannels') }}
        </p>

        <p v-else class="muted" style="margin: 0 0 12px; font-size: 13px">
          {{ t('settings.chainHint') }}
        </p>

        <div v-for="(key, index) in chain" :key="key" class="channel">
          <div class="channel__head">
            <span class="badge">{{ index + 1 }}</span>
            <strong>{{ definition(key).label }}</strong>
            <span v-if="index === 0" class="muted" style="font-size: 12px">{{ t('settings.primary') }}</span>
            <div class="panel__spacer" />
            <button class="btn btn--sm" type="button" :disabled="index === 0" @click="move(index, -1)">
              ↑
            </button>
            <button
              class="btn btn--sm"
              type="button"
              :disabled="index === chain.length - 1"
              @click="move(index, 1)"
            >
              ↓
            </button>
            <button class="btn btn--sm btn--danger" type="button" @click="remove(index)">
              {{ t('action.remove') }}
            </button>
          </div>

          <div class="grid-2">
            <div class="field">
              <label class="field__label" :for="`url-${key}`">{{ definition(key).urlLabel }}</label>
              <input :id="`url-${key}`" v-model="credentials[key].url" class="field__control" />
              <p class="muted" style="font-size: 12px; margin: 4px 0 0">{{ definition(key).urlHint }}</p>
            </div>

            <div v-if="definition(key).needsUser" class="field">
              <label class="field__label" :for="`user-${key}`">{{ t('settings.userName') }}</label>
              <input
                :id="`user-${key}`"
                v-model="credentials[key].user"
                class="field__control"
                autocomplete="off"
              />
            </div>

            <div class="field">
              <label class="field__label" :for="`password-${key}`">
                {{ definition(key).passwordLabel }}
              </label>
              <input
                :id="`password-${key}`"
                v-model="credentials[key].password"
                type="password"
                class="field__control"
                autocomplete="new-password"
              />
            </div>

            <div v-for="property in definition(key).properties ?? []" :key="property.name" class="field">
              <label class="field__label" :for="`prop-${property.name}`">{{ property.label }}</label>
              <input
                :id="`prop-${property.name}`"
                v-model="properties[property.name]"
                class="field__control"
                :placeholder="property.placeholder"
              />
              <p class="muted" style="font-size: 12px; margin: 4px 0 0">{{ property.hint }}</p>
            </div>
          </div>
        </div>
      </div>
    </div>

    <div class="panel">
      <div class="panel__head"><h2 style="margin: 0">{{ t('settings.templates') }}</h2></div>
      <div class="panel__body">
        <div v-for="name in TEMPLATES" :key="name" class="field">
          <label class="field__label" :for="`tpl-${name}`">{{ TEMPLATE_LABELS[name] ?? name }}</label>
          <input :id="`tpl-${name}`" v-model="templates[name]" class="field__control" />
        </div>
      </div>
    </div>
  </form>
</template>

<style scoped>
.channel {
  border: 1px solid var(--line);
  border-radius: var(--radius);
  padding: 12px 14px 0;
  margin-bottom: 14px;
}

.channel__head {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 12px;
}
</style>
