import { t } from '@/i18n'
import * as api from '@/api/talon'
import * as breeze from '@/api/breeze'
import type { ResourceDef, SelectOption } from './resource'
import type {
  BeneficiaryGroup,
  CardLoad,
  OutboundMessage,
  Country,
  Location,
  MenuCategory,
  MenuLink,
  Organization,
  ReferenceName,
  TransactionLogItem,
  VendorType,
  VoucherType
} from '@/api/types'

/** Option loaders shared by several forms. */
const optionsFrom = <T extends { Id: number }>(
  load: () => Promise<T[]>,
  label: (row: T) => string
) => async (): Promise<SelectOption[]> =>
  (await load()).map(row => ({ value: row.Id, label: label(row) }))

const locationOptions = optionsFrom(api.locations, l => l.Name)
const vendorTypeOptions = optionsFrom(api.vendorTypes, t => t.Name)
const voucherTypeOptions = optionsFrom(api.voucherTypes, t => t.Name)
const menuCategoryOptions = optionsFrom(api.menuCategories, c => c.Name)
const organizationOptions = optionsFrom(api.organizations, o => o.Name)

const NAME_ONLY = [{ key: 'Name', label: 'common.name', required: true, full: true }]

const nameColumns = [
  { key: 'Id', label: 'common.id', width: '80px' },
  { key: 'Name', label: 'common.name' }
]

/** Country-scoped reference lists: a name and nothing else. */
export const locations: ResourceDef<Location> = {
  name: 'locations',
  title: 'resource.locations',
  singular: 'resource.location',
  context: 'EVM',
  typeName: 'Location',
  resource: 'Locations',
  columns: nameColumns,
  fields: NAME_ONLY,
  searchFields: ['Name'],
  tenant: 'country',
  permission: 'Locations',
  describe: row => row.Name
}

export const vendorTypes: ResourceDef<VendorType> = {
  name: 'vendor-types',
  title: 'resource.vendorTypes',
  singular: 'resource.vendorType',
  context: 'EVM',
  typeName: 'VendorType',
  resource: 'VendorTypes',
  columns: nameColumns,
  fields: NAME_ONLY,
  searchFields: ['Name'],
  tenant: 'country',
  permission: 'VendorTypes',
  describe: row => row.Name
}

export const voucherTypes: ResourceDef<VoucherType> = {
  name: 'voucher-types',
  title: 'resource.voucherTypes',
  singular: 'resource.voucherType',
  context: 'EVM',
  typeName: 'VoucherType',
  resource: 'VoucherTypes',
  columns: nameColumns,
  fields: NAME_ONLY,
  searchFields: ['Name'],
  tenant: 'countryAndOrganization',
  permission: 'VoucherTypes',
  describe: row => row.Name
}

export const cycles: ResourceDef<BeneficiaryGroup> = {
  name: 'cycles',
  title: 'resource.cycles',
  singular: 'resource.cycle',
  context: 'EVM',
  typeName: 'BeneficiaryGroup',
  resource: 'BeneficiaryGroups',
  columns: nameColumns,
  fields: NAME_ONLY,
  searchFields: ['Name'],
  tenant: 'countryAndOrganization',
  permission: 'BeneficiaryGroups',
  describe: row => row.Name
}

// ---- system administration -------------------------------------------------------

export const organizations: ResourceDef<Organization> = {
  name: 'organizations',
  title: 'resource.organizations',
  singular: 'resource.organization',
  context: 'Admin',
  typeName: 'Organization',
  resource: 'Organizations',
  columns: [
    { key: 'Id', label: 'common.id', width: '80px' },
    { key: 'Name', label: 'common.name' },
    { key: 'Abbreviation', label: 'resource.abbreviation' }
  ],
  fields: [
    { key: 'Name', label: 'common.name', required: true },
    { key: 'Abbreviation', label: 'resource.abbreviation' },
    { key: 'LogoSVG', label: 'resource.logo', type: 'textarea', full: true }
  ],
  searchFields: ['Name', 'Abbreviation'],
  permission: 'Organizations',
  describe: row => row.Name
}

export const countries: ResourceDef<Country> = {
  name: 'countries',
  title: 'country.title',
  singular: 'country.singular',
  context: 'Admin',
  typeName: 'Country',
  resource: 'Countries',
  columns: [
    { key: 'Id', label: 'common.id', width: '80px' },
    { key: 'Name', label: 'common.name' },
    { key: 'IsoAlpha2', label: 'country.isoAlpha2' },
    { key: 'CurrencyIsoCode', label: 'country.currencyIso' }
  ],
  fields: [
    { key: 'Name', label: 'country.name', required: true },
    { key: 'IsoAlpha2', label: 'country.isoAlpha2', required: true, help: 'country.isoAlpha2Hint' },
    { key: 'IsoAlpha3', label: 'country.isoAlpha3' },
    { key: 'CurrencyUnicodeSymbol', label: 'country.currencySymbol', required: true },
    { key: 'CurrencyIsoCode', label: 'country.currencyIso' },
    { key: 'CountryCallingCode', label: 'country.callingCode', required: true },
    { key: 'LanguageCode', label: 'country.languageCode', required: true }
  ],
  searchFields: ['Name', 'IsoAlpha2'],
  permission: 'Countries',
  // Creating a country writes three rows, so its form is CountryEditView rather than the generic
  // one; the descriptor still drives the list and the delete button.
  customEditView: true,
  describe: row => row.Name
}

export const actions: ResourceDef<ReferenceName> = {
  name: 'actions',
  title: 'resource.actions',
  singular: 'resource.action',
  context: 'Admin',
  typeName: 'Action',
  resource: 'Actions',
  columns: nameColumns,
  fields: NAME_ONLY,
  searchFields: ['Name'],
  permission: 'Actions',
  emptyMessage: 'resource.actionsEmpty',
  describe: row => row.Name
}

export const menuCategories: ResourceDef<MenuCategory> = {
  name: 'menu-categories',
  title: 'resource.menuCategories',
  singular: 'resource.menuCategory',
  context: 'Admin',
  typeName: 'MenuCategory',
  resource: 'MenuCategories',
  columns: [
    { key: 'Id', label: 'common.id', width: '80px' },
    { key: 'Name', label: 'common.name' },
    { key: 'SortOrder', label: 'resource.sortOrder', numeric: true }
  ],
  fields: [
    { key: 'Name', label: 'common.name', required: true },
    { key: 'SortOrder', label: 'resource.sortOrder', type: 'number' },
    { key: 'NeedsCountry', label: 'resource.needsCountry', type: 'checkbox', full: true }
  ],
  defaultSort: 'SortOrder',
  permission: 'MenuCategories',
  describe: row => row.Name
}

export const menuItems: ResourceDef<MenuLink> = {
  name: 'menu-items',
  title: 'resource.menuItems',
  singular: 'resource.menuItem',
  context: 'Admin',
  typeName: 'MenuItem',
  resource: 'MenuItems',
  columns: [
    { key: 'Id', label: 'common.id', width: '80px' },
    { key: 'Title', label: 'resource.itemTitle' },
    { key: 'State', label: 'resource.state' },
    { key: 'CssClass', label: 'resource.icon' }
  ],
  fields: [
    { key: 'Title', label: 'resource.itemTitle', required: true },
    { key: 'State', label: 'resource.state', help: 'resource.stateHint' },
    { key: 'CssClass', label: 'resource.iconClass' },
    { key: 'CategoryId', label: 'resource.category', type: 'select', options: menuCategoryOptions },
    { key: 'ParentId', label: 'resource.parentItem', type: 'select', options: optionsFrom(api.menuItems, i => i.Title) }
  ],
  searchFields: ['Title', 'State'],
  permission: 'MenuItems',
  describe: row => row.Title
}

// ---- read-only transaction views ---------------------------------------------------

export const transactionLog: ResourceDef<TransactionLogItem> = {
  name: 'transaction-log',
  title: 'resource.transactionLog',
  singular: 'resource.transaction',
  context: 'EVM',
  typeName: 'TransactionLogItem',
  resource: 'TransactionLogItems',
  columns: [
    { key: 'Id', label: 'common.id', width: '80px' },
    { key: 'Date', label: 'common.date' },
    { key: 'Beneficiary.Name', label: 'beneficiary.singular', sortBy: null },
    { key: 'Vendor.Name', label: 'vendor.singular', sortBy: null },
    { key: 'AmountCredited', label: 'resource.credited', numeric: true },
    { key: 'AmountRemaining', label: 'resource.remaining', numeric: true },
    { key: 'TransactionCode', label: 'resource.code' }
  ],
  fields: [],
  expand: ['Beneficiary', 'Vendor'],
  defaultSort: 'Date',
  readOnly: true,
  emptyMessage: 'resource.transactionsEmpty'
}

export const quarantine: ResourceDef<TransactionLogItem> = {
  ...transactionLog,
  name: 'quarantine',
  title: 'resource.quarantine',
  filter: { Quarantine: true },
  emptyMessage: 'resource.quarantineEmpty'
}

/** Mirrors OutboundMessageState on the server. Resolved at render time, so it follows the locale. */
const messageState = (state: number) => t(`messageState.${state}`)

const localTime = (value: string | null | undefined) =>
  value ? new Date(value).toLocaleString() : ''

/**
 * The delivery log. Messages used to be sent from an unawaited task with every failure discarded,
 * so a beneficiary who never got their voucher code left no trace; this is where that now shows up.
 */
export const outboundMessages: ResourceDef<OutboundMessage> = {
  name: 'message-log',
  title: 'resource.messageLog',
  singular: 'resource.message',
  context: 'EVM',
  typeName: 'OutboundMessage',
  resource: 'OutboundMessages',
  columns: [
    { key: 'Id', label: 'common.id', width: '70px' },
    { key: 'CreatedOn', label: 'resource.queued', value: row => localTime(row.CreatedOn) },
    { key: 'To', label: 'resource.recipient' },
    { key: 'RecipientName', label: 'common.name' },
    { key: 'State', label: 'common.status', value: row => messageState(row.State) },
    { key: 'Attempts', label: 'resource.attempts', numeric: true },
    { key: 'LastError', label: 'resource.lastError' }
  ],
  fields: [],
  defaultSort: 'Id',
  readOnly: true,
  searchFields: ['To', 'RecipientName', 'Body', 'LastError'],
  emptyMessage: 'resource.messageLogEmpty'
}

export const cardLoads: ResourceDef<CardLoad> = {
  name: 'card-loads',
  title: 'resource.cardLoads',
  singular: 'resource.cardLoad',
  context: 'EVM',
  typeName: 'CardLoad',
  resource: 'CardLoads',
  columns: [
    { key: 'Id', label: 'common.id', width: '80px' },
    { key: 'Date', label: 'common.date' },
    { key: 'Beneficiary.Name', label: 'beneficiary.singular', sortBy: null },
    { key: 'Vendor.Name', label: 'vendor.singular', sortBy: null },
    { key: 'Amount', label: 'common.amount', numeric: true }
  ],
  fields: [],
  expand: ['Beneficiary', 'Vendor'],
  defaultSort: 'Date',
  readOnly: true,
  emptyMessage: 'resource.cardLoadsEmpty'
}

/** Every descriptor-driven screen, in the order the router registers them. */
export const RESOURCES: ResourceDef<never>[] = [
  locations,
  vendorTypes,
  voucherTypes,
  cycles,
  organizations,
  countries,
  actions,
  menuCategories,
  menuItems,
  transactionLog,
  quarantine,
  cardLoads,
  outboundMessages
] as unknown as ResourceDef<never>[]

export { locationOptions, vendorTypeOptions, voucherTypeOptions, organizationOptions, breeze }
