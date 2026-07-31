import { onScopeDispose } from 'vue'
import { HubConnectionBuilder, LogLevel, type HubConnection } from '@microsoft/signalr'
import { notify, type ToastKind } from './useToast'

/**
 * The server pushes two things over `/signalr/dashboard`: `message` toasts raised by long-running
 * work, and `updateDashboard` when a distribution or redemption changes the numbers.
 *
 * The connection is shared — several components may listen, but only one socket is opened — and it
 * closes once nothing is listening any more.
 */
let connection: HubConnection | null = null
let listeners = 0

function ensureConnection(): HubConnection {
  if (connection) return connection

  connection = new HubConnectionBuilder()
    .withUrl(`${window.location.origin}/signalr/dashboard`)
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Warning)
    .build()

  connection.on('message', (kind: string, _title: string, message: string) => {
    notify(toToastKind(kind), message)
  })

  // A failed connection is not worth interrupting the user over: the screens all load their own
  // data, so losing live updates degrades to "refresh to see changes".
  connection.start().catch(() => {})

  return connection
}

const toToastKind = (kind: string): ToastKind =>
  kind === 'success' || kind === 'error' ? kind : 'info'

/** Runs `onUpdate` whenever the server says the dashboard figures have moved. */
export function useDashboardHub(onUpdate: () => void) {
  const hub = ensureConnection()
  hub.on('updateDashboard', onUpdate)
  listeners += 1

  onScopeDispose(() => {
    hub.off('updateDashboard', onUpdate)
    listeners -= 1

    if (listeners === 0) {
      void hub.stop()
      connection = null
    }
  })
}
