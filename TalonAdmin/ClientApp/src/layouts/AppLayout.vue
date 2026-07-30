<script setup lang="ts">
import { RouterView } from 'vue-router'
import AppSidebar from '@/components/AppSidebar.vue'
import AppHeader from '@/components/AppHeader.vue'
</script>

<template>
  <div class="shell">
    <AppSidebar />
    <div class="shell__main">
      <AppHeader />
      <main class="shell__body">
        <RouterView />
      </main>
    </div>
  </div>
</template>
