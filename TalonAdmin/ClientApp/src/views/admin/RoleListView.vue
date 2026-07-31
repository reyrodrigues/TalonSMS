<script setup lang="ts">
import { onMounted, ref } from 'vue'
import * as api from '@/api/talon'
import type { ApplicationRole } from '@/api/types'
import { notify, notifyError } from '@/composables/useToast'

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
    notifyError(error, 'Could not load the roles.')
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
    notify('success', 'Role created.')
    await load()
  } catch (error) {
    notifyError(error, 'Could not create the role.')
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
    notify('success', 'Role renamed.')
    await load()
  } catch (error) {
    notifyError(error, 'Could not rename the role.')
  } finally {
    busy.value = false
  }
}

async function remove(role: ApplicationRole) {
  if (!window.confirm(`Delete the ${role.Name} role?`)) return

  busy.value = true
  try {
    await api.deleteRole(role.Id)
    notify('success', 'Role deleted.')
    await load()
  } catch (error) {
    notifyError(error, 'Could not delete the role.')
  } finally {
    busy.value = false
  }
}
</script>

<template>
  <h1>Roles</h1>

  <form class="panel panel__head" @submit.prevent="create">
    <input v-model="newName" class="input" placeholder="New role name" style="max-width: 420px" />
    <button class="btn btn--primary" type="submit" :disabled="!newName.trim() || busy">Add</button>
  </form>

  <div class="panel">
    <div class="table-wrap">
      <table class="table">
        <thead>
          <tr>
            <th>Name</th>
            <th class="table__actions">Actions</th>
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
                Rename
              </button>
              <button class="btn btn--sm btn--danger" :disabled="busy" @click="remove(role)">
                Delete
              </button>
            </td>
          </tr>
        </tbody>
      </table>

      <p v-if="loading" class="empty"><span class="spinner" aria-label="Loading" /></p>
      <p v-else-if="roles.length === 0" class="empty">No roles have been defined.</p>
    </div>
  </div>
</template>
