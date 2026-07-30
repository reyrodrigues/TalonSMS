<script setup lang="ts">
import { RouterLink } from 'vue-router'
</script>

<template>
  <div class="panel" style="margin: 60px auto; max-width: 460px">
    <div class="panel__body" style="text-align: center">
      <h1>Page not found</h1>
      <p class="muted">That screen has not been ported to the new client yet.</p>
      <RouterLink class="btn btn--primary" :to="{ name: 'dashboard' }">Back to the dashboard</RouterLink>
    </div>
  </div>
</template>
