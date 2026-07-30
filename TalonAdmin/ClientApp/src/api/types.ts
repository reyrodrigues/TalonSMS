/** Entity shapes as the server serialises them: PascalCase, matching the Breeze metadata. */

export interface Entity {
  Id: number
}

export interface Organization extends Entity {
  Name: string
  Abbreviation: string | null
  LogoSVG: string | null
}

export interface Country extends Entity {
  Name: string
  IsoAlpha2: string
  IsoAlpha3: string | null
  CurrencyIsoCode: string | null
  CurrencyUnicodeSymbol: string | null
  CountryCallingCode: string | null
  LanguageCode: string | null
  SettingsId: number
}

export interface ApplicationUserCountry extends Entity {
  ApplicationUserId: string
  CountryId: number
  Country: Country | null
}

export interface CurrentUser {
  Id: string
  UserName: string
  FullName: string | null
  Email: string | null
  OrganizationId: number
  Organization: Organization | null
  Countries: ApplicationUserCountry[]
  IsSystemAdministrator: boolean
  AvailableActions: Array<{ Id: number; Name: string }>
}

export interface MenuLink extends Entity {
  Title: string
  State: string | null
  CssClass: string | null
  ParentId: number | null
  CategoryId: number | null
  Children: MenuLink[] | null
}

export interface MenuCategory extends Entity {
  Name: string
  SortOrder: number
  NeedsCountry: boolean | null
  Items: MenuLink[] | null
  Roles: Array<{ RoleId: string }> | null
}

export interface Location extends Entity {
  Name: string
  CountryId: number
}

export interface VendorType extends Entity {
  Name: string
  CountryId: number
}

export interface VoucherType extends Entity {
  Name: string
  CountryId: number
  OrganizationId: number
}

export interface BeneficiaryGroup extends Entity {
  Name: string
  CountryId: number
  OrganizationId: number
}

export interface Beneficiary extends Entity {
  FirstName: string | null
  LastName: string | null
  Name: string | null
  NationalId: string | null
  IRCId: string | null
  MobileNumber: string | null
  BirthYear: string | null
  Sex: number
  Disabled: boolean | null
  PIN: string | null
  GroupId: number | null
  LocationId: number | null
  Group: BeneficiaryGroup | null
  Location: Location | null
  CountryId: number
  OrganizationId: number
}

export interface Vendor extends Entity {
  Name: string | null
  FirstName: string | null
  LastName: string | null
  MobileNumber: string | null
  NationalId: string | null
  UserName: string | null
  LocationId: number | null
  TypeId: number | null
  ParentRecordId: number | null
  GPSCoordinates: string | null
  Location: Location | null
  Type: VendorType | null
  CountryId: number
}

export interface Program extends Entity {
  Name: string
  FundCodes: string | null
  DistributionMechanism: number
  VoucherCodeLength: number
  ShowInDashboard: boolean | null
  CountryId: number
  OrganizationId: number
}

export interface Distribution extends Entity {
  Number: number | null
  Title: string | null
  FundCodes: string | null
  ProgramId: number | null
  GroupId: number | null
  LocationId: number | null
  IsClosed: boolean
  CreatedOn: string
  Program: Program | null
  Group: BeneficiaryGroup | null
  Location: Location | null
  CountryId: number
  OrganizationId: number
}

export interface DashboardDistribution {
  Distribution: Distribution | null
  Location: Location | null
  TotalVouchers: number
  VouchersUsed: number
  Beneficiaries: number
  UniqueBeneficiaries: number
  TotalVendors: number
  IssuedAmount: number | null
  ClaimedAmount: number | null
}

export interface DashboardProgram {
  Program: { Id: number; Name: string; FundCodes: string | null }
  TotalVouchers: number
  VouchersUsed: number
  Beneficiaries: number
  UniqueBeneficiaries: number
  TotalVendors: number
  IssuedAmount: number | null
  ClaimedAmount: number | null
  Distributions: DashboardDistribution[]
  /** [date, issued, 0, used] tuples driving the programme chart. */
  UsedVsIssued: Array<[string, number, number, number]>
  /** [vendorName, transactionCount] tuples. */
  Vendor: Array<[string, number]>
}
