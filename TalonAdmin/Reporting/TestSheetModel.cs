using System;

namespace TalonAdmin.Reporting
{
    /// <summary>
    /// Backs /Views/Reports/TestSheet.cshtml.
    /// </summary>
    public class TestSheetModel : ReportModelBase
    {
        public TestSheetCode[] Codes { get; set; }
    }

    public class TestSheetCode
    {
        public string QR { get; set; }
        public string Beneficiary { get; set; }
        public decimal? Value { get; set; }
        public DateTime? ValidAfter { get; set; }
    }
}
