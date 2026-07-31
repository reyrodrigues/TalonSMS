<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { RouterLink, useRouter } from 'vue-router'
import * as api from '@/api/talon'
import type {
  BeneficiaryGroup,
  Distribution,
  Location,
  Program,
  ProgramVoucherCategory,
  VendorType,
  VoucherType
} from '@/api/types'
import { useSessionStore } from '@/stores/session'
import { useFormat } from '@/composables/useFormat'
import { notify, notifyError, notifySaved } from '@/composables/useToast'

const { t } = useI18n()

/**
 * A programme carries its voucher categories, and it is where a distribution is started from.
 * The AngularJS version edited the categories inline in the same form; that is kept.
 */
const props = defineProps<{ id?: string }>()

const session = useSessionStore()
const router = useRouter()
const { date } = useFormat()

/** The paper QR-code mechanism was removed; it was never used in the field. */
const MECHANISMS = [
  { value: 1, label: 'SMS' },
  { value: 2, label: 'NFC' }
]

const model = ref<Partial<Program>>({
  Name: '',
  FundCodes: '',
  DistributionMechanism: 1,
  VoucherCodeLength: 6,
  ShowInDashboard: true
})
const original = ref<Program | null>(null)

const categories = ref<Array<Partial<ProgramVoucherCategory>>>([])
const removedCategories = ref<ProgramVoucherCategory[]>([])
const distributions = ref<Distribution[]>([])

const voucherTypes = ref<VoucherType[]>([])
const vendorTypes = ref<VendorType[]>([])
const cycles = ref<BeneficiaryGroup[]>([])
const locations = ref<Location[]>([])

const loading = ref(true)
const saving = ref(false)
const submitted = ref(false)
const distributing = ref(false)
const showDistribute = ref(false)
const distributeForm = ref<{ GroupId: number | null; LocationId: number | null }>({
  GroupId: null,
  LocationId: null
})

const isNew = computed(() => !props.id)
const readOnly = computed(() =>
  isNew.value ? !session.can('Create Programs') : !session.can('Edit Programs')
)
const invalid = computed(
  () => !String(model.value.Name ?? '').trim() || !String(model.value.FundCodes ?? '').trim()
)

onMounted(async () => {
  try {
    const [voucher, vendor, cycleList, locationList] = await Promise.all([
      api.voucherTypes(),
      api.vendorTypes(),
      api.beneficiaryGroups(),
      api.locations()
    ])
    voucherTypes.value = voucher
    vendorTypes.value = vendor
    cycles.value = cycleList
    locations.value = locationList

    if (props.id) {
      const id = Number(props.id)
      const [found, distributionList] = await Promise.all([
        api.program(id),
        api.distributionsForProgram(id)
      ])

      if (!found) {
        notifyError(null, t('program.gone'))
        await router.replace({ name: 'programs' })
        return
      }

      original.value = found
      model.value = { ...found }
      categories.value = (found.Categories ?? []).map(c => ({ ...c }))
      distributions.value = distributionList
    }
  } catch (error) {
    notifyError(error, t('program.loadFailed'))
  } finally {
    loading.value = false
  }
})

function addCategory() {
  categories.value.push({
    TypeId: voucherTypes.value[0]?.Id,
    VendorTypeId: vendorTypes.value[0]?.Id ?? null,
    Value: null,
    NumberOfVouchers: 0,
    IssuedVouchers: 0
  })
}

function removeCategory(index: number) {
  const [removed] = categories.value.splice(index, 1)
  if (removed?.Id) removedCategories.value.push(removed as ProgramVoucherCategory)
}

async function save() {
  submitted.value = true
  if (invalid.value) return

  saving.value = true
  try {
    const tenant = { CountryId: session.country?.Id ?? 0, OrganizationId: session.organizationId }

    const saved = await api.saveProgram({ ...model.value, ...tenant }, original.value ?? undefined)

    // A new programme only gets its key back from the save, and the categories need it.
    const programId =
      model.value.Id ?? saved.KeyMappings[0]?.RealValue ?? saved.Entities[0]?.Id

    if (!programId) throw new Error(t('program.noKey'))

    const withProgram = categories.value.map(c => ({ ...c, ProgramId: programId, ...tenant }))

    if (withProgram.length > 0 || removedCategories.value.length > 0) {
      await api.saveProgramCategories(
        withProgram.filter(c => !c.Id),
        withProgram.filter(c => c.Id),
        removedCategories.value
      )
    }

    notifySaved(t('program.singular'))
    await router.push({ name: 'programs' })
  } catch (error) {
    notifyError(error, t('program.saveFailed'))
  } finally {
    saving.value = false
  }
}

async function distribute() {
  if (!distributeForm.value.GroupId || !distributeForm.value.LocationId || !props.id) return

  distributing.value = true
  try {
    await api.distributeVouchers({
      ProgramId: Number(props.id),
      GroupId: distributeForm.value.GroupId,
      LocationId: distributeForm.value.LocationId
    })

    showDistribute.value = false
    notify('success', t('distribute.done'))
    distributions.value = await api.distributionsForProgram(Number(props.id))
  } catch (error) {
    notifyError(error, t('distribute.failed'))
  } finally {
    distributing.value = false
  }
}
</script>

<template>
  <h1>{{ isNew ? t('program.new') : t('program.singular') }}</h1>

  <p v-if="loading" class="empty"><span class="spinner" :aria-label="t('common.loading')" /></p>

  <form v-else @submit.prevent="save">
    <div class="panel panel__head">
      <button
        v-if="!isNew && session.can('Distribute Vouchers')"
        class="btn btn--info"
        type="button"
        @click="showDistribute = true"
      >
        {{ t('distribute.action') }}
      </button>
      <div class="panel__spacer" />
      <button v-if="!readOnly" class="btn btn--primary" type="submit" :disabled="saving">
        {{ saving ? t('action.saving') : t('action.save') }}
      </button>
      <button class="btn btn--ghost" type="button" @click="router.push({ name: 'programs' })">
        {{ t('action.back') }}
      </button>
    </div>

    <div class="panel">
      <div class="panel__body grid-2">
        <div class="field">
          <label class="field__label" for="name">{{ t('common.name') }} <span class="field__required">*</span></label>
          <input
            id="name"
            v-model="model.Name"
            class="field__control"
            :class="{ 'field__control--invalid': submitted && !model.Name }"
            :disabled="readOnly"
          />
        </div>

        <div class="field">
          <label class="field__label" for="mechanism">{{ t('program.mechanism') }}</label>
          <select
            id="mechanism"
            v-model.number="model.DistributionMechanism"
            class="field__control"
            :disabled="readOnly || !isNew"
          >
            <option v-for="m in MECHANISMS" :key="m.value" :value="m.value">{{ m.label }}</option>
          </select>
        </div>

        <div class="field">
          <label class="field__label" for="fundCodes">{{ t('program.fundCodes') }} <span class="field__required">*</span></label>
          <input
            id="fundCodes"
            v-model="model.FundCodes"
            class="field__control"
            :class="{ 'field__control--invalid': submitted && !model.FundCodes }"
            :disabled="readOnly"
          />
        </div>

        <div class="field">
          <label class="field__label" for="codeLength">{{ t('program.codeLength') }}</label>
          <input
            id="codeLength"
            v-model.number="model.VoucherCodeLength"
            type="number"
            class="field__control"
            :disabled="readOnly"
          />
        </div>

        <div class="field">
          <label class="field__label">
            <input v-model="model.ShowInDashboard" type="checkbox" :disabled="readOnly" />
            {{ t('program.showInDashboard') }}
          </label>
        </div>
      </div>
    </div>

    <div class="panel">
      <div class="panel__head">
        <h2 style="margin: 0">{{ t('program.categories') }}</h2>
        <div class="panel__spacer" />
        <button v-if="!readOnly" class="btn btn--sm" type="button" @click="addCategory">{{ t('action.add') }}</button>
      </div>

      <div class="table-wrap">
        <table class="table">
          <thead>
            <tr>
              <th style="width: 60px">{{ t('common.id') }}</th>
              <th>{{ t('program.voucherType') }}</th>
              <th>{{ t('program.vendorType') }}</th>
              <th>{{ t('common.value') }}</th>
              <th v-if="!readOnly" class="table__actions" />
            </tr>
          </thead>
          <tbody>
            <tr v-for="(category, index) in categories" :key="category.Id ?? `new-${index}`">
              <td>{{ index + 1 }}</td>
              <td>
                <select v-model.number="category.TypeId" class="field__control" :disabled="readOnly">
                  <option v-for="type in voucherTypes" :key="type.Id" :value="type.Id">
                    {{ type.Name }}
                  </option>
                </select>
              </td>
              <td>
                <select v-model.number="category.VendorTypeId" class="field__control" :disabled="readOnly">
                  <option :value="null">{{ t('common.none') }}</option>
                  <option v-for="type in vendorTypes" :key="type.Id" :value="type.Id">
                    {{ type.Name }}
                  </option>
                </select>
              </td>
              <td>
                <div class="field__prefix">
                  <span class="field__prefix-label">{{ session.country?.CurrencyIsoCode }}</span>
                  <input
                    v-model.number="category.Value"
                    type="number"
                    step="0.01"
                    class="field__control"
                    :disabled="readOnly"
                  />
                </div>
              </td>
              <td v-if="!readOnly" class="table__actions">
                <button class="btn btn--sm btn--danger" type="button" @click="removeCategory(index)">
                  {{ t('action.remove') }}
                </button>
              </td>
            </tr>
          </tbody>
        </table>
        <p v-if="categories.length === 0" class="empty">
          {{ t('program.categoriesEmpty') }}
        </p>
      </div>
    </div>

    <div v-if="!isNew" class="panel">
      <div class="panel__head"><h2 style="margin: 0">{{ t('program.distributions') }}</h2></div>
      <div class="table-wrap">
        <table class="table">
          <thead>
            <tr>
              <th style="width: 60px">{{ t('common.id') }}</th>
              <th>{{ t('distribution.heading') }}</th>
              <th>{{ t('beneficiary.cycle') }}</th>
              <th>{{ t('beneficiary.location') }}</th>
              <th>{{ t('distribution.createdOn') }}</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="row in distributions" :key="row.Id">
              <td>{{ row.Id }}</td>
              <td>
                <RouterLink :to="{ name: 'distribution-detail', params: { id: row.Id } }">
                  {{ row.Title }}
                </RouterLink>
              </td>
              <td>{{ row.Group?.Name }}</td>
              <td>{{ row.Location?.Name }}</td>
              <td>{{ date(row.CreatedOn) }}</td>
            </tr>
          </tbody>
        </table>
        <p v-if="distributions.length === 0" class="empty">{{ t('program.noDistributions') }}</p>
      </div>
    </div>
  </form>

  <div v-if="showDistribute" class="modal" @click.self="showDistribute = false">
    <div class="modal__card" role="dialog" aria-modal="true" aria-label="Distribute vouchers">
      <h2>{{ t('distribute.action') }}</h2>

      <div class="field">
        <label class="field__label" for="cycle">{{ t('beneficiary.cycle') }} <span class="field__required">*</span></label>
        <select id="cycle" v-model.number="distributeForm.GroupId" class="field__control">
          <option :value="null">{{ t('common.none') }}</option>
          <option v-for="cycle in cycles" :key="cycle.Id" :value="cycle.Id">{{ cycle.Name }}</option>
        </select>
      </div>

      <div class="field">
        <label class="field__label" for="place">{{ t('beneficiary.location') }}</label>
        <select id="place" v-model.number="distributeForm.LocationId" class="field__control">
          <option :value="null">{{ t('common.none') }}</option>
          <option v-for="location in locations" :key="location.Id" :value="location.Id">
            {{ location.Name }}
          </option>
        </select>
      </div>

      <p class="muted" style="font-size: 13px">
        {{ t('distribute.explain') }}
      </p>

      <div class="modal__actions">
        <button
          class="btn btn--primary"
          :disabled="!distributeForm.GroupId || !distributeForm.LocationId || distributing"
          @click="distribute"
        >
          {{ distributing ? t('distribute.distributing') : t('distribute.create') }}
        </button>
        <button class="btn btn--ghost" @click="showDistribute = false">{{ t('action.cancel') }}</button>
      </div>
    </div>
  </div>
</template>
