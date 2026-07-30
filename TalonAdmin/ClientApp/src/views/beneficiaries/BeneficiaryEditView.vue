<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import * as api from '@/api/talon'
import type { Beneficiary, BeneficiaryGroup, Location } from '@/api/types'
import { useSessionStore } from '@/stores/session'
import { notifyError, notifySaved } from '@/composables/useToast'

const props = defineProps<{ id?: string }>()

const session = useSessionStore()
const router = useRouter()

const model = ref<Partial<Beneficiary>>(blank())
const original = ref<Beneficiary | null>(null)
const groups = ref<BeneficiaryGroup[]>([])
const locations = ref<Location[]>([])
const loading = ref(true)
const saving = ref(false)
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
    NationalId: '',
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
        notifyError(null, 'That beneficiary no longer exists.')
        await router.replace({ name: 'beneficiaries' })
        return
      }
      original.value = found
      model.value = { ...found }
    }
  } catch (error) {
    notifyError(error, 'Could not load the beneficiary.')
  } finally {
    loading.value = false
  }
})

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
    notifySaved('Beneficiary')
    await router.push({ name: 'beneficiaries' })
  } catch (error) {
    notifyError(error, 'Could not save the beneficiary.')
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <h1>{{ isNew ? 'New Beneficiary' : 'Beneficiary' }}</h1>

  <p v-if="loading" class="empty"><span class="spinner" aria-label="Loading" /></p>

  <form v-else @submit.prevent="save">
    <div class="panel panel__head">
      <button v-if="!readOnly" class="btn btn--primary" type="submit" :disabled="saving">
        {{ saving ? 'Saving…' : 'Save' }}
      </button>
      <button class="btn btn--ghost" type="button" @click="router.push({ name: 'beneficiaries' })">
        Back
      </button>
    </div>

    <div class="panel">
      <div class="panel__body grid-2">
        <div class="field">
          <label class="field__label" for="firstName">First Name <span class="field__required">*</span></label>
          <input
            id="firstName"
            v-model="model.FirstName"
            class="field__control"
            :class="{ 'field__control--invalid': required(model.FirstName) }"
            :disabled="readOnly"
          />
          <p v-if="required(model.FirstName)" class="field__error">First name is required.</p>
        </div>

        <div class="field">
          <label class="field__label" for="lastName">Last Name <span class="field__required">*</span></label>
          <input
            id="lastName"
            v-model="model.LastName"
            class="field__control"
            :class="{ 'field__control--invalid': required(model.LastName) }"
            :disabled="readOnly"
          />
          <p v-if="required(model.LastName)" class="field__error">Last name is required.</p>
        </div>

        <div class="field">
          <label class="field__label" for="nationalId">National Id</label>
          <input id="nationalId" v-model="model.NationalId" class="field__control" :disabled="readOnly" />
        </div>

        <div class="field">
          <label class="field__label" for="mobile">Mobile Number</label>
          <div class="field__prefix">
            <span class="field__prefix-label">{{ callingCode }}</span>
            <input id="mobile" v-model="model.MobileNumber" class="field__control" :disabled="readOnly" />
          </div>
        </div>

        <div class="field">
          <label class="field__label" for="birthYear">Birth Year</label>
          <input id="birthYear" v-model="model.BirthYear" class="field__control" :disabled="readOnly" />
        </div>

        <div class="field">
          <label class="field__label" for="sex">Sex</label>
          <select id="sex" v-model.number="model.Sex" class="field__control" :disabled="readOnly">
            <option :value="1">Male</option>
            <option :value="2">Female</option>
          </select>
        </div>

        <div class="field">
          <label class="field__label" for="group">Cycle</label>
          <select id="group" v-model.number="model.GroupId" class="field__control" :disabled="readOnly">
            <option :value="null">—</option>
            <option v-for="group in groups" :key="group.Id" :value="group.Id">{{ group.Name }}</option>
          </select>
        </div>

        <div class="field">
          <label class="field__label" for="location">Location</label>
          <select id="location" v-model.number="model.LocationId" class="field__control" :disabled="readOnly">
            <option :value="null">—</option>
            <option v-for="location in locations" :key="location.Id" :value="location.Id">
              {{ location.Name }}
            </option>
          </select>
        </div>

        <div class="field">
          <label class="field__label" for="pin">PIN</label>
          <input id="pin" v-model="model.PIN" class="field__control" :disabled="readOnly" />
        </div>

        <div class="field">
          <label class="field__label"><input v-model="model.Disabled" type="checkbox" :disabled="readOnly" /> Disabled</label>
        </div>
      </div>
    </div>
  </form>
</template>
