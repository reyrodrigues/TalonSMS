<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import TelegramEnrolment from '@/components/TelegramEnrolment.vue'
import { useRouter } from 'vue-router'
import * as api from '@/api/talon'
import type { Beneficiary, BeneficiaryGroup, Location } from '@/api/types'
import { useSessionStore } from '@/stores/session'
import { notifyError, notifySaved } from '@/composables/useToast'

const { t } = useI18n()

const props = defineProps<{ id?: string }>()

const session = useSessionStore()
const router = useRouter()

const model = ref<Partial<Beneficiary>>(blank())
const original = ref<Beneficiary | null>(null)
const groups = ref<BeneficiaryGroup[]>([])
const locations = ref<Location[]>([])
const loading = ref(true)
const saving = ref(false)
const generating = ref(false)
const submitted = ref(false)

const isNew = computed(() => !props.id)
const readOnly = computed(() =>
  isNew.value ? !session.can('Create Beneficiaries') : !session.can('Edit Beneficiaries')
)

const callingCode = computed(() =>
  session.country?.CountryCallingCode ? `+${session.country.CountryCallingCode}` : '+'
)

function blank(): Partial<Beneficiary> {
  return {
    FirstName: '',
    LastName: '',
    PersonalIdentifier: '',
    MobileNumber: '',
    BirthYear: '',
    Sex: 1,
    PIN: '',
    GroupId: null,
    LocationId: null,
    Disabled: false
  }
}

const required = (value: unknown) => submitted.value && !String(value ?? '').trim()

const invalid = computed(
  () => !String(model.value.FirstName ?? '').trim() || !String(model.value.LastName ?? '').trim()
)

onMounted(async () => {
  try {
    const [groupList, locationList] = await Promise.all([
      api.beneficiaryGroups(),
      api.locations()
    ])
    groups.value = groupList
    locations.value = locationList

    if (props.id) {
      const found = await api.beneficiary(Number(props.id))
      if (!found) {
        notifyError(null, t('beneficiary.gone'))
        await router.replace({ name: 'beneficiaries' })
        return
      }
      original.value = found
      model.value = { ...found }
    }
  } catch (error) {
    notifyError(error, t('beneficiary.loadFailed'))
  } finally {
    loading.value = false
  }
})

/**
 * Draws a fresh identifier. Deliberately does not overwrite one already on file without asking —
 * the household has been told the old one, and a silent change strands them at the vendor.
 */
async function generateIdentifier() {
  if (model.value.PersonalIdentifier?.trim()
    && !window.confirm(t('beneficiary.replaceIdentifier'))) {
    return
  }

  generating.value = true
  try {
    const result = await api.generatePersonalIdentifier()
    model.value.PersonalIdentifier = result.Identifier
  } catch (error) {
    notifyError(error, t('beneficiary.generateFailed'))
  } finally {
    generating.value = false
  }
}

async function save() {
  submitted.value = true
  if (invalid.value) return

  saving.value = true
  try {
    const payload: Partial<Beneficiary> = {
      ...model.value,
      CountryId: session.country?.Id ?? 0,
      OrganizationId: session.organizationId
    }

    await api.saveBeneficiary(payload, original.value ?? undefined)
    notifySaved(t('beneficiary.singular'))
    await router.push({ name: 'beneficiaries' })
  } catch (error) {
    notifyError(error, t('beneficiary.saveFailed'))
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <h1>{{ isNew ? t('beneficiary.new') : t('beneficiary.singular') }}</h1>

  <p v-if="loading" class="empty"><span class="spinner" :aria-label="t('common.loading')" /></p>

  <form v-else @submit.prevent="save">
    <div class="panel panel__head">
      <button v-if="!readOnly" class="btn btn--primary" type="submit" :disabled="saving">
        {{ saving ? t('action.saving') : t('action.save') }}
      </button>
      <button class="btn btn--ghost" type="button" @click="router.push({ name: 'beneficiaries' })">
        {{ t('action.back') }}
      </button>
    </div>

    <div class="panel">
      <div class="panel__body grid-2">
        <div class="field">
          <label class="field__label" for="firstName">{{ t('beneficiary.firstName') }} <span class="field__required">*</span></label>
          <input
            id="firstName"
            v-model="model.FirstName"
            class="field__control"
            :class="{ 'field__control--invalid': required(model.FirstName) }"
            :disabled="readOnly"
          />
          <p v-if="required(model.FirstName)" class="field__error">{{ t('beneficiary.firstNameRequired') }}</p>
        </div>

        <div class="field">
          <label class="field__label" for="lastName">{{ t('beneficiary.lastName') }} <span class="field__required">*</span></label>
          <input
            id="lastName"
            v-model="model.LastName"
            class="field__control"
            :class="{ 'field__control--invalid': required(model.LastName) }"
            :disabled="readOnly"
          />
          <p v-if="required(model.LastName)" class="field__error">{{ t('beneficiary.lastNameRequired') }}</p>
        </div>

        <div class="field">
          <label class="field__label" for="personalIdentifier">{{ t('beneficiary.personalIdentifier') }}</label>
          <div class="field__prefix">
            <input
              id="personalIdentifier"
              v-model="model.PersonalIdentifier"
              class="field__control"
              :disabled="readOnly"
            />
            <button
              v-if="!readOnly"
              class="btn"
              type="button"
              :disabled="generating"
              @click="generateIdentifier"
            >
              {{ generating ? t('action.generating') : t('action.generate') }}
            </button>
          </div>
          <p class="muted" style="font-size: 12px; margin: 4px 0 0">
{{ t('beneficiary.personalIdentifierHint') }}
          </p>
        </div>

        <div class="field">
          <label class="field__label" for="mobile">{{ t('beneficiary.mobileNumber') }}</label>
          <div class="field__prefix">
            <span class="field__prefix-label">{{ callingCode }}</span>
            <input id="mobile" v-model="model.MobileNumber" class="field__control" :disabled="readOnly" />
          </div>
        </div>

        <TelegramEnrolment :mobile-number="model.MobileNumber" />

        <div class="field">
          <label class="field__label" for="birthYear">{{ t('beneficiary.birthYear') }}</label>
          <input id="birthYear" v-model="model.BirthYear" class="field__control" :disabled="readOnly" />
        </div>

        <div class="field">
          <label class="field__label" for="sex">{{ t('beneficiary.sex') }}</label>
          <select id="sex" v-model.number="model.Sex" class="field__control" :disabled="readOnly">
            <option :value="1">{{ t('beneficiary.male') }}</option>
            <option :value="2">{{ t('beneficiary.female') }}</option>
          </select>
        </div>

        <div class="field">
          <label class="field__label" for="group">{{ t('beneficiary.cycle') }}</label>
          <select id="group" v-model.number="model.GroupId" class="field__control" :disabled="readOnly">
            <option :value="null">{{ t('common.none') }}</option>
            <option v-for="group in groups" :key="group.Id" :value="group.Id">{{ group.Name }}</option>
          </select>
        </div>

        <div class="field">
          <label class="field__label" for="location">{{ t('beneficiary.location') }}</label>
          <select id="location" v-model.number="model.LocationId" class="field__control" :disabled="readOnly">
            <option :value="null">{{ t('common.none') }}</option>
            <option v-for="location in locations" :key="location.Id" :value="location.Id">
              {{ location.Name }}
            </option>
          </select>
        </div>

        <div class="field">
          <label class="field__label" for="pin">{{ t('beneficiary.pin') }}</label>
          <input id="pin" v-model="model.PIN" class="field__control" :disabled="readOnly" />
        </div>

        <div class="field">
          <label class="field__label"><input v-model="model.Disabled" type="checkbox" :disabled="readOnly" /> {{ t('beneficiary.disabledBadge') }}</label>
        </div>
      </div>
    </div>
  </form>
</template>
