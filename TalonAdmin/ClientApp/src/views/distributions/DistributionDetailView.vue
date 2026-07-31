<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import * as api from '@/api/talon'
import { downloadBlob } from '@/api/http'
import type { Beneficiary, Distribution, VoucherTransactionRecord } from '@/api/types'
import { useSessionStore } from '@/stores/session'
import { useFormat } from '@/composables/useFormat'
import { notify, notifyError } from '@/composables/useToast'

/**
 * What a distribution actually produced: the vouchers still outstanding, the ones that have been
 * redeemed, and the households it reached. Vouchers are cancelled and re-sent from here.
 */
const props = defineProps<{ id: string }>()

const session = useSessionStore()
const router = useRouter()
const { dateTime, money } = useFormat()

type Tab = 'unused' | 'used' | 'beneficiaries'

const distribution = ref<Distribution | null>(null)
const unused = ref<VoucherTransactionRecord[]>([])
const used = ref<VoucherTransactionRecord[]>([])
const beneficiaries = ref<Beneficiary[]>([])
const tab = ref<Tab>('unused')
const loading = ref(true)
const busy = ref(false)

const id = computed(() => Number(props.id))

/** SMS is the only mechanism where cancelling or re-sending a voucher means anything. */
const isSms = computed(() => distribution.value?.Program?.DistributionMechanism === 1)

const rows = computed(() => (tab.value === 'used' ? used.value : unused.value))

onMounted(load)

async function load() {
  loading.value = true
  try {
    const [found, unusedList, usedList, people] = await Promise.all([
      api.distribution(id.value),
      api.distributionTransactions(id.value, false),
      api.distributionTransactions(id.value, true),
      api.distributionBeneficiaries(id.value)
    ])

    if (!found) {
      notifyError(null, 'That distribution no longer exists.')
      await router.replace({ name: 'distributions' })
      return
    }

    distribution.value = found
    unused.value = unusedList
    used.value = usedList
    beneficiaries.value = people
  } catch (error) {
    notifyError(error, 'Could not load the distribution.')
  } finally {
    loading.value = false
  }
}

async function cancel(record: VoucherTransactionRecord) {
  if (!record.VoucherId) return
  if (!window.confirm(`Cancel voucher ${record.Voucher?.VoucherCode ?? record.VoucherId}?`)) return

  busy.value = true
  try {
    await api.cancelVoucher(record.VoucherId)
    notify('success', 'Voucher cancelled.')
    await load()
  } catch (error) {
    notifyError(error, 'Could not cancel the voucher.')
  } finally {
    busy.value = false
  }
}

async function resend(record: VoucherTransactionRecord) {
  if (!record.VoucherId) return

  busy.value = true
  try {
    await api.resendVoucherSms(record.BeneficiaryId, record.VoucherId)
    notify('success', 'Voucher re-sent.')
  } catch (error) {
    notifyError(error, 'Could not re-send the voucher.')
  } finally {
    busy.value = false
  }
}

async function download(kind: 'report' | 'testSheet') {
  if (!session.tenant) return

  busy.value = true
  try {
    const blob =
      kind === 'testSheet'
        ? await api.testSheet(id.value)
        : await api.distributionReport({
            DistributionId: id.value,
            CountryId: session.tenant.countryId,
            OrganizationId: session.tenant.organizationId,
            PaperSize: 'A4',
            TimezoneOffset: -new Date().getTimezoneOffset() / 60
          })

    downloadBlob(blob, kind === 'testSheet' ? `test-sheet-${id.value}.pdf` : `distribution-${id.value}.pdf`)
  } catch (error) {
    notifyError(error, 'Could not generate the document.')
  } finally {
    busy.value = false
  }
}
</script>

<template>
  <h1>Distribution</h1>

  <p v-if="loading" class="empty"><span class="spinner" aria-label="Loading" /></p>

  <template v-else-if="distribution">
    <div class="panel panel__head">
      <button class="btn btn--info" :disabled="busy" @click="download('report')">
        Distribution Report
      </button>
      <button class="btn" :disabled="busy" @click="download('testSheet')">QR Test Sheet</button>
      <div class="panel__spacer" />
      <button class="btn btn--ghost" @click="router.push({ name: 'distributions' })">Back</button>
    </div>

    <div class="panel">
      <dl class="summary">
        <div><dt>Title</dt><dd>{{ distribution.Title }}</dd></div>
        <div><dt>Program</dt><dd>{{ distribution.Program?.Name }}</dd></div>
        <div><dt>Cycle</dt><dd>{{ distribution.Group?.Name }}</dd></div>
        <div><dt>Location</dt><dd>{{ distribution.Location?.Name }}</dd></div>
        <div><dt>Fund Codes</dt><dd>{{ distribution.FundCodes }}</dd></div>
        <div>
          <dt>Status</dt>
          <dd>{{ distribution.IsClosed ? 'Closed' : 'Open' }}</dd>
        </div>
      </dl>
    </div>

    <div class="tabs" role="tablist">
      <button
        v-for="option in (['unused', 'used', 'beneficiaries'] as Tab[])"
        :key="option"
        class="tabs__tab"
        :class="{ 'tabs__tab--active': tab === option }"
        role="tab"
        :aria-selected="tab === option"
        @click="tab = option"
      >
        {{ option === 'unused' ? 'Unused Vouchers' : option === 'used' ? 'Used Vouchers' : 'Beneficiaries' }}
        ({{ option === 'unused' ? unused.length : option === 'used' ? used.length : beneficiaries.length }})
      </button>
    </div>

    <div class="panel">
      <div v-if="tab === 'beneficiaries'" class="table-wrap">
        <table class="table">
          <thead>
            <tr><th style="width: 70px">#</th><th>Name</th><th>Mobile</th><th>Location</th></tr>
          </thead>
          <tbody>
            <tr v-for="person in beneficiaries" :key="person.Id">
              <td>{{ person.Id }}</td>
              <td>{{ person.Name || `${person.FirstName ?? ''} ${person.LastName ?? ''}`.trim() }}</td>
              <td>{{ person.MobileNumber }}</td>
              <td>{{ person.Location?.Name }}</td>
            </tr>
          </tbody>
        </table>
        <p v-if="beneficiaries.length === 0" class="empty">No households were reached.</p>
      </div>

      <div v-else class="table-wrap">
        <table class="table">
          <thead>
            <tr>
              <th style="width: 70px">#</th>
              <th>Beneficiary</th>
              <th>Voucher Code</th>
              <th>Serial Number</th>
              <th class="table__numeric">Value</th>
              <th>{{ tab === 'used' ? 'Used On' : 'Created On' }}</th>
              <th v-if="tab === 'unused' && isSms" class="table__actions">Actions</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="record in rows" :key="record.Id">
              <td>{{ record.Id }}</td>
              <td>
                {{ record.Beneficiary?.Name
                  || `${record.Beneficiary?.FirstName ?? ''} ${record.Beneficiary?.LastName ?? ''}`.trim() }}
              </td>
              <td>{{ record.Voucher?.VoucherCode }}</td>
              <td>{{ record.Voucher?.SequentialCode }}</td>
              <td class="table__numeric">{{ money(record.Value) }}</td>
              <td>{{ dateTime(record.CreatedOn) }}</td>
              <td v-if="tab === 'unused' && isSms" class="table__actions">
                <button class="btn btn--sm" :disabled="busy" @click="resend(record)">Resend</button>
                <button class="btn btn--sm btn--danger" :disabled="busy" @click="cancel(record)">
                  Cancel
                </button>
              </td>
            </tr>
          </tbody>
        </table>
        <p v-if="rows.length === 0" class="empty">
          {{ tab === 'used' ? 'No vouchers have been redeemed yet.' : 'Every voucher has been redeemed.' }}
        </p>
      </div>
    </div>
  </template>
</template>
