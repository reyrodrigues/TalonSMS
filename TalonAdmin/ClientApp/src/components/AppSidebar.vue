<script setup lang="ts">
import { computed, ref } from 'vue'
import { RouterLink } from 'vue-router'
import { useSessionStore } from '@/stores/session'
import { routeForState } from '@/router/stateMap'
import type { MenuCategory, MenuLink } from '@/api/types'

/**
 * The menu is data held on the server (MenuCategory -> MenuItem -> child link), and each child
 * carries the name of an AngularJS ui-router state. `routeForState` maps those to Vue routes so
 * the same rows drive both clients while the port is in progress.
 */
const session = useSessionStore()

const categories = computed<MenuCategory[]>(() =>
  session.menu
    .filter(category => !category.NeedsCountry || session.country)
    .slice()
    .sort((a, b) => a.SortOrder - b.SortOrder)
)

const expanded = ref<Set<number>>(new Set())

const toggle = (group: MenuLink) => {
  if (expanded.value.has(group.Id)) expanded.value.delete(group.Id)
  else expanded.value.add(group.Id)
  expanded.value = new Set(expanded.value)
}

/** Only links that already have a Vue screen are shown; the rest would be dead ends. */
const availableChildren = (group: MenuLink) =>
  (group.Children ?? []).filter(child => child.State && routeForState(child.State))

const groupsWithLinks = (category: MenuCategory) =>
  (category.Items ?? []).filter(group => availableChildren(group).length > 0)

const visibleCategories = computed(() =>
  categories.value.filter(category => groupsWithLinks(category).length > 0)
)
</script>

<template>
  <nav class="sidebar" aria-label="Main">
    <RouterLink :to="{ name: 'dashboard' }" class="sidebar__brand">Talon</RouterLink>

    <RouterLink :to="{ name: 'dashboard' }" class="sidebar__link" style="padding-left: 16px">
      Dashboard
    </RouterLink>

    <template v-for="category in visibleCategories" :key="category.Id">
      <div class="sidebar__section">{{ category.Name }}</div>

      <div v-for="group in groupsWithLinks(category)" :key="group.Id" class="sidebar__group">
        <button
          class="sidebar__group-toggle"
          :aria-expanded="expanded.has(group.Id)"
          @click="toggle(group)"
        >
          <span>{{ group.Title }}</span>
          <span class="sidebar__chevron" :class="{ 'sidebar__chevron--open': expanded.has(group.Id) }">
            ›
          </span>
        </button>

        <div v-show="expanded.has(group.Id)">
          <RouterLink
            v-for="child in availableChildren(group)"
            :key="child.Id"
            :to="routeForState(child.State!)!"
            class="sidebar__link"
          >
            {{ child.Title }}
          </RouterLink>
        </div>
      </div>
    </template>
  </nav>
</template>
