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
  /** Populated when the programme is loaded with `expand: ['Categories']`. */
  Categories?: ProgramVoucherCategory[] | null
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

export interface ReferenceName extends Entity {
  Name: string
}

export interface TransactionLogItem extends Entity {
  BeneficiaryId: number
  VendorId: number
  Type: number
  AmountCredited: number
  AmountRemaining: number
  Date: string
  TransactionCode: string | null
  ConfirmationCode: string | null
  VoucherCode: string | null
  Quarantine: boolean
  Processed: boolean
  ProcessedOn: string | null
  ProcessedBy: string | null
  Beneficiary: Beneficiary | null
  Vendor: Vendor | null
  CountryId: number
  OrganizationId: number
}

export interface CardLoad extends Entity {
  BeneficiaryId: number
  VendorId: number
  Amount: number
  Date: string
  DistributionDate: string
  Beneficiary: Beneficiary | null
  Vendor: Vendor | null
  CountryId: number
  OrganizationId: number
}

export interface ProgramVoucherCategory extends Entity {
  ProgramId: number
  TypeId: number
  VendorTypeId: number | null
  NumberOfVouchers: number
  IssuedVouchers: number
  Value: number | null
  ValidAfterOffset: number | null
  ValidAfterOffsetType: number | null
  Type: VoucherType | null
  VendorType: VendorType | null
  CountryId: number
  OrganizationId: number
}

export interface ApplicationRole {
  Id: string
  Name: string
}

export interface ApplicationUser {
  Id: string
  UserName: string
  FullName: string | null
  Email: string | null
  OrganizationId: number
  Organization: Organization | null
}

/** 0 pending, 1 sent, 2 failed — see OutboundMessageState on the server. */
export interface OutboundMessage extends Entity {
  To: string | null
  RecipientName: string | null
  Body: string | null
  ChannelKey: string | null
  State: number
  Attempts: number
  CreatedOn: string
  NextAttemptOn: string
  SentOn: string | null
  LastError: string | null
  ProviderMessageId: string | null
  CountryId: number
  OrganizationId: number
}

export interface CountrySettingsProperty {
  SettingsId: number
  Name: string
  Value: string | null
}

export interface CountrySettings extends Entity {
  SmsBackendType: number
  ServiceUrl: string | null
  ServiceUser: string | null
  ServicePassword: string | null
  /** The SMS message templates, one row per name. */
  PropertyCollection: CountrySettingsProperty[] | null
}

export interface OrganizationCountry extends Entity {
  OrganizationId: number
  CountryId: number
  SettingsId: number | null
  Country: Country | null
  Settings: CountrySettings | null
}

export interface ExportedReport extends Entity {
  ProgramId: number
  ReportType: number
  Description: string | null
  ReportRunOn: string | null
  ReportRunBy: string | null
  Program: Program | null
  /** PDF and spreadsheet payloads, stored on the row as base64. */
  OriginalReport: string | null
  Spreadsheet: string | null
  SignedReport: string | null
}

export interface Voucher extends Entity {
  VoucherCode: string | null
  SequentialCode: string | null
  CategoryId: number | null
  DistributionId: number | null
  BeneficiaryId: number | null
  /** 1 issued, 2 used, 3 partially used, 4 cancelled. */
  Status: number
}

export interface VoucherTransactionRecord extends Entity {
  BeneficiaryId: number
  VoucherId: number | null
  VendorId: number | null
  CreatedOn: string
  ConfirmationCode: string | null
  /** 1 credit, 2 debit, 3 cancellation. */
  Type: number
  Value: number | null
  ReconciledBy: string | null
  ReconciledOn: string | null
  IsFinalized: boolean | null
  Beneficiary: Beneficiary | null
  Vendor: Vendor | null
  Voucher: Voucher | null
  CountryId: number
  OrganizationId: number
}
