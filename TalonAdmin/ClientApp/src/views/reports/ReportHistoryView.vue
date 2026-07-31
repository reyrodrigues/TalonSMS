<script setup lang="ts">
import { onMounted, ref, watch } from 'vue'
import * as api from '@/api/talon'
import { downloadBlob } from '@/api/http'
import type { ExportedReport, Program } from '@/api/types'
import { useFormat } from '@/composables/useFormat'
import { notify, notifyError } from '@/composables/useToast'

/**
 * Reports the server archived when they were generated. The PDF and spreadsheet ride on the row as
 * base64, and a counter-signed copy can be attached back onto it.
 */
const { dateTime } = useFormat()

const programs = ref<Program[]>([])
const programId = ref<number | null>(null)
const reportType = ref<number>(api.REPORT_TYPES[0].Id)
const reports = ref<ExportedReport[]>([])
const loading = ref(false)
const uploadFor = ref<number | null>(null)
const fileInput = ref<HTMLInputElement | null>(null)

onMounted(async () => {
  try {
    programs.value = await api.programs()
    programId.value = programs.value[0]?.Id ?? null
  } catch (error) {
    notifyError(error, 'Could not load the programmes.')
  }
})

watch([programId, reportType], load, { immediate: false })

async function load() {
  if (programId.value === null) {
    reports.value = []
    return
  }

  loading.value = true
  try {
    reports.value = await api.exportedReports(programId.value, reportType.value)
  } catch (error) {
    notifyError(error, 'Could not load the report history.')
  } finally {
    loading.value = false
  }
}

/** The blobs arrive as base64 strings, so they are decoded here rather than re-fetched. */
function save(base64: string | null, fileName: string, mimeType: string) {
  if (!base64) return

  const binary = atob(base64)
  const bytes = Uint8Array.from(binary, char => char.charCodeAt(0))
  downloadBlob(new Blob([bytes], { type: mimeType }), fileName)
}

function pickSignedReport(report: ExportedReport) {
  uploadFor.value = report.Id
  fileInput.value?.click()
}

async function attachSignedReport(event: Event) {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  const report = reports.value.find(r => r.Id === uploadFor.value)

  input.value = ''
  if (!file || !report) return

  try {
    const buffer = await file.arrayBuffer()
    let binary = ''
    for (const byte of new Uint8Array(buffer)) binary += String.fromCharCode(byte)

    await api.attachSignedReport(report, btoa(binary))
    notify('success', 'Signed report attached.')
    await load()
  } catch (error) {
    notifyError(error, 'Could not attach the signed report.')
  }
}
</script>

<template>
  <h1>Report History</h1>

  <div class="panel">
    <div class="panel__body grid-2">
      <div class="field">
        <label class="field__label" for="program">Program</label>
        <select id="program" v-model.number="programId" class="field__control">
          <option :value="null">—</option>
          <option v-for="item in programs" :key="item.Id" :value="item.Id">{{ item.Name }}</option>
        </select>
      </div>

      <div class="field">
        <label class="field__label" for="type">Report Type</label>
        <select id="type" v-model.number="reportType" class="field__control">
          <option v-for="type in api.REPORT_TYPES" :key="type.Id" :value="type.Id">
            {{ type.Name }}
          </option>
        </select>
      </div>
    </div>
  </div>

  <div class="panel">
    <div class="table-wrap">
      <table class="table">
        <thead>
          <tr>
            <th style="width: 70px">#</th>
            <th>Description</th>
            <th>Run On</th>
            <th>Run By</th>
            <th class="table__actions">Downloads</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="report in reports" :key="report.Id">
            <td>{{ report.Id }}</td>
            <td>{{ report.Description }}</td>
            <td>{{ dateTime(report.ReportRunOn) }}</td>
            <td>{{ report.ReportRunBy }}</td>
            <td class="table__actions">
              <button
                class="btn btn--sm"
                :disabled="!report.OriginalReport"
                @click="save(report.OriginalReport, `report-${report.Id}.pdf`, 'application/pdf')"
              >
                PDF
              </button>
              <button
                class="btn btn--sm"
                :disabled="!report.Spreadsheet"
                @click="
                  save(
                    report.Spreadsheet,
                    `report-${report.Id}.xlsx`,
                    'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'
                  )
                "
              >
                Spreadsheet
              </button>
              <button
                v-if="report.SignedReport"
                class="btn btn--sm btn--info"
                @click="save(report.SignedReport, `signed-${report.Id}.pdf`, 'application/pdf')"
              >
                Signed
              </button>
              <button v-else class="btn btn--sm" @click="pickSignedReport(report)">
                Attach signed
              </button>
            </td>
          </tr>
        </tbody>
      </table>

      <p v-if="loading" class="empty"><span class="spinner" aria-label="Loading" /></p>
      <p v-else-if="reports.length === 0" class="empty">
        No reports of this kind have been archived for this programme.
      </p>
    </div>
  </div>

  <input
    ref="fileInput"
    type="file"
    accept="application/pdf"
    style="display: none"
    @change="attachSignedReport"
  />
</template>
