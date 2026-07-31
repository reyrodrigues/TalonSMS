<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute } from 'vue-router'
import * as api from '@/api/talon'
import { downloadBlob } from '@/api/http'
import type { ReportSpec } from './spec'
import { useSessionStore } from '@/stores/session'
import { notifyError } from '@/composables/useToast'

/** Renders whichever report spec the route carries in `meta.report`. */
const route = useRoute()
const session = useSessionStore()

const spec = route.meta.report as ReportSpec

const programs = ref<Array<{ Id: number; Name: string }>>([])
const distributions = ref<Array<{ Id: number; Label: string }>>([])
const vendors = ref<Array<{ Id: number; Name: string }>>([])

const selection = ref<{ ProgramId: number | null; DistributionId: number | null; VendorId: number | null }>({
  ProgramId: null,
  DistributionId: null,
  VendorId: null
})
const paperSize = ref('A4')
const loading = ref(true)
const running = ref(false)

const required = computed(() => spec.selectors[0])

const ready = computed(() => {
  if (required.value === 'program') return selection.value.ProgramId !== null
  if (required.value === 'distribution') return selection.value.DistributionId !== null
  return selection.value.VendorId !== null
})

onMounted(async () => {
  try {
    const wanted = new Set(spec.selectors)
    const [programList, distributionList, vendorList] = await Promise.all([
      wanted.has('program') ? api.programs() : Promise.resolve([]),
      wanted.has('distribution') ? api.distributions() : Promise.resolve([]),
      wanted.has('vendor') ? api.vendors() : Promise.resolve([])
    ])

    programs.value = programList
    distributions.value = distributionList.map(d => ({
      Id: d.Id,
      Label: `${d.Program?.Name ? `${d.Program.Name} — ` : ''}${d.Title ?? `Distribution ${d.Id}`}`
    }))
    // Sub-vendors are billed through their parent, so they are not offered on their own.
    vendors.value = vendorList
      .filter(v => !v.ParentRecordId)
      .map(v => ({ Id: v.Id, Name: v.Name ?? `${v.FirstName ?? ''} ${v.LastName ?? ''}`.trim() }))
  } catch (error) {
    notifyError(error, 'Could not load the report options.')
  } finally {
    loading.value = false
  }
})

async function run() {
  if (!ready.value || !session.tenant) return

  running.value = true
  try {
    const blob = await spec.run({
      ProgramId: selection.value.ProgramId ?? undefined,
      DistributionId: selection.value.DistributionId ?? undefined,
      VendorId: selection.value.VendorId ?? undefined,
      CountryId: session.tenant.countryId,
      OrganizationId: session.tenant.organizationId,
      PaperSize: paperSize.value,
      TimezoneOffset: -new Date().getTimezoneOffset() / 60
    })

    downloadBlob(blob, `${spec.fileName}.pdf`)
  } catch (error) {
    notifyError(error, 'The report could not be generated.')
  } finally {
    running.value = false
  }
}
</script>

<template>
  <h1>{{ spec.title }}</h1>

  <p v-if="loading" class="empty"><span class="spinner" aria-label="Loading" /></p>

  <form v-else class="panel" @submit.prevent="run">
    <div class="panel__body grid-2">
      <div v-if="spec.selectors.includes('program')" class="field">
        <label class="field__label" for="program">
          Program <span v-if="required === 'program'" class="field__required">*</span>
        </label>
        <select id="program" v-model.number="selection.ProgramId" class="field__control">
          <option :value="null">—</option>
          <option v-for="item in programs" :key="item.Id" :value="item.Id">{{ item.Name }}</option>
        </select>
      </div>

      <div v-if="spec.selectors.includes('distribution')" class="field">
        <label class="field__label" for="distribution">
          Distribution <span v-if="required === 'distribution'" class="field__required">*</span>
        </label>
        <select id="distribution" v-model.number="selection.DistributionId" class="field__control">
          <option :value="null">—</option>
          <option v-for="item in distributions" :key="item.Id" :value="item.Id">{{ item.Label }}</option>
        </select>
      </div>

      <div v-if="spec.selectors.includes('vendor')" class="field">
        <label class="field__label" for="vendor">
          Vendor <span v-if="required === 'vendor'" class="field__required">*</span>
        </label>
        <select id="vendor" v-model.number="selection.VendorId" class="field__control">
          <option :value="null">—</option>
          <option v-for="item in vendors" :key="item.Id" :value="item.Id">{{ item.Name }}</option>
        </select>
      </div>

      <div class="field">
        <label class="field__label" for="paper">Paper Size</label>
        <select id="paper" v-model="paperSize" class="field__control">
          <option value="A4">A4</option>
          <option value="Letter">Letter</option>
        </select>
      </div>
    </div>

    <div class="panel__head">
      <p class="muted" style="margin: 0; font-size: 13px">{{ spec.description }}</p>
      <div class="panel__spacer" />
      <button class="btn btn--primary" type="submit" :disabled="!ready || running">
        {{ running ? 'Generating…' : 'Download PDF' }}
      </button>
    </div>
  </form>
</template>
