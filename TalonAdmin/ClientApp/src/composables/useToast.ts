import { ref } from 'vue'

/** Replaces angularjs-toaster. Module-level state so any module can raise a notification. */

export type ToastKind = 'success' | 'error' | 'info'

export interface Toast {
  id: number
  kind: ToastKind
  message: string
}

export const toasts = ref<Toast[]>([])

let nextId = 1

export function notify(kind: ToastKind, message: string, timeoutMs = 4000) {
  const id = nextId++
  toasts.value.push({ id, kind, message })

  if (timeoutMs > 0) {
    window.setTimeout(() => dismiss(id), timeoutMs)
  }
}

export const dismiss = (id: number) => {
  toasts.value = toasts.value.filter(t => t.id !== id)
}

export const notifySaved = (what: string) => notify('success', `${what} saved.`)

export function notifyError(error: unknown, fallback = 'Something went wrong.') {
  const message = error instanceof Error ? error.message : String(error ?? fallback)
  notify('error', message.slice(0, 300) || fallback, 8000)
}
