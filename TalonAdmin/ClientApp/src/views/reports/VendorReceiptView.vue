<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import * as api from '@/api/talon'
import { downloadBlob } from '@/api/http'
import type { Program, Vendor, VoucherTransactionRecord } from '@/api/types'
import { useSessionStore } from '@/stores/session'
import { useFormat } from '@/composables/useFormat'
import { notify, notifyError } from '@/composables/useToast'

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
    notifyError(error, 'Could not load the vendors and programmes.')
  }
})

async function load() {
  if (!ready.value) return

  loading.value = true
  excluded.value = new Set()
  try {
    records.value = await api.reconcilableTransactions(vendorId.value!, programId.value!)
  } catch (error) {
    notifyError(error, 'Could not load the transactions.')
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
    notify('success', `${payable.value.length} transactions reconciled.`)
    await load()
  } catch (error) {
    notifyError(error, 'Could not reconcile the transactions.')
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
    notifyError(error, 'Could not generate the payment schedule.')
  } finally {
    busy.value = false
  }
}
</script>

<template>
  <h1>Vendor Receipt</h1>

  <div class="panel">
    <div class="panel__body grid-2">
      <div class="field">
        <label class="field__label" for="vendor">Vendor <span class="field__required">*</span></label>
        <select id="vendor" v-model.number="vendorId" class="field__control">
          <option :value="null">—</option>
          <option v-for="item in vendors" :key="item.Id" :value="item.Id">
            {{ item.Name || `${item.FirstName ?? ''} ${item.LastName ?? ''}`.trim() }}
          </option>
        </select>
      </div>

      <div class="field">
        <label class="field__label" for="program">Program <span class="field__required">*</span></label>
        <select id="program" v-model.number="programId" class="field__control">
          <option :value="null">—</option>
          <option v-for="item in programs" :key="item.Id" :value="item.Id">{{ item.Name }}</option>
        </select>
      </div>

      <div class="field">
        <label class="field__label" for="start">Period Start</label>
        <input id="start" v-model="period.start" type="date" class="field__control" />
      </div>

      <div class="field">
        <label class="field__label" for="end">Period End</label>
        <input id="end" v-model="period.end" type="date" class="field__control" />
      </div>
    </div>

    <div class="panel__head">
      <button class="btn btn--primary" :disabled="!ready || loading" @click="load">
        Load Transactions
      </button>
      <div class="panel__spacer" />
      <p class="muted" style="margin: 0; font-size: 13px">
        The payment schedule covers finalized transactions reconciled inside the period above.
      </p>
      <button class="btn btn--info" :disabled="!canSchedule || busy" @click="paymentSchedule">
        Payment Schedule
      </button>
    </div>
  </div>

  <div v-if="records.length > 0" class="panel panel__head">
    <input v-model="filter" class="input" placeholder="Filter by voucher or confirmation code"
           style="max-width: 420px" />
    <div class="panel__spacer" />
    <strong>{{ payable.length }} to pay — {{ money(total) }}</strong>
    <button class="btn btn--primary" :disabled="payable.length === 0 || busy" @click="reconcileAll">
      {{ busy ? 'Reconciling…' : 'Reconcile All' }}
    </button>
  </div>

  <div class="panel">
    <div class="table-wrap">
      <table class="table">
        <thead>
          <tr>
            <th style="width: 70px">#</th>
            <th>Beneficiary</th>
            <th>Voucher Code</th>
            <th>Confirmation</th>
            <th>Redeemed On</th>
            <th class="table__numeric">Value</th>
            <th class="table__actions">Status</th>
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
              <span v-if="record.ReconciledOn" class="badge badge--success">Reconciled</span>
              <button v-else class="btn btn--sm" @click="toggle(record)">
                {{ excluded.has(record.Id) ? 'Include' : 'Hold back' }}
              </button>
            </td>
          </tr>
        </tbody>
      </table>

      <p v-if="loading" class="empty"><span class="spinner" aria-label="Loading" /></p>
      <p v-else-if="records.length === 0" class="empty">
        Pick a vendor and a programme, then load their outstanding transactions.
      </p>
    </div>
  </div>
</template>
