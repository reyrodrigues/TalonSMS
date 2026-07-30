using System;
using System.Collections.Generic;
using TalonAdmin.Models.Admin;
using TalonAdmin.Models.Vouchers;

namespace TalonAdmin.Reporting
{
    /// <summary>
    /// Backs /Views/Reports/ProgramClosureReport.cshtml.
    /// </summary>
    public class ProgramClosureReportModel : ReportModelBase
    {
        public ApplicationUser User { get; set; }
        public Models.Vouchers.Program Program { get; set; }
        public Organization Organization { get; set; }
        public Country Country { get; set; }
        public List<ProgramClosureReportPage> Pages { get; set; }
        public ProgramClosureReportRow[] Items { get; set; }
        public int PageSize { get; set; }
        public Vendor Vendor { get; set; }
        public decimal? Total { get; set; }
    }

    public class ProgramClosureReportPage
    {
        public int Page { get; set; }
        public bool IsLastPage { get; set; }
        public ProgramClosureReportRow[] Items { get; set; }
        public decimal? SubTotal { get; set; }
    }

    /// <summary>
    /// Property order matters: the rows are serialized straight into the exported spreadsheet
    /// (after flattening "Vendor"), and the anonymous type this replaces produced the columns
    /// in this order.
    /// </summary>
    public class ProgramClosureReportRow
    {
        public Vendor Vendor { get; set; }
        public int NumberOfVouchers { get; set; }
        public decimal? TotalValue { get; set; }
    }
}
