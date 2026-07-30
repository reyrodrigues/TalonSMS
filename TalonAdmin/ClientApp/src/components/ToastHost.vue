<script setup lang="ts">
import { toasts, dismiss } from '@/composables/useToast'
</script>

<template>
  <div class="toasts" role="status" aria-live="polite">
    <div v-for="toast in toasts" :key="toast.id" class="toast" :class="`toast--${toast.kind}`">
      <span>{{ toast.message }}</span>
      <button class="toast__close" aria-label="Dismiss" @click="dismiss(toast.id)">×</button>
    </div>
  </div>
</template>

<style scoped>
.toasts {
  position: fixed;
  top: 16px;
  right: 16px;
  display: flex;
  flex-direction: column;
  gap: 8px;
  z-index: 100;
}

.toast {
  display: flex;
  align-items: center;
  gap: 12px;
  min-width: 260px;
  max-width: 420px;
  padding: 11px 14px;
  border-radius: 3px;
  color: #fff;
  box-shadow: 0 6px 18px rgba(0, 0, 0, 0.18);
}

.toast--success { background: #27c24c; }
.toast--error { background: #f05050; }
.toast--info { background: #23b7e5; }

.toast__close {
  margin-left: auto;
  background: none;
  border: 0;
  color: inherit;
  font-size: 18px;
  line-height: 1;
  cursor: pointer;
  opacity: 0.8;
}

.toast__close:hover { opacity: 1; }
</style>
