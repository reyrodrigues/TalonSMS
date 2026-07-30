using System;
using System.Collections.Generic;
using TalonAdmin.Models.Admin;
using TalonAdmin.Models.Vouchers;

namespace TalonAdmin.Reporting
{
    /// <summary>
    /// Backs /Views/Reports/DistributionReport.cshtml.
    /// </summary>
    public class DistributionReportModel : ReportModelBase
    {
        public ApplicationUser User { get; set; }
        public Distribution Distribution { get; set; }
        public string DistributionNumber { get; set; }
        public Organization Organization { get; set; }
        public Country Country { get; set; }
        public List<DistributionReportPage> Pages { get; set; }
        public DistributionReportRow[] Items { get; set; }
        public int PageSize { get; set; }
        public decimal? Total { get; set; }
    }

    public class DistributionReportPage
    {
        public int Page { get; set; }
        public bool IsLastPage { get; set; }
        public DistributionReportRow[] Items { get; set; }
        public decimal? SubTotal { get; set; }
    }

    /// <summary>
    /// Property order matters: the rows are serialized straight into the exported spreadsheet,
    /// and the anonymous type this replaces produced the columns in this order.
    /// </summary>
    public class DistributionReportRow
    {
        public string Name { get; set; }
        public string Sex { get; set; }
        public string NationalId { get; set; }
        public string MobileNumber { get; set; }
        public string Location { get; set; }
        public string VoucherCode { get; set; }
        public decimal? Value { get; set; }
        public string StatusString { get; set; }
        public string ConfirmationCode { get; set; }
    }
}
