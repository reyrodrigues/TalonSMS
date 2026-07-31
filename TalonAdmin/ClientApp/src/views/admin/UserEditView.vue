<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRoute, useRouter } from 'vue-router'
import * as api from '@/api/talon'
import type { ApplicationRole, ApplicationUser, Organization } from '@/api/types'
import { useSessionStore } from '@/stores/session'
import { notifyError, notifySaved } from '@/composables/useToast'

const { t } = useI18n()

/**
 * A user is created through the Identity endpoints rather than Breeze — Identity owns password
 * hashing and the role table — so this form posts JSON and then reconciles role membership.
 */
const props = defineProps<{ id?: string }>()

const route = useRoute()
const router = useRouter()
const session = useSessionStore()

const scope = route.meta.userScope as 'Administrators' | 'OrganizationUsers'
const listRoute = String(route.name).replace(/-(create|edit)$/, '')

const model = ref({
  UserName: '',
  FullName: '',
  Email: '',
  Password: '',
  ConfirmPassword: '',
  OrganizationId: 0
})

const organizations = ref<Organization[]>([])
const roles = ref<ApplicationRole[]>([])
const assigned = ref(new Set<string>())
const originalAssigned = ref(new Set<string>())

const loading = ref(true)
const saving = ref(false)
const submitted = ref(false)

const isNew = computed(() => !props.id)

const problems = computed(() => {
  const found: string[] = []
  if (!model.value.UserName.trim()) found.push(t('user.problems.userName'))
  if (!model.value.Email.trim()) found.push(t('user.problems.email'))
  if (!model.value.FullName.trim()) found.push(t('user.problems.fullName'))
  if (!model.value.OrganizationId) found.push(t('user.problems.organization'))
  if (isNew.value && model.value.Password.length < 6) {
    found.push(t('user.problems.passwordLength'))
  }
  if (model.value.Password !== model.value.ConfirmPassword) {
    found.push(t('user.problems.passwordMismatch'))
  }
  return found
})

onMounted(async () => {
  try {
    const [organizationList, roleList] = await Promise.all([api.organizations(), api.roles()])
    organizations.value = organizationList
    roles.value = roleList
    model.value.OrganizationId = session.organizationId

    if (props.id) {
      const [people, memberships] = await Promise.all([api.users(scope), api.userRoles()])
      const found = people.find((u: ApplicationUser) => u.Id === props.id)

      if (!found) {
        notifyError(null, t('user.gone'))
        await router.replace({ name: listRoute })
        return
      }

      model.value = {
        UserName: found.UserName,
        FullName: found.FullName ?? '',
        Email: found.Email ?? '',
        Password: '',
        ConfirmPassword: '',
        OrganizationId: found.OrganizationId
      }

      const mine = new Set(memberships.filter(m => m.UserId === props.id).map(m => m.RoleId))
      assigned.value = mine
      originalAssigned.value = new Set(mine)
    }
  } catch (error) {
    notifyError(error, t('user.loadFailed'))
  } finally {
    loading.value = false
  }
})

function toggleRole(roleId: string) {
  const next = new Set(assigned.value)
  if (next.has(roleId)) next.delete(roleId)
  else next.add(roleId)
  assigned.value = next
}

async function save() {
  submitted.value = true
  if (problems.value.length > 0) return

  saving.value = true
  try {
    const body: Record<string, unknown> = {
      UserName: model.value.UserName,
      FullName: model.value.FullName,
      Email: model.value.Email,
      OrganizationId: model.value.OrganizationId
    }

    // On edit an empty password means "leave it alone"; the server skips the reset in that case.
    if (model.value.Password) {
      body.Password = model.value.Password
      body.ConfirmPassword = model.value.ConfirmPassword
    }

    const saved = props.id
      ? await api.updateUser(props.id, body)
      : await api.createUser(body)

    const userId = props.id ?? saved.Id

    await Promise.all([
      ...[...assigned.value]
        .filter(r => !originalAssigned.value.has(r))
        .map(r => api.addUserToRole(userId, r)),
      ...[...originalAssigned.value]
        .filter(r => !assigned.value.has(r))
        .map(r => api.removeUserFromRole(userId, r))
    ])

    notifySaved(t('user.singular'))
    await router.push({ name: listRoute })
  } catch (error) {
    notifyError(error, t('user.saveFailed'))
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <h1>{{ isNew ? t('user.new') : t('user.singular') }}</h1>

  <p v-if="loading" class="empty"><span class="spinner" :aria-label="t('common.loading')" /></p>

  <form v-else @submit.prevent="save">
    <div class="panel panel__head">
      <button class="btn btn--primary" type="submit" :disabled="saving">
        {{ saving ? t('action.saving') : t('action.save') }}
      </button>
      <button class="btn btn--ghost" type="button" @click="router.push({ name: listRoute })">
        {{ t('action.back') }}
      </button>
    </div>

    <div v-if="submitted && problems.length > 0" class="alert alert--danger">
      <p v-for="problem in problems" :key="problem" style="margin: 0">{{ problem }}</p>
    </div>

    <div class="panel">
      <div class="panel__body grid-2">
        <div class="field">
          <label class="field__label" for="userName">{{ t('login.userName') }} <span class="field__required">*</span></label>
          <input id="userName" v-model="model.UserName" class="field__control" autocomplete="off" />
        </div>

        <div class="field">
          <label class="field__label" for="fullName">{{ t('user.fullName') }} <span class="field__required">*</span></label>
          <input id="fullName" v-model="model.FullName" class="field__control" />
        </div>

        <div class="field">
          <label class="field__label" for="email">{{ t('user.email') }} <span class="field__required">*</span></label>
          <input id="email" v-model="model.Email" type="email" class="field__control" />
        </div>

        <div class="field">
          <label class="field__label" for="organization">
            {{ t('user.organization') }} <span class="field__required">*</span>
          </label>
          <select id="organization" v-model.number="model.OrganizationId" class="field__control">
            <option :value="0">—</option>
            <option v-for="item in organizations" :key="item.Id" :value="item.Id">
              {{ item.Name }}
            </option>
          </select>
        </div>

        <div class="field">
          <label class="field__label" for="password">
            {{ t('login.password') }} <span v-if="isNew" class="field__required">*</span>
          </label>
          <input
            id="password"
            v-model="model.Password"
            type="password"
            class="field__control"
            autocomplete="new-password"
          />
          <p v-if="!isNew" class="muted" style="font-size: 12px; margin: 4px 0 0">
            {{ t('user.keepPassword') }}
          </p>
        </div>

        <div class="field">
          <label class="field__label" for="confirm">{{ t('user.confirmPassword') }}</label>
          <input
            id="confirm"
            v-model="model.ConfirmPassword"
            type="password"
            class="field__control"
            autocomplete="new-password"
          />
        </div>
      </div>
    </div>

    <div class="panel">
      <div class="panel__head"><h2 style="margin: 0">{{ t('user.roles') }}</h2></div>
      <div class="panel__body">
        <label v-for="role in roles" :key="role.Id" class="row-gap" style="margin-bottom: 8px">
          <input type="checkbox" :checked="assigned.has(role.Id)" @change="toggleRole(role.Id)" />
          {{ role.Name }}
        </label>
        <p v-if="roles.length === 0" class="muted">{{ t('user.noRoles') }}</p>
      </div>
    </div>
  </form>
</template>
