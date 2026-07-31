import { getJson, postForm, postJson, postBlob, postMultipart, sendJson } from './http'
import * as breeze from './breeze'
import type {
  ApplicationRole,
  ApplicationUser,
  Beneficiary,
  BeneficiaryGroup,
  Country,
  CountrySettings,
  CurrentUser,
  DashboardProgram,
  Distribution,
  ExportedReport,
  Location,
  MenuCategory,
  MenuLink,
  Organization,
  OrganizationCountry,
  Program,
  ProgramVoucherCategory,
  Vendor,
  VendorType,
  VoucherTransactionRecord,
  VoucherType
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
export const voucherTypes = () => breeze.query<VoucherType>('EVM', 'VoucherTypes', { orderBy: ['Name'] })
export const organizations = () => breeze.query<Organization>('Admin', 'Organizations', { orderBy: ['Name'] })
export const menuCategories = () => breeze.query<MenuCategory>('Admin', 'MenuCategories', { orderBy: ['Name'] })
export const menuItems = () => breeze.query<MenuLink>('Admin', 'MenuItems', { orderBy: ['Title'] })
export const roles = () => breeze.query<ApplicationRole>('Admin', 'Roles', { orderBy: ['Name'] })

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

export const cancelVoucher = (voucherId: number) =>
  postJson<void>('/api/VoucherWorkflow/CancelVoucher', { VoucherId: voucherId })

export const resendVoucherSms = (beneficiaryId: number, voucherId: number) =>
  postJson<void>('/api/VoucherWorkflow/ResendSMS', {
    BeneficiaryId: beneficiaryId,
    VoucherId: voucherId
  })

/** Voucher status: 1 issued, 2 used, 3 partially used, 4 cancelled. */
export const distributionTransactions = (distributionId: number, used: boolean) =>
  breeze.query<VoucherTransactionRecord>('EVM', 'VoucherTransactionRecords', {
    where: {
      and: [
        { 'Voucher.DistributionId': distributionId },
        used
          ? { Type: 2 }
          : { and: [{ Type: 1 }, { 'Voucher.Status': 1 }] }
      ]
    },
    expand: ['Beneficiary', 'Voucher'],
    orderBy: ['Id']
  })

// ---- telegram -------------------------------------------------------------------------

/**
 * The enrolment link for one recipient, or null when the country does not deliver over Telegram —
 * the endpoint answers 404 in that case, and the UI simply hides the control.
 */
export async function telegramEnrolmentLink(mobileNumber: string) {
  try {
    return await getJson<{ Link: string; Enrolled: boolean }>(
      `/api/Telegram/EnrolmentLink?mobileNumber=${encodeURIComponent(mobileNumber)}`
    )
  } catch {
    return null
  }
}

// ---- reports --------------------------------------------------------------------------

export interface ReportRequest {
  DistributionId?: number
  ProgramId?: number
  VendorId?: number
  CountryId: number
  OrganizationId: number
  PaperSize?: string
  TimezoneOffset?: number
  /** ISO dates. Only the payment schedule uses these, and it requires both. */
  PeriodStart?: string
  PeriodEnd?: string
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

// ---- programmes -----------------------------------------------------------------------

/**
 * Voucher categories have no Breeze resource of their own — they are reached through the
 * programme's `Categories` navigation, which is how the AngularJS client loaded them too.
 */
export const program = (id: number) =>
  breeze.first<Program>('EVM', 'Programs', { where: { Id: id }, expand: ['Categories'] })

export function saveProgram(entity: Partial<Program>, original?: Program) {
  const record =
    original && entity.Id
      ? breeze.modified('EVM', 'Program', 'Programs', entity as Record<string, unknown>, {})
      : breeze.added('EVM', 'Program', 'Programs', entity as Record<string, unknown>)

  return breeze.saveChanges<Program>('EVM', [record])
}

export const saveProgramCategories = (
  added: Array<Partial<ProgramVoucherCategory>>,
  updated: Array<Partial<ProgramVoucherCategory>>,
  removed: ProgramVoucherCategory[]
) =>
  breeze.saveChanges('EVM', [
    ...added.map(c =>
      breeze.added('EVM', 'ProgramVoucherCategory', 'ProgramVoucherCategories', c as Record<string, unknown>)
    ),
    ...updated.map(c =>
      breeze.modified('EVM', 'ProgramVoucherCategory', 'ProgramVoucherCategories', c as Record<string, unknown>, {})
    ),
    ...removed.map(c =>
      breeze.deleted('EVM', 'ProgramVoucherCategory', 'ProgramVoucherCategories', c as unknown as Record<string, unknown>)
    )
  ])

export const distribution = (id: number) =>
  breeze.first<Distribution>('EVM', 'Distributions', {
    where: { Id: id },
    expand: ['Program', 'Group', 'Location']
  })

export const distributionsForProgram = (programId: number) =>
  breeze.query<Distribution>('EVM', 'Distributions', {
    where: { ProgramId: programId },
    expand: ['Group', 'Location'],
    orderBy: ['Id']
  })

// ---- administration -------------------------------------------------------------------

/**
 * A country is four rows, not one:
 *
 *  - `CountrySettings`, which the Country's foreign key points at — creating only the Country
 *    fails outright on that constraint;
 *  - the `Country` itself;
 *  - an `OrganizationCountry` granting the organization access to it;
 *  - an `ApplicationUserCountry` granting the *creating user* access, without which the new
 *    country never appears in their country switcher and cannot be worked in.
 *
 * Breeze resolves the temporary settings key inside the first bundle; the two link rows need the
 * real keys, so they go in a second.
 */
export async function createCountry(
  values: Partial<Country>,
  organizationId: number,
  userId: string
) {
  const settingsTempId = breeze.tempId()
  const countryTempId = breeze.tempId()

  const first = await breeze.saveChanges('Admin', [
    breeze.added(
      'Admin',
      'CountrySettings',
      'CountrySettings',
      { SmsBackendType: 1 },
      settingsTempId
    ),
    breeze.added(
      'Admin',
      'Country',
      'Countries',
      { ...values, SettingsId: settingsTempId },
      countryTempId
    )
  ])

  const countryId = first.realId(countryTempId)
  const settingsId = first.realId(settingsTempId)

  await breeze.saveChanges('Admin', [
    breeze.added('Admin', 'OrganizationCountry', 'OrganizationCountries', {
      OrganizationId: organizationId,
      CountryId: countryId,
      SettingsId: settingsId
    }),
    breeze.added('Admin', 'ApplicationUserCountry', 'ApplicationUserCountries', {
      ApplicationUserId: userId,
      CountryId: countryId
    })
  ])

  return countryId
}

export const country = (id: number) =>
  breeze.first<Country>('Admin', 'Countries', { where: { Id: id } })

export const updateCountry = (values: Partial<Country>, original: Country) =>
  breeze.saveChanges<Country>('Admin', [
    breeze.modified('Admin', 'Country', 'Countries', values as Record<string, unknown>, {
      ...Object.fromEntries(
        Object.keys(values)
          .filter(k => (original as unknown as Record<string, unknown>)[k] !== (values as Record<string, unknown>)[k])
          .map(k => [k, (original as unknown as Record<string, unknown>)[k] ?? null])
      )
    })
  ])

export const organizationCountries = () =>
  breeze.query<OrganizationCountry>('Admin', 'OrganizationCountries', {
    expand: ['Country', 'Settings', 'Settings.PropertyCollection']
  })

/**
 * The SMS gateway credentials are columns on CountrySettings; the message templates are rows in
 * its property collection, keyed by (SettingsId, Name). Both go up in one bundle.
 */
export function saveCountrySettings(
  settings: CountrySettings,
  changes: Record<string, unknown>,
  templates: Array<{ Name: string; Value: string; existed: boolean }>
) {
  const bundle = [
    breeze.modified(
      'Admin',
      'CountrySettings',
      'CountrySettings',
      { ...(settings as unknown as Record<string, unknown>), ...changes },
      Object.fromEntries(
        Object.keys(changes).map(k => [k, (settings as unknown as Record<string, unknown>)[k] ?? null])
      )
    ),
    ...templates.map(t => {
      const row = { SettingsId: settings.Id, Name: t.Name, Value: t.Value }

      return t.existed
        ? breeze.modified(
            'Admin',
            'CountrySettingsProperty',
            'CountrySettings',
            row,
            {},
            breeze.clientKey('SettingsId')
          )
        : breeze.addedWithKey('Admin', 'CountrySettingsProperty', 'CountrySettings', row, 'SettingsId')
    })
  ]

  return breeze.saveChanges('Admin', bundle)
}

/** Users and roles have string keys and their own REST endpoints rather than Breeze saves. */
export const users = (scope: 'Users' | 'OrganizationUsers' | 'Administrators' = 'Users') =>
  breeze.query<ApplicationUser>('Admin', scope, { expand: ['Organization'] })

export const createUser = (body: Record<string, unknown>) =>
  postJson<ApplicationUser>('/api/ApplicationUser', body)

export const updateUser = (id: string, body: Record<string, unknown>) =>
  sendJson<ApplicationUser>('PUT', `/api/ApplicationUser/${id}`, body)

export const deleteUser = (id: string) => sendJson<void>('DELETE', `/api/ApplicationUser/${id}`)

export const addUserToRole = (id: string, roleId: string) =>
  postJson<void>(`/api/ApplicationUser/${id}/AddUserToRole`, { roleId })

export const removeUserFromRole = (id: string, roleId: string) =>
  postJson<void>(`/api/ApplicationUser/${id}/RemoveUserFromRole`, { roleId })

export const createRole = (name: string) => postJson<ApplicationRole>('/api/ApplicationRole', { Name: name })

export const updateRole = (id: string, name: string) =>
  sendJson<ApplicationRole>('PUT', `/api/ApplicationRole/${id}`, { Name: name })

export const deleteRole = (id: string) => sendJson<void>('DELETE', `/api/ApplicationRole/${id}`)

export const userRoles = () =>
  breeze.query<{ UserId: string; RoleId: string }>('Admin', 'UserRoles', {})

// ---- reporting -------------------------------------------------------------------------

export const vendorFinancialReport = (request: ReportRequest) =>
  postBlob('/api/Reports/VendorProgramFinancialReport', request)

/** Unlike the other reports this one answers with a spreadsheet, and both period bounds are required. */
export const paymentScheduleReport = (request: ReportRequest & { PeriodStart: string; PeriodEnd: string }) =>
  postBlob('/api/Reports/PaymentScheduleReport', request)

/** The report kinds the server stamps onto an ExportedReport row. */
export const REPORT_TYPES = [
  { Id: 1, Name: 'Program Closure Report' },
  { Id: 2, Name: 'Vendor Financial Report' },
  { Id: 3, Name: 'Distribution Report' },
  { Id: 4, Name: 'Payment Schedule' }
]

export const exportedReports = (programId: number, reportType: number) =>
  breeze.query<ExportedReport>('EVM', 'ExportedReports', {
    where: { and: [{ ProgramId: programId }, { ReportType: reportType }] },
    expand: ['Program'],
    orderBy: ['Id desc']
  })

/** Attaches a counter-signed PDF, stored on the row as base64 like the report itself. */
export const attachSignedReport = (report: ExportedReport, base64: string) =>
  breeze.saveChanges('EVM', [
    breeze.modified(
      'EVM',
      'ExportedReport',
      'ExportedReports',
      { ...(report as unknown as Record<string, unknown>), SignedReport: base64 },
      { SignedReport: report.SignedReport ?? null }
    )
  ])

// ---- vendor reconciliation ----------------------------------------------------------------

/**
 * Transactions awaiting reconciliation for one vendor on one programme. Sub-vendors roll up to
 * their parent, and only debits (2) and cancellations (3) are reconciled.
 */
export const reconcilableTransactions = (vendorId: number, programId: number) =>
  breeze.query<VoucherTransactionRecord>('EVM', 'VoucherTransactionRecords', {
    where: {
      and: [
        { or: [{ VendorId: vendorId }, { 'Vendor.ParentRecordId': vendorId }] },
        { 'Voucher.Distribution.ProgramId': programId },
        { IsFinalized: null },
        { or: [{ Type: 2 }, { Type: 3 }] }
      ]
    },
    expand: ['Voucher', 'Beneficiary', 'Vendor'],
    orderBy: ['CreatedOn']
  })

export const reconcile = (records: VoucherTransactionRecord[], by: string, at: string) =>
  breeze.saveChanges(
    'EVM',
    records.map(record =>
      breeze.modified(
        'EVM',
        'VoucherTransactionRecord',
        'VoucherTransactionRecords',
        { ...(record as unknown as Record<string, unknown>), ReconciledOn: at, ReconciledBy: by },
        { ReconciledOn: record.ReconciledOn ?? null, ReconciledBy: record.ReconciledBy ?? null }
      )
    )
  )

// ---- country settings ---------------------------------------------------------------------

export const vendors = () => breeze.query<Vendor>('EVM', 'Vendors', { orderBy: ['Name'] })

export const distributions = () =>
  breeze.query<Distribution>('EVM', 'Distributions', { expand: ['Program'], orderBy: ['Id'] })

// Beneficiary.Name is a computed getter with no column behind it, so sorting uses its parts.
export const distributionBeneficiaries = (distributionId: number) =>
  breeze.query<Beneficiary>('EVM', 'DistributionBeneficiaries', {
    expand: ['Location'],
    orderBy: ['LastName', 'FirstName'],
    parameters: { distributionId }
  })

export const distributionVendors = (distributionId: number) =>
  breeze.query<Vendor>('EVM', 'DistributionVendors', {
    expand: ['Location', 'Type'],
    orderBy: ['Name'],
    parameters: { distributionId }
  })

// ---- spreadsheets -----------------------------------------------------------------------

const tenantQuery = (countryId: number, organizationId: number) =>
  `organizationId=${organizationId}&countryId=${countryId}`

export const exportBeneficiaries = (countryId: number, organizationId: number) =>
  postBlob(`/api/Excel/ExportBeneficiaries?${tenantQuery(countryId, organizationId)}`, {})

export const exportVendors = (countryId: number, organizationId: number) =>
  postBlob(`/api/Excel/ExportVendors?${tenantQuery(countryId, organizationId)}`, {})

export const importBeneficiaries = (file: File, countryId: number, organizationId: number) =>
  postMultipart<{ Errors: unknown[] }>(
    `/api/Excel/ImportBeneficiaries?${tenantQuery(countryId, organizationId)}`,
    file
  )

export const importVendors = (file: File, countryId: number, organizationId: number) =>
  postMultipart<{ Errors: unknown[] }>(
    `/api/Excel/ImportVendors?${tenantQuery(countryId, organizationId)}`,
    file
  )
