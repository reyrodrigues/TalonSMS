using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace TalonAdmin.Models.Vouchers
{
    public class ExportedReport : TenantEntity
    {
        public virtual int ProgramId { get; set; }
        public virtual int ReportType { get; set; }
        public virtual string Description { get; set; }

        [Column(TypeName = "ntext")]
        public virtual string Spreadsheet { get; set; }

        [Column(TypeName = "ntext")]
        public virtual string OriginalReport { get; set; }

        [Column(TypeName = "ntext")]
        public virtual string SignedReport { get; set; }

        public virtual DateTime? ReportRunOn { get; set; }
        public virtual string ReportRunBy { get; set; }

        public virtual Program Program { get; set; }
    }
}