using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace TalonAdmin.Models.Vouchers
{
    public class ProgramVendorReconciliation : TenantEntity
    {
        public virtual DateTime? ReconciledOn { get; set; }
        public virtual int ProgramId { get; set; }
        public virtual int VendorId { get; set; }

        [Column(TypeName = "ntext")]
        public virtual string OriginalReport { get; set; }

        [Column(TypeName = "ntext")]
        public virtual string SignedReport { get; set; }

        public virtual Program Program { get; set; }
        public virtual Vendor Vendor { get; set; }

        public virtual DateTime? ReportRunOn { get; set; }
        public virtual string ReportRunBy { get; set; }
    }
}