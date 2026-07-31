<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import * as api from '@/api/talon'
import type { ApplicationRole } from '@/api/types'
import { notify, notifyError } from '@/composables/useToast'

const { t } = useI18n()

/**
 * Roles are edited in place: there are only a handful, and the only editable field is the name.
 * Creating and renaming both go through the Identity endpoints, not Breeze.
 */
const roles = ref<ApplicationRole[]>([])
const editing = ref<Record<string, string>>({})
const newName = ref('')
const loading = ref(true)
const busy = ref(false)

onMounted(load)

async function load() {
  loading.value = true
  try {
    roles.value = await api.roles()
    editing.value = Object.fromEntries(roles.value.map(r => [r.Id, r.Name]))
  } catch (error) {
    notifyError(error, t('role.loadFailed'))
  } finally {
    loading.value = false
  }
}

async function create() {
  if (!newName.value.trim()) return

  busy.value = true
  try {
    await api.createRole(newName.value.trim())
    newName.value = ''
    notify('success', t('role.created'))
    await load()
  } catch (error) {
    notifyError(error, t('role.createFailed'))
  } finally {
    busy.value = false
  }
}

async function rename(role: ApplicationRole) {
  const name = (editing.value[role.Id] ?? '').trim()
  if (!name || name === role.Name) return

  busy.value = true
  try {
    await api.updateRole(role.Id, name)
    notify('success', t('role.renamed'))
    await load()
  } catch (error) {
    notifyError(error, t('role.renameFailed'))
  } finally {
    busy.value = false
  }
}

async function remove(role: ApplicationRole) {
  if (!window.confirm(t('role.confirmDelete', { name: role.Name }))) return

  busy.value = true
  try {
    await api.deleteRole(role.Id)
    notify('success', t('role.deleted'))
    await load()
  } catch (error) {
    notifyError(error, t('role.deleteFailed'))
  } finally {
    busy.value = false
  }
}
</script>

<template>
  <h1>{{ t('role.title') }}</h1>

  <form class="panel panel__head" @submit.prevent="create">
    <input v-model="newName" class="input" :placeholder="t('role.newName')" style="max-width: 420px" />
    <button class="btn btn--primary" type="submit" :disabled="!newName.trim() || busy">{{ t('action.add') }}</button>
  </form>

  <div class="panel">
    <div class="table-wrap">
      <table class="table">
        <thead>
          <tr>
            <th>{{ t('common.name') }}</th>
            <th class="table__actions">{{ t('common.actions') }}</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="role in roles" :key="role.Id">
            <td><input v-model="editing[role.Id]" class="field__control" /></td>
            <td class="table__actions">
              <button
                class="btn btn--sm btn--info"
                :disabled="busy || editing[role.Id] === role.Name"
                @click="rename(role)"
              >
                {{ t('action.rename') }}
              </button>
              <button class="btn btn--sm btn--danger" :disabled="busy" @click="remove(role)">
                {{ t('action.delete') }}
              </button>
            </td>
          </tr>
        </tbody>
      </table>

      <p v-if="loading" class="empty"><span class="spinner" :aria-label="t('common.loading')" /></p>
      <p v-else-if="roles.length === 0" class="empty">{{ t('role.empty') }}</p>
    </div>
  </div>
</template>
