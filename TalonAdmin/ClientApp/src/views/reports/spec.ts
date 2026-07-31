import * as api from '@/api/talon'
import type { ReportRequest } from '@/api/talon'

/**
 * The three PDF reports differ only in which selectors they need and which endpoint they post to,
 * so one screen renders all of them. Each route carries its spec in `meta.report`.
 */
export interface ReportSpec {
  /** Catalogue keys, resolved when the screen renders so they follow the locale. */
  title: string
  description: string
  /** Which pickers to show; the first one listed is the required one. */
  selectors: Array<'program' | 'distribution' | 'vendor'>
  fileName: string
  run: (request: ReportRequest) => Promise<Blob>
}

export const distributionReport: ReportSpec = {
  title: 'report.distribution.title',
  description: 'report.distribution.description',
  selectors: ['distribution'],
  fileName: 'distribution-report',
  run: api.distributionReport
}

export const programClosureReport: ReportSpec = {
  title: 'report.programClosure.title',
  description: 'report.programClosure.description',
  selectors: ['program'],
  fileName: 'program-closure-report',
  run: api.programClosureReport
}

export const vendorFinancialReport: ReportSpec = {
  title: 'report.vendorFinancial.title',
  description: 'report.vendorFinancial.description',
  selectors: ['vendor', 'program'],
  fileName: 'vendor-financial-report',
  run: api.vendorFinancialReport
}
