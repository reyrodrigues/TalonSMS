<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import * as api from '@/api/talon'
import type { Location, Vendor, VendorType } from '@/api/types'
import { useSessionStore } from '@/stores/session'
import { notifyError, notifySaved } from '@/composables/useToast'

const props = defineProps<{ id?: string }>()

const session = useSessionStore()
const router = useRouter()

const model = ref<Partial<Vendor>>({
  Name: '',
  FirstName: '',
  LastName: '',
  MobileNumber: '',
  PersonalIdentifier: '',
  TypeId: null,
  LocationId: null
})
const original = ref<Vendor | null>(null)
const types = ref<VendorType[]>([])
const locations = ref<Location[]>([])
const loading = ref(true)
const saving = ref(false)
const submitted = ref(false)

const isNew = computed(() => !props.id)
const readOnly = computed(() =>
  isNew.value ? !session.can('Create Vendors') : !session.can('Edit Vendors')
)

const callingCode = computed(() =>
  session.country?.CountryCallingCode ? `+${session.country.CountryCallingCode}` : '+'
)

const required = (value: unknown) => submitted.value && !String(value ?? '').trim()

const invalid = computed(() =>
  ['Name', 'FirstName', 'LastName', 'MobileNumber', 'PersonalIdentifier'].some(
    field => !String(model.value[field as keyof Vendor] ?? '').trim()
  )
)

onMounted(async () => {
  try {
    const [typeList, locationList] = await Promise.all([api.vendorTypes(), api.locations()])
    types.value = typeList
    locations.value = locationList

    if (props.id) {
      const found = await api.vendor(Number(props.id))
      if (!found) {
        notifyError(null, 'That vendor no longer exists.')
        await router.replace({ name: 'vendors' })
        return
      }
      original.value = found
      model.value = { ...found }
    }
  } catch (error) {
    notifyError(error, 'Could not load the vendor.')
  } finally {
    loading.value = false
  }
})

async function save() {
  submitted.value = true
  if (invalid.value) return

  saving.value = true
  try {
    await api.saveVendor(
      { ...model.value, CountryId: session.country?.Id ?? 0 },
      original.value ?? undefined
    )
    notifySaved('Vendor')
    await router.push({ name: 'vendors' })
  } catch (error) {
    notifyError(error, 'Could not save the vendor.')
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <h1>{{ isNew ? 'New Vendor' : 'Vendor' }}</h1>

  <p v-if="loading" class="empty"><span class="spinner" aria-label="Loading" /></p>

  <form v-else @submit.prevent="save">
    <div class="panel panel__head">
      <button v-if="!readOnly" class="btn btn--primary" type="submit" :disabled="saving">
        {{ saving ? 'Saving…' : 'Save' }}
      </button>
      <button class="btn btn--ghost" type="button" @click="router.push({ name: 'vendors' })">
        Back
      </button>
    </div>

    <div class="panel">
      <div class="panel__body">
        <div class="field">
          <label class="field__label" for="name">Entity Name <span class="field__required">*</span></label>
          <input
            id="name"
            v-model="model.Name"
            class="field__control"
            :class="{ 'field__control--invalid': required(model.Name) }"
            :disabled="readOnly"
          />
          <p v-if="required(model.Name)" class="field__error">Entity name is required.</p>
        </div>

        <div class="grid-2">
          <div class="field">
            <label class="field__label" for="type">Vendor Type</label>
            <select id="type" v-model.number="model.TypeId" class="field__control" :disabled="readOnly">
              <option :value="null">—</option>
              <option v-for="type in types" :key="type.Id" :value="type.Id">{{ type.Name }}</option>
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
        </div>
      </div>
    </div>

    <div class="panel">
      <div class="panel__head"><h2 style="margin: 0">Owner Information</h2></div>
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
        </div>

        <div class="field">
          <label class="field__label" for="mobile">Mobile Number <span class="field__required">*</span></label>
          <div class="field__prefix">
            <span class="field__prefix-label">{{ callingCode }}</span>
            <input
              id="mobile"
              v-model="model.MobileNumber"
              class="field__control"
              :class="{ 'field__control--invalid': required(model.MobileNumber) }"
              :disabled="readOnly"
            />
          </div>
        </div>

        <div class="field">
          <label class="field__label" for="personalIdentifier">Personal Identifier <span class="field__required">*</span></label>
          <input
            id="personalIdentifier"
            v-model="model.PersonalIdentifier"
            class="field__control"
            :class="{ 'field__control--invalid': required(model.PersonalIdentifier) }"
            :disabled="readOnly"
          />
        </div>
      </div>
    </div>
  </form>
</template>
