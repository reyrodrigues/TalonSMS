import * as api from '@/api/talon'
import type { ReportRequest } from '@/api/talon'

/**
 * The three PDF reports differ only in which selectors they need and which endpoint they post to,
 * so one screen renders all of them. Each route carries its spec in `meta.report`.
 */
export interface ReportSpec {
  title: string
  description: string
  /** Which pickers to show; the first one listed is the required one. */
  selectors: Array<'program' | 'distribution' | 'vendor'>
  fileName: string
  run: (request: ReportRequest) => Promise<Blob>
}

export const distributionReport: ReportSpec = {
  title: 'Distribution Report',
  description: 'Every voucher issued in one distribution, with its redemption status.',
  selectors: ['distribution'],
  fileName: 'distribution-report',
  run: api.distributionReport
}

export const programClosureReport: ReportSpec = {
  title: 'Program Closure Report',
  description: 'The full financial picture for a programme, for sign-off at closure.',
  selectors: ['program'],
  fileName: 'program-closure-report',
  run: api.programClosureReport
}

export const vendorFinancialReport: ReportSpec = {
  title: 'Vendor Financial Report',
  description: 'What one vendor is owed for the vouchers they redeemed on a programme.',
  selectors: ['vendor', 'program'],
  fileName: 'vendor-financial-report',
  run: api.vendorFinancialReport
}
