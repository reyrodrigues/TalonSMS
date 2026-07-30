using System;
using System.Collections.Generic;
using TalonAdmin.Models.Admin;
using TalonAdmin.Models.Vouchers;

namespace TalonAdmin.Reporting
{
    /// <summary>
    /// Backs /Views/Reports/VendorProgramReceiptReport.cshtml.
    /// </summary>
    public class VendorProgramReceiptReportModel : ReportModelBase
    {
        public ApplicationUser User { get; set; }
        public Models.Vouchers.Program Program { get; set; }
        public Organization Organization { get; set; }
        public Country Country { get; set; }
        public List<VendorProgramReceiptReportPage> Pages { get; set; }
        public VendorProgramReceiptReportRow[] Items { get; set; }
        public int PageSize { get; set; }
        public Vendor Vendor { get; set; }
        public decimal? Total { get; set; }
    }

    public class VendorProgramReceiptReportPage
    {
        public int Page { get; set; }
        public bool IsLastPage { get; set; }
        public VendorProgramReceiptReportRow[] Items { get; set; }
        public decimal? SubTotal { get; set; }
    }

    /// <summary>
    /// Property order matters: the rows are serialized straight into the exported spreadsheet,
    /// and the anonymous type this replaces produced the columns in this order.
    /// </summary>
    public class VendorProgramReceiptReportRow
    {
        public string Name { get; set; }
        public string MobileNumber { get; set; }
        public string NationalId { get; set; }
        public DateTime? FinalizedOn { get; set; }
        public string VoucherCode { get; set; }
        public string ConfirmationCode { get; set; }
        public decimal? Value { get; set; }
        public int? VoucherId { get; set; }
        public int DistributionId { get; set; }
        public string DistributionNumber { get; set; }
    }
}
