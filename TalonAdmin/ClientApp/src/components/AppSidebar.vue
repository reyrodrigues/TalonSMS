<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { RouterLink } from 'vue-router'
import { useSessionStore } from '@/stores/session'
import { labelKeyForName, labelKeyForState, routeForState } from '@/router/stateMap'
import type { MenuCategory, MenuLink } from '@/api/types'

const { t } = useI18n()

defineProps<{ open?: boolean }>()
defineEmits<{ (e: 'close'): void }>()

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

/**
 * The catalogue's wording for a seeded menu row, or the server's own text when it is not one of
 * them — a deployment that renamed a menu item keeps its name.
 */
const label = (name: string | null | undefined, state?: string | null) => {
  const key = labelKeyForState(state) ?? labelKeyForName(name)
  return key ? t(key) : (name ?? '')
}

const visibleCategories = computed(() =>
  categories.value.filter(category => groupsWithLinks(category).length > 0)
)
</script>

<template>
  <nav class="sidebar" :class="{ 'sidebar--open': open }" aria-label="Main">
    <RouterLink :to="{ name: 'dashboard' }" class="sidebar__brand">{{ t('app.name') }}</RouterLink>

    <button class="sidebar__close" :aria-label="t('app.closeMenu')" @click="$emit('close')">×</button>

    <RouterLink :to="{ name: 'dashboard' }" class="sidebar__link sidebar__link--flush">
      {{ t('dashboard.title') }}
    </RouterLink>

    <template v-for="category in visibleCategories" :key="category.Id">
      <div class="sidebar__section">{{ label(category.Name) }}</div>

      <div v-for="group in groupsWithLinks(category)" :key="group.Id" class="sidebar__group">
        <button
          class="sidebar__group-toggle"
          :aria-expanded="expanded.has(group.Id)"
          @click="toggle(group)"
        >
          <span>{{ label(group.Title) }}</span>
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
            {{ label(child.Title, child.State) }}
          </RouterLink>
        </div>
      </div>
    </template>
  </nav>
</template>
