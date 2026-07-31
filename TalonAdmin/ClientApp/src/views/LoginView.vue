<script setup lang="ts">
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRoute, useRouter } from 'vue-router'
import { useSessionStore } from '@/stores/session'
import { ApiError } from '@/api/http'

const { t } = useI18n()

const session = useSessionStore()
const router = useRouter()
const route = useRoute()

const userName = ref('')
const password = ref('')
const error = ref('')
const busy = ref(false)

async function submit() {
  error.value = t('login.failed')
  busy.value = true

  try {
    await session.logIn(userName.value, password.value)
    const redirect = route.query.redirect
    await router.push(typeof redirect === 'string' ? redirect : { name: 'dashboard' })
  } catch (e) {
    // /Token answers 400 with an OAuth error payload when the credentials are wrong.
    error.value =
      e instanceof ApiError && e.status === 400
        ? 'The user name or password is incorrect.'
        : 'Could not sign in. Please try again.'
  } finally {
    busy.value = false
  }
}
</script>

<template>
  <div class="login">
    <form class="login__card" @submit.prevent="submit">
      <h1 class="login__brand">{{ t('app.name') }}</h1>

      <p v-if="error" class="alert alert--danger">{{ error }}</p>

      <div class="field">
        <label class="field__label" for="userName">{{ t('login.userName') }}</label>
        <input
          id="userName"
          v-model="userName"
          class="field__control"
          autocomplete="username"
          required
          autofocus
        />
      </div>

      <div class="field">
        <label class="field__label" for="password">{{ t('login.password') }}</label>
        <input
          id="password"
          v-model="password"
          type="password"
          class="field__control"
          autocomplete="current-password"
          required
        />
      </div>

      <button class="btn btn--primary login__submit" type="submit" :disabled="busy">
        {{ busy ? t('login.signingIn') : t('login.submit') }}
      </button>
    </form>
  </div>
</template>

<style scoped>
.login {
  min-height: 100%;
  display: flex;
  align-items: flex-start;
  justify-content: center;
  padding-top: 8vh;
}

.login__card { width: 330px; }

.login__brand {
  text-align: center;
  margin-bottom: 22px;
  font-weight: 600;
  font-size: 24px;
}

.login__submit { width: 100%; justify-content: center; margin-top: 6px; }
</style>
