<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { RouterLink, useRoute, useRouter } from 'vue-router'
import * as api from '@/api/talon'
import type { ApplicationUser } from '@/api/types'
import { notify, notifyError } from '@/composables/useToast'

const { t } = useI18n()

/**
 * Users live behind three different Breeze endpoints depending on who is asking: system admins see
 * `Administrators`, organization admins see `OrganizationUsers`. The route says which.
 */
const route = useRoute()
const router = useRouter()

const scope = route.meta.userScope as 'Administrators' | 'OrganizationUsers'
const title = route.meta.title as string

/**
 * Each list is scoped by role, so a user created here shows up on the other screen unless they are
 * given a matching role. Saying so avoids the "I just created them and they vanished" surprise.
 */
const scopeNote =
  scope === 'Administrators' ? 'user.scopeAdministrators' : 'user.scopeOrganization'

const users = ref<ApplicationUser[]>([])
const search = ref('')
const loading = ref(true)

onMounted(load)

async function load() {
  loading.value = true
  try {
    users.value = await api.users(scope)
  } catch (error) {
    notifyError(error, t('user.loadFailed'))
  } finally {
    loading.value = false
  }
}

async function remove(user: ApplicationUser) {
  if (!window.confirm(t('user.confirmDelete', { name: user.UserName }))) return

  try {
    await api.deleteUser(user.Id)
    notify('success', t('toast.deleted', { what: t('user.singular') }))
    await load()
  } catch (error) {
    notifyError(error, t('user.deleteFailed'))
  }
}

const matches = (user: ApplicationUser) => {
  const term = search.value.trim().toLowerCase()
  if (!term) return true

  return [user.UserName, user.FullName, user.Email].some(v => (v ?? '').toLowerCase().includes(term))
}
</script>

<template>
  <h1>{{ t(title) }}</h1>
  <p class="muted" style="margin-top: -8px">{{ t(scopeNote) }}</p>

  <div class="panel panel__head">
    <input v-model="search" class="input" :placeholder="t('action.search')" style="max-width: 420px" />
    <div class="panel__spacer" />
    <button class="btn btn--primary" @click="router.push({ name: `${String(route.name)}-create` })">
      {{ t('action.new') }}
    </button>
  </div>

  <div class="panel">
    <div class="table-wrap">
      <table class="table">
        <thead>
          <tr>
            <th>{{ t('login.userName') }}</th>
            <th>{{ t('user.fullName') }}</th>
            <th>{{ t('user.email') }}</th>
            <th>{{ t('user.organization') }}</th>
            <th class="table__actions">{{ t('common.actions') }}</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="user in users.filter(matches)" :key="user.Id">
            <td>{{ user.UserName }}</td>
            <td>{{ user.FullName }}</td>
            <td>{{ user.Email }}</td>
            <td>{{ user.Organization?.Name }}</td>
            <td class="table__actions">
              <RouterLink
                class="btn btn--sm btn--info"
                :to="{ name: `${String(route.name)}-edit`, params: { id: user.Id } }"
              >
                {{ t('action.edit') }}
              </RouterLink>
              <button class="btn btn--sm btn--danger" @click="remove(user)">{{ t('action.delete') }}</button>
            </td>
          </tr>
        </tbody>
      </table>

      <p v-if="loading" class="empty"><span class="spinner" :aria-label="t('common.loading')" /></p>
      <p v-else-if="users.filter(matches).length === 0" class="empty">{{ t('user.empty') }}</p>
    </div>
  </div>
</template>
