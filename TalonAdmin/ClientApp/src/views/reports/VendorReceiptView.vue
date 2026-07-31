<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import * as api from '@/api/talon'
import { downloadBlob } from '@/api/http'
import type { Program, Vendor, VoucherTransactionRecord } from '@/api/types'
import { useSessionStore } from '@/stores/session'
import { useFormat } from '@/composables/useFormat'
import { notify, notifyError } from '@/composables/useToast'

const { t } = useI18n()

/**
 * Reconciliation: confirm which of a vendor's redemptions will be paid, then produce the payment
 * schedule they sign. Anything excluded here simply stays unreconciled for a later run.
 */
const session = useSessionStore()
const { dateTime, money } = useFormat()

const vendors = ref<Vendor[]>([])
const programs = ref<Program[]>([])
const vendorId = ref<number | null>(null)
const programId = ref<number | null>(null)

const records = ref<VoucherTransactionRecord[]>([])
const excluded = ref(new Set<number>())
const filter = ref('')
const loading = ref(false)
const busy = ref(false)

// The payment schedule covers a date range; the reconciliation list itself does not.
const period = ref({ start: '', end: '' })

const ready = computed(() => vendorId.value !== null && programId.value !== null)
const canSchedule = computed(() => ready.value && !!period.value.start && !!period.value.end)

const visible = computed(() => {
  const term = filter.value.trim().toLowerCase()
  if (!term) return records.value

  return records.value.filter(
    r =>
      (r.ConfirmationCode ?? '').toLowerCase().includes(term) ||
      (r.Voucher?.VoucherCode ?? '').toLowerCase().includes(term)
  )
})

const payable = computed(() =>
  visible.value.filter(r => !excluded.value.has(r.Id) && !r.ReconciledOn)
)

const total = computed(() => payable.value.reduce((sum, r) => sum + (r.Value ?? 0), 0))

onMounted(async () => {
  try {
    const [vendorList, programList] = await Promise.all([api.vendors(), api.programs()])
    vendors.value = vendorList.filter(v => !v.ParentRecordId)
    programs.value = programList
  } catch (error) {
    notifyError(error, t('report.receipt.listFailed'))
  }
})

async function load() {
  if (!ready.value) return

  loading.value = true
  excluded.value = new Set()
  try {
    records.value = await api.reconcilableTransactions(vendorId.value!, programId.value!)
  } catch (error) {
    notifyError(error, t('report.receipt.loadFailed'))
  } finally {
    loading.value = false
  }
}

function toggle(record: VoucherTransactionRecord) {
  const next = new Set(excluded.value)
  if (next.has(record.Id)) next.delete(record.Id)
  else next.add(record.Id)
  excluded.value = next
}

async function reconcileAll() {
  if (payable.value.length === 0 || !session.user) return

  busy.value = true
  try {
    await api.reconcile(payable.value, session.user.UserName, new Date().toISOString())
    notify('success', t('report.receipt.done', { count: payable.value.length }))
    await load()
  } catch (error) {
    notifyError(error, t('report.receipt.failed'))
  } finally {
    busy.value = false
  }
}

async function paymentSchedule() {
  if (!ready.value || !session.tenant || !period.value.start || !period.value.end) return

  busy.value = true
  try {
    const blob = await api.paymentScheduleReport({
      VendorId: vendorId.value!,
      ProgramId: programId.value!,
      CountryId: session.tenant.countryId,
      OrganizationId: session.tenant.organizationId,
      PeriodStart: period.value.start,
      PeriodEnd: period.value.end,
      TimezoneOffset: -new Date().getTimezoneOffset() / 60
    })

    downloadBlob(blob, `payment-schedule-${vendorId.value}.xlsx`)
  } catch (error) {
    notifyError(error, t('report.receipt.scheduleFailed'))
  } finally {
    busy.value = false
  }
}
</script>

<template>
  <h1>{{ t('report.receipt.title') }}</h1>

  <div class="panel">
    <div class="panel__body grid-2">
      <div class="field">
        <label class="field__label" for="vendor">{{ t('vendor.singular') }} <span class="field__required">*</span></label>
        <select id="vendor" v-model.number="vendorId" class="field__control">
          <option :value="null">{{ t('common.none') }}</option>
          <option v-for="item in vendors" :key="item.Id" :value="item.Id">
            {{ item.Name || `${item.FirstName ?? ''} ${item.LastName ?? ''}`.trim() }}
          </option>
        </select>
      </div>

      <div class="field">
        <label class="field__label" for="program">{{ t('program.singular') }} <span class="field__required">*</span></label>
        <select id="program" v-model.number="programId" class="field__control">
          <option :value="null">{{ t('common.none') }}</option>
          <option v-for="item in programs" :key="item.Id" :value="item.Id">{{ item.Name }}</option>
        </select>
      </div>

      <div class="field">
        <label class="field__label" for="start">{{ t('report.receipt.periodStart') }}</label>
        <input id="start" v-model="period.start" type="date" class="field__control" />
      </div>

      <div class="field">
        <label class="field__label" for="end">{{ t('report.receipt.periodEnd') }}</label>
        <input id="end" v-model="period.end" type="date" class="field__control" />
      </div>
    </div>

    <div class="panel__head">
      <button class="btn btn--primary" :disabled="!ready || loading" @click="load">
        {{ t('report.receipt.load') }}
      </button>
      <div class="panel__spacer" />
      <p class="muted" style="margin: 0; font-size: 13px">
        {{ t('report.receipt.periodHint') }}
      </p>
      <button class="btn btn--info" :disabled="!canSchedule || busy" @click="paymentSchedule">
        {{ t('report.receipt.paymentSchedule') }}
      </button>
    </div>
  </div>

  <div v-if="records.length > 0" class="panel panel__head">
    <input v-model="filter" class="input" :placeholder="t('report.receipt.filter')"
           style="max-width: 420px" />
    <div class="panel__spacer" />
    <strong>{{ t('report.receipt.toPay', { count: payable.length, total: money(total) }) }}</strong>
    <button class="btn btn--primary" :disabled="payable.length === 0 || busy" @click="reconcileAll">
      {{ busy ? t('report.receipt.reconciling') : t('report.receipt.reconcileAll') }}
    </button>
  </div>

  <div class="panel">
    <div class="table-wrap">
      <table class="table">
        <thead>
          <tr>
            <th style="width: 70px">{{ t('common.id') }}</th>
            <th>{{ t('beneficiary.singular') }}</th>
            <th>{{ t('distribution.voucherCode') }}</th>
            <th>{{ t('report.receipt.confirmation') }}</th>
            <th>{{ t('report.receipt.redeemedOn') }}</th>
            <th class="table__numeric">{{ t('common.value') }}</th>
            <th class="table__actions">{{ t('common.status') }}</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="record in visible" :key="record.Id">
            <td>{{ record.Id }}</td>
            <td>
              {{ record.Beneficiary?.Name
                || `${record.Beneficiary?.FirstName ?? ''} ${record.Beneficiary?.LastName ?? ''}`.trim() }}
            </td>
            <td>{{ record.Voucher?.VoucherCode }}</td>
            <td>{{ record.ConfirmationCode }}</td>
            <td>{{ dateTime(record.CreatedOn) }}</td>
            <td class="table__numeric">{{ money(record.Value) }}</td>
            <td class="table__actions">
              <span v-if="record.ReconciledOn" class="badge badge--success">{{ t('report.receipt.reconciled') }}</span>
              <button v-else class="btn btn--sm" @click="toggle(record)">
                {{ excluded.has(record.Id) ? t('report.receipt.include') : t('report.receipt.holdBack') }}
              </button>
            </td>
          </tr>
        </tbody>
      </table>

      <p v-if="loading" class="empty"><span class="spinner" :aria-label="t('common.loading')" /></p>
      <p v-else-if="records.length === 0" class="empty">
        {{ t('report.receipt.empty') }}
      </p>
    </div>
  </div>
</template>
