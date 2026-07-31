import * as breeze from '@/api/breeze'
import type { ResourceDef, TenantScope } from './resource'
import { useSessionStore } from '@/stores/session'

/** Query and save helpers derived from a resource descriptor. */
export function useResource<T extends object>(def: ResourceDef<T>) {
  const session = useSessionStore()

  const tenantKeys = (scope: TenantScope = def.tenant ?? 'none'): Record<string, unknown> => {
    if (scope === 'none') return {}
    const country = { CountryId: session.country?.Id ?? 0 }
    return scope === 'country' ? country : { ...country, OrganizationId: session.organizationId }
  }

  async function page(options: { skip: number; take: number; orderBy: string[]; search: string }) {
    const search = def.searchFields ? breeze.containsAny(options.search, def.searchFields) : undefined

    const where =
      search && def.filter ? { and: [def.filter, search] } : (search ?? def.filter ?? undefined)

    return breeze.queryPage<T>(def.context, def.resource, {
      skip: options.skip,
      take: options.take,
      orderBy: options.orderBy,
      expand: def.expand,
      where
    })
  }

  const load = (id: number) =>
    breeze.first<T>(def.context, def.resource, { where: { Id: id }, expand: def.expand })

  function save(values: Record<string, unknown>, original?: T) {
    const payload = { ...values, ...tenantKeys() }

    const entity =
      original && values.Id
        ? breeze.modified(def.context, def.typeName, def.resource, payload, changed(values, original))
        : breeze.added(def.context, def.typeName, def.resource, payload)

    return breeze.saveChanges<T>(def.context, [entity])
  }

  const remove = (row: T) =>
    breeze.saveChanges(def.context, [
      breeze.deleted(def.context, def.typeName, def.resource, row as unknown as Record<string, unknown>)
    ])

  const can = (verb: 'Create' | 'Edit' | 'Delete') =>
    !def.readOnly && (!def.permission || session.can(`${verb} ${def.permission}`))

  return { page, load, save, remove, can, tenantKeys }
}

/** The values a Modified row started with, for the fields that actually changed. */
function changed(next: Record<string, unknown>, original: object): Record<string, unknown> {
  const before = original as Record<string, unknown>
  const result: Record<string, unknown> = {}

  for (const key of Object.keys(next)) {
    const value = before[key]
    if (value !== null && typeof value === 'object') continue
    if (next[key] !== value) result[key] = value
  }

  return result
}
