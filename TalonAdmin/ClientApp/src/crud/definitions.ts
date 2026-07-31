import * as api from '@/api/talon'
import * as breeze from '@/api/breeze'
import type { ResourceDef, SelectOption } from './resource'
import type {
  BeneficiaryGroup,
  CardLoad,
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

const NAME_ONLY = [{ key: 'Name', label: 'Name', required: true, full: true }]

const nameColumns = [
  { key: 'Id', label: '#', width: '80px' },
  { key: 'Name', label: 'Name' }
]

/** Country-scoped reference lists: a name and nothing else. */
export const locations: ResourceDef<Location> = {
  name: 'locations',
  title: 'Locations',
  singular: 'Location',
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
  title: 'Vendor Types',
  singular: 'Vendor Type',
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
  title: 'Voucher Types',
  singular: 'Voucher Type',
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
  title: 'Cycles',
  singular: 'Cycle',
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
  title: 'Organizations',
  singular: 'Organization',
  context: 'Admin',
  typeName: 'Organization',
  resource: 'Organizations',
  columns: [
    { key: 'Id', label: '#', width: '80px' },
    { key: 'Name', label: 'Name' },
    { key: 'Abbreviation', label: 'Abbreviation' }
  ],
  fields: [
    { key: 'Name', label: 'Name', required: true },
    { key: 'Abbreviation', label: 'Abbreviation' },
    { key: 'LogoSVG', label: 'Logo (SVG)', type: 'textarea', full: true }
  ],
  searchFields: ['Name', 'Abbreviation'],
  permission: 'Organizations',
  describe: row => row.Name
}

export const countries: ResourceDef<Country> = {
  name: 'countries',
  title: 'Countries',
  singular: 'Country',
  context: 'Admin',
  typeName: 'Country',
  resource: 'Countries',
  columns: [
    { key: 'Id', label: '#', width: '80px' },
    { key: 'Name', label: 'Name' },
    { key: 'IsoAlpha2', label: 'ISO' },
    { key: 'CurrencyIsoCode', label: 'Currency' }
  ],
  fields: [
    { key: 'Name', label: 'Country Name', required: true },
    { key: 'IsoAlpha2', label: 'ISO Alpha-2 Code', required: true, help: 'Routes the inbound SMS webhook.' },
    { key: 'IsoAlpha3', label: 'ISO Alpha-3 Code' },
    { key: 'CurrencyUnicodeSymbol', label: 'Currency Symbol', required: true },
    { key: 'CurrencyIsoCode', label: 'Currency ISO Code' },
    { key: 'CountryCallingCode', label: 'Calling Code', required: true },
    { key: 'LanguageCode', label: 'Language Code', required: true }
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
  title: 'Actions',
  singular: 'Action',
  context: 'Admin',
  typeName: 'Action',
  resource: 'Actions',
  columns: nameColumns,
  fields: NAME_ONLY,
  searchFields: ['Name'],
  permission: 'Actions',
  emptyMessage: 'No actions are defined. Actions gate the create, edit and delete buttons.',
  describe: row => row.Name
}

export const menuCategories: ResourceDef<MenuCategory> = {
  name: 'menu-categories',
  title: 'Menu Categories',
  singular: 'Menu Category',
  context: 'Admin',
  typeName: 'MenuCategory',
  resource: 'MenuCategories',
  columns: [
    { key: 'Id', label: '#', width: '80px' },
    { key: 'Name', label: 'Name' },
    { key: 'SortOrder', label: 'Sort Order', numeric: true }
  ],
  fields: [
    { key: 'Name', label: 'Name', required: true },
    { key: 'SortOrder', label: 'Sort Order', type: 'number' },
    { key: 'NeedsCountry', label: 'Only show when a country is selected', type: 'checkbox', full: true }
  ],
  defaultSort: 'SortOrder',
  permission: 'MenuCategories',
  describe: row => row.Name
}

export const menuItems: ResourceDef<MenuLink> = {
  name: 'menu-items',
  title: 'Menu Items',
  singular: 'Menu Item',
  context: 'Admin',
  typeName: 'MenuItem',
  resource: 'MenuItems',
  columns: [
    { key: 'Id', label: '#', width: '80px' },
    { key: 'Title', label: 'Title' },
    { key: 'State', label: 'State' },
    { key: 'CssClass', label: 'Icon' }
  ],
  fields: [
    { key: 'Title', label: 'Title', required: true },
    { key: 'State', label: 'State', help: 'Router state this link navigates to.' },
    { key: 'CssClass', label: 'Icon class' },
    { key: 'CategoryId', label: 'Category', type: 'select', options: menuCategoryOptions },
    { key: 'ParentId', label: 'Parent item', type: 'select', options: optionsFrom(api.menuItems, i => i.Title) }
  ],
  searchFields: ['Title', 'State'],
  permission: 'MenuItems',
  describe: row => row.Title
}

// ---- read-only transaction views ---------------------------------------------------

export const transactionLog: ResourceDef<TransactionLogItem> = {
  name: 'transaction-log',
  title: 'Transaction Log',
  singular: 'Transaction',
  context: 'EVM',
  typeName: 'TransactionLogItem',
  resource: 'TransactionLogItems',
  columns: [
    { key: 'Id', label: '#', width: '80px' },
    { key: 'Date', label: 'Date' },
    { key: 'Beneficiary.Name', label: 'Beneficiary', sortBy: null },
    { key: 'Vendor.Name', label: 'Vendor', sortBy: null },
    { key: 'AmountCredited', label: 'Credited', numeric: true },
    { key: 'AmountRemaining', label: 'Remaining', numeric: true },
    { key: 'TransactionCode', label: 'Code' }
  ],
  fields: [],
  expand: ['Beneficiary', 'Vendor'],
  defaultSort: 'Date',
  readOnly: true,
  emptyMessage: 'No point-of-sale transactions have been uploaded for this country.'
}

export const quarantine: ResourceDef<TransactionLogItem> = {
  ...transactionLog,
  name: 'quarantine',
  title: 'Transaction Quarantine',
  filter: { Quarantine: true },
  emptyMessage: 'Nothing is in quarantine.'
}

export const cardLoads: ResourceDef<CardLoad> = {
  name: 'card-loads',
  title: 'Card Load Log',
  singular: 'Card Load',
  context: 'EVM',
  typeName: 'CardLoad',
  resource: 'CardLoads',
  columns: [
    { key: 'Id', label: '#', width: '80px' },
    { key: 'Date', label: 'Date' },
    { key: 'Beneficiary.Name', label: 'Beneficiary', sortBy: null },
    { key: 'Vendor.Name', label: 'Vendor', sortBy: null },
    { key: 'Amount', label: 'Amount', numeric: true }
  ],
  fields: [],
  expand: ['Beneficiary', 'Vendor'],
  defaultSort: 'Date',
  readOnly: true,
  emptyMessage: 'No card loads have been recorded.'
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
  cardLoads
] as unknown as ResourceDef<never>[]

export { locationOptions, vendorTypeOptions, voucherTypeOptions, organizationOptions, breeze }
