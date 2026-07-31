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
        <!--
          Several routes share one component (every descriptor-driven list, every report). Keying on
          the path forces a remount instead of leaving the previous screen's state behind.
        -->
        <RouterView v-slot="{ Component, route }">
          <component :is="Component" :key="route.path" />
        </RouterView>
      </main>
    </div>
  </div>
</template>
