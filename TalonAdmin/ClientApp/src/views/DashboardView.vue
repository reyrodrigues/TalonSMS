<script setup lang="ts">
import { onMounted, ref } from 'vue'
import * as api from '@/api/talon'
import type { DashboardProgram } from '@/api/types'
import LineChart from '@/components/LineChart.vue'
import { useFormat } from '@/composables/useFormat'
import { notifyError } from '@/composables/useToast'

const { money, number } = useFormat()

const programmes = ref<DashboardProgram[]>([])
const loading = ref(true)

onMounted(async () => {
  try {
    programmes.value = await api.dashboard()
  } catch (error) {
    notifyError(error, 'Could not load the dashboard.')
  } finally {
    loading.value = false
  }
})

/** The server sends [date, issued, unused, used] tuples. */
const chartFor = (programme: DashboardProgram) => ({
  labels: programme.UsedVsIssued.map(point => new Date(point[0]).toLocaleDateString()),
  series: [
    {
      label: 'Distributed vouchers',
      data: programme.UsedVsIssued.map(point => point[1]),
      color: '#23b7e5'
    },
    {
      label: 'Used vouchers',
      data: programme.UsedVsIssued.map(point => point[3]),
      color: '#27c24c'
    }
  ]
})
</script>

<template>
  <h1>Dashboard</h1>

  <p v-if="loading" class="empty"><span class="spinner" aria-label="Loading" /></p>

  <p v-else-if="programmes.length === 0" class="panel empty">
    No programme has been distributed in this country yet.
  </p>

  <section v-for="programme in programmes" :key="programme.Program.Id" class="panel">
    <header class="programme__head">Program: {{ programme.Program.Name }}</header>

    <div class="panel__body">
      <LineChart v-bind="chartFor(programme)" />

      <div class="tiles" style="margin-top: 18px">
        <div class="tile">
          <div class="tile__value">{{ number(programme.Beneficiaries) }}</div>
          <div class="tile__label">Total Beneficiaries</div>
        </div>
        <div class="tile">
          <div class="tile__value">{{ number(programme.TotalVouchers) }}</div>
          <div class="tile__label">Vouchers Issued</div>
        </div>
        <div class="tile tile--dark">
          <div class="tile__value">{{ money(programme.IssuedAmount) }}</div>
          <div class="tile__label">Issued</div>
        </div>
        <div class="tile">
          <div class="tile__value">{{ number(programme.UniqueBeneficiaries) }}</div>
          <div class="tile__label">Beneficiaries Reached</div>
        </div>
        <div class="tile">
          <div class="tile__value">{{ number(programme.VouchersUsed) }}</div>
          <div class="tile__label">Vouchers Used</div>
        </div>
        <div class="tile tile--dark">
          <div class="tile__value">{{ money(programme.ClaimedAmount) }}</div>
          <div class="tile__label">Claimed</div>
        </div>
      </div>

      <div class="table-wrap" style="margin-top: 18px">
        <table class="table">
          <thead>
            <tr>
              <th>Distribution</th>
              <th>Location</th>
              <th class="table__numeric">Beneficiaries</th>
              <th class="table__numeric">Vouchers Issued</th>
              <th class="table__numeric">Vouchers Used</th>
              <th class="table__numeric">Vendors</th>
              <th class="table__numeric">Total Claimed</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="(row, index) in programme.Distributions" :key="row.Distribution?.Id ?? index">
              <td>{{ row.Distribution?.Title }}</td>
              <td>{{ row.Location?.Name }}</td>
              <td class="table__numeric">{{ number(row.Beneficiaries) }}</td>
              <td class="table__numeric">{{ number(row.TotalVouchers) }}</td>
              <td class="table__numeric">{{ number(row.VouchersUsed) }}</td>
              <td class="table__numeric">{{ number(row.TotalVendors) }}</td>
              <td class="table__numeric">{{ money(row.ClaimedAmount) }}</td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>
  </section>
</template>

<style scoped>
.programme__head {
  padding: 12px 16px;
  background: var(--accent);
  color: var(--accent-ink);
  font-weight: 600;
}
</style>
