<script setup lang="ts">
import { computed, ref } from 'vue'
import * as api from '@/api/talon'
import { useSessionStore } from '@/stores/session'
import { notifyError, notifySaved } from '@/composables/useToast'

/** The signed-in user's own details. Organization and roles are not editable from here. */
const session = useSessionStore()

const model = ref({
  UserName: session.user?.UserName ?? '',
  FullName: session.user?.FullName ?? '',
  Email: session.user?.Email ?? '',
  Password: '',
  ConfirmPassword: ''
})

const saving = ref(false)
const submitted = ref(false)

const problems = computed(() => {
  const found: string[] = []
  if (!model.value.UserName.trim()) found.push('A user name is required.')
  if (!model.value.Email.trim()) found.push('An email address is required.')
  if (!model.value.FullName.trim()) found.push('A full name is required.')
  if (model.value.Password && model.value.Password.length < 6) {
    found.push('The new password must be at least 6 characters.')
  }
  if (model.value.Password !== model.value.ConfirmPassword) {
    found.push('The passwords do not match.')
  }
  return found
})

async function save() {
  submitted.value = true
  if (problems.value.length > 0 || !session.user) return

  saving.value = true
  try {
    const body: Record<string, unknown> = {
      UserName: model.value.UserName,
      FullName: model.value.FullName,
      Email: model.value.Email,
      OrganizationId: session.user.OrganizationId
    }

    if (model.value.Password) {
      body.Password = model.value.Password
      body.ConfirmPassword = model.value.ConfirmPassword
    }

    await api.updateUser(session.user.Id, body)

    model.value.Password = ''
    model.value.ConfirmPassword = ''
    submitted.value = false

    // The header shows the name, so the cached user has to catch up.
    await session.loadSession()
    notifySaved('Profile')
  } catch (error) {
    notifyError(error, 'Could not save your profile.')
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <h1>My Profile</h1>

  <form @submit.prevent="save">
    <div class="panel panel__head">
      <button class="btn btn--primary" type="submit" :disabled="saving">
        {{ saving ? 'Saving…' : 'Save' }}
      </button>
    </div>

    <div v-if="submitted && problems.length > 0" class="alert alert--danger">
      <p v-for="problem in problems" :key="problem" style="margin: 0">{{ problem }}</p>
    </div>

    <div class="panel">
      <div class="panel__body grid-2">
        <div class="field">
          <label class="field__label" for="userName">User Name <span class="field__required">*</span></label>
          <input id="userName" v-model="model.UserName" class="field__control" autocomplete="username" />
        </div>

        <div class="field">
          <label class="field__label" for="fullName">Full Name <span class="field__required">*</span></label>
          <input id="fullName" v-model="model.FullName" class="field__control" />
        </div>

        <div class="field">
          <label class="field__label" for="email">Email <span class="field__required">*</span></label>
          <input id="email" v-model="model.Email" type="email" class="field__control" />
        </div>

        <div class="field">
          <label class="field__label">Organization</label>
          <input class="field__control" :value="session.user?.Organization?.Name ?? ''" disabled />
        </div>

        <div class="field">
          <label class="field__label" for="password">New Password</label>
          <input
            id="password"
            v-model="model.Password"
            type="password"
            class="field__control"
            autocomplete="new-password"
          />
          <p class="muted" style="font-size: 12px; margin: 4px 0 0">
            Leave blank to keep your current password.
          </p>
        </div>

        <div class="field">
          <label class="field__label" for="confirm">Confirm New Password</label>
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
  </form>
</template>
