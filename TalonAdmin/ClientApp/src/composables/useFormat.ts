import { useSessionStore } from '@/stores/session'

/** Formatting helpers that need the active country's currency. */
export function useFormat() {
  const session = useSessionStore()

  const money = (value: number | null | undefined) =>
    `${session.currency}${(value ?? 0).toLocaleString(undefined, {
      minimumFractionDigits: 2,
      maximumFractionDigits: 2
    })}`

  const number = (value: number | null | undefined) => (value ?? 0).toLocaleString()

  const date = (value: string | null | undefined) =>
    value ? new Date(value).toLocaleDateString() : ''

  const dateTime = (value: string | null | undefined) =>
    value ? new Date(value).toLocaleString() : ''

  return { money, number, date, dateTime }
}
