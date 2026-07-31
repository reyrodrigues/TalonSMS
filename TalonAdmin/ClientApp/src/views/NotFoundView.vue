<script setup lang="ts">
import { RouterLink } from 'vue-router'
import { useI18n } from 'vue-i18n'

const { t } = useI18n()
</script>

<template>
  <div class="panel" style="margin: 60px auto; max-width: 460px">
    <div class="panel__body" style="text-align: center">
      <h1>{{ t('notFound.title') }}</h1>
      <p class="muted">{{ t('notFound.body') }}</p>
      <RouterLink class="btn btn--primary" :to="{ name: 'dashboard' }">{{ t('notFound.back') }}</RouterLink>
    </div>
  </div>
</template>
