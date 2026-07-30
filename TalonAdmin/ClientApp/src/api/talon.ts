import { getJson, postForm, postJson, postBlob } from './http'
import * as breeze from './breeze'
import type {
  Beneficiary,
  BeneficiaryGroup,
  Country,
  CurrentUser,
  DashboardProgram,
  Distribution,
  Location,
  MenuCategory,
  Program,
  Vendor,
  VendorType
} from './types'

/** The endpoints the client actually uses, typed. */

export interface TokenResponse {
  access_token: string
  token_type: string
  expires_in: number
  userName: string
}

export async function requestToken(userName: string, password: string): Promise<TokenResponse> {
  const response = await postForm('/Token', {
    grant_type: 'password',
    username: userName,
    password
  })

  return (await response.json()) as TokenResponse
}

export const currentUser = () => getJson<CurrentUser>('/api/ApplicationUser/Me')

export const navigation = () => getJson<MenuCategory[]>('/Home/NavigationItems')

export const dashboard = () => getJson<DashboardProgram[]>('/api/Dashboard/DashboardSummary')

// ---- reference data ---------------------------------------------------------------

export const locations = () => breeze.query<Location>('EVM', 'Locations', { orderBy: ['Name'] })
export const vendorTypes = () => breeze.query<VendorType>('EVM', 'VendorTypes', { orderBy: ['Name'] })
export const beneficiaryGroups = () =>
  breeze.query<BeneficiaryGroup>('EVM', 'BeneficiaryGroups', { orderBy: ['Name'] })
export const programs = () => breeze.query<Program>('EVM', 'Programs', { orderBy: ['Name'] })
export const countries = () => breeze.query<Country>('Admin', 'Countries', { orderBy: ['Name'] })

// ---- beneficiaries ----------------------------------------------------------------

const BENEFICIARY_SEARCH = ['FirstName', 'LastName', 'MobileNumber', 'NationalId']

export function beneficiaryPage(options: {
  skip: number
  take: number
  orderBy: string[]
  search: string
}) {
  return breeze.queryPage<Beneficiary>('EVM', 'Beneficiaries', {
    skip: options.skip,
    take: options.take,
    orderBy: options.orderBy,
    expand: ['Group', 'Location'],
    where: breeze.containsAny(options.search, BENEFICIARY_SEARCH)
  })
}

export const beneficiary = (id: number) =>
  breeze.first<Beneficiary>('EVM', 'Beneficiaries', {
    where: { Id: id },
    expand: ['Group', 'Location']
  })

export function saveBeneficiary(entity: Partial<Beneficiary>, original?: Beneficiary) {
  const record =
    original && entity.Id
      ? breeze.modified('EVM', 'Beneficiary', 'Beneficiaries', entity as Record<string, unknown>, {
          ...changedFields(entity, original)
        })
      : breeze.added('EVM', 'Beneficiary', 'Beneficiaries', entity as Record<string, unknown>)

  return breeze.saveChanges<Beneficiary>('EVM', [record])
}

export const deleteBeneficiary = (entity: Beneficiary) =>
  breeze.saveChanges('EVM', [
    breeze.deleted('EVM', 'Beneficiary', 'Beneficiaries', entity as unknown as Record<string, unknown>)
  ])

// ---- vendors ------------------------------------------------------------------------

const VENDOR_SEARCH = ['Name', 'FirstName', 'LastName', 'MobileNumber', 'NationalId']

export function vendorPage(options: {
  skip: number
  take: number
  orderBy: string[]
  search: string
}) {
  return breeze.queryPage<Vendor>('EVM', 'Vendors', {
    skip: options.skip,
    take: options.take,
    orderBy: options.orderBy,
    expand: ['Location', 'Type'],
    where: breeze.containsAny(options.search, VENDOR_SEARCH)
  })
}

export const vendor = (id: number) =>
  breeze.first<Vendor>('EVM', 'Vendors', { where: { Id: id }, expand: ['Location', 'Type'] })

export function saveVendor(entity: Partial<Vendor>, original?: Vendor) {
  const record =
    original && entity.Id
      ? breeze.modified('EVM', 'Vendor', 'Vendors', entity as Record<string, unknown>, {
          ...changedFields(entity, original)
        })
      : breeze.added('EVM', 'Vendor', 'Vendors', entity as Record<string, unknown>)

  return breeze.saveChanges<Vendor>('EVM', [record])
}

export const deleteVendor = (entity: Vendor) =>
  breeze.saveChanges('EVM', [
    breeze.deleted('EVM', 'Vendor', 'Vendors', entity as unknown as Record<string, unknown>)
  ])

// ---- distributions -------------------------------------------------------------------

export function distributionPage(options: { skip: number; take: number; orderBy: string[] }) {
  return breeze.queryPage<Distribution>('EVM', 'Distributions', {
    ...options,
    expand: ['Program', 'Group', 'Location']
  })
}

export const distributeVouchers = (body: {
  ProgramId: number
  GroupId: number
  LocationId: number
}) => postJson<void>('/api/VoucherWorkflow/DistributeVouchers', body)

// ---- reports --------------------------------------------------------------------------

export interface ReportRequest {
  DistributionId?: number
  ProgramId?: number
  VendorId?: number
  CountryId: number
  OrganizationId: number
  PaperSize?: string
  TimezoneOffset?: number
}

export const distributionReport = (request: ReportRequest) =>
  postBlob('/api/Reports/DistributionReport', request)

export const programClosureReport = (request: ReportRequest) =>
  postBlob('/api/Reports/ProgramClosureReport', request)

/**
 * Breeze wants the values a Modified entity started with. Only the fields that actually changed
 * are reported, which is what the server uses to build its update.
 */
function changedFields<T extends object>(next: Partial<T>, original: T): Record<string, unknown> {
  const changed: Record<string, unknown> = {}

  for (const key of Object.keys(next) as Array<keyof T & string>) {
    const before = original[key]
    if (before !== null && typeof before === 'object') continue
    if (next[key] !== before) changed[key] = before
  }

  return changed
}
