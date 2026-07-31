<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { RouterView, useRoute } from 'vue-router'
import AppSidebar from '@/components/AppSidebar.vue'
import AppHeader from '@/components/AppHeader.vue'

/**
 * Below the drawer breakpoint the sidebar is hidden until asked for, so the menu state lives here
 * rather than in the sidebar itself — the header's button opens it and the scrim closes it.
 */
const menuOpen = ref(false)
const route = useRoute()

// Following a link is the end of what the menu was opened for.
watch(() => route.fullPath, () => (menuOpen.value = false))

function onKeydown(event: KeyboardEvent) {
  if (event.key === 'Escape') menuOpen.value = false
}

onMounted(() => document.addEventListener('keydown', onKeydown))
onBeforeUnmount(() => document.removeEventListener('keydown', onKeydown))
</script>

<template>
  <div class="shell">
    <AppSidebar :open="menuOpen" @close="menuOpen = false" />

    <!-- Only rendered above the sidebar on narrow viewports; the stylesheet hides it otherwise. -->
    <div v-if="menuOpen" class="shell__scrim" @click="menuOpen = false" />

    <div class="shell__main">
      <AppHeader :menu-open="menuOpen" @toggle-menu="menuOpen = !menuOpen" />
      <main class="shell__body">
        <!--
          Several routes share one component (every descriptor-driven list, every report). Keying on
          the path forces a remount instead of leaving the previous screen's state behind.
        -->
        <RouterView v-slot="{ Component, route: current }">
          <component :is="Component" :key="current.path" />
        </RouterView>
      </main>
    </div>
  </div>
</template>
