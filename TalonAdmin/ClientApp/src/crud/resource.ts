import type { Column } from '@/components/DataTable.vue'
import type { BreezeContext, BreezeQuery } from '@/api/breeze'

/**
 * Most screens in this application are a paged list over one entity plus a form of plain fields.
 * Describing them here rather than writing thirty near-identical components keeps the differences
 * visible: columns, fields, and which tenant keys the server expects on save.
 */

export type FieldType = 'text' | 'number' | 'decimal' | 'checkbox' | 'select' | 'phone' | 'textarea'

export interface SelectOption {
  value: unknown
  label: string
}

export interface FieldDef {
  key: string
  label: string
  type?: FieldType
  required?: boolean
  /** Loaded once when the form opens; used by `select` fields. */
  options?: () => Promise<SelectOption[]>
  /** Fixed options for small enumerations. */
  choices?: SelectOption[]
  help?: string
  readOnly?: boolean
  full?: boolean
}

/** Which tenant keys the server expects the client to stamp on a new row. */
export type TenantScope = 'none' | 'country' | 'countryAndOrganization'

export interface ResourceDef<T extends object = Record<string, unknown>> {
  /** Route name stem: `<name>`, `<name>-create`, `<name>-edit`. */
  name: string
  title: string
  singular: string
  context: BreezeContext
  /** CLR type name, as Breeze expects it in the save bundle. */
  typeName: string
  /** Breeze resource (usually the plural). */
  resource: string
  columns: Column<T>[]
  fields: FieldDef[]
  searchFields?: string[]
  expand?: string[]
  defaultSort?: string
  tenant?: TenantScope
  /**
   * Suffix used for the permission names the UI gates on, e.g. `Locations` gives
   * "Create Locations" / "Edit Locations" / "Delete Locations". Omit to always allow.
   */
  permission?: string
  /** Extra query applied to every list request. */
  filter?: BreezeQuery['where']
  /** Shown when the list is empty. */
  emptyMessage?: string
  /** Read-only screens hide the New button and render the form disabled. */
  readOnly?: boolean
  describe?: (row: T) => string
}

export const labelFor = (row: Record<string, unknown>, keys: string[]) =>
  keys
    .map(k => row[k])
    .filter(v => v !== null && v !== undefined && v !== '')
    .join(' ')
