using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace TalonAdmin.Models.Vouchers
{
    public class ExportedReport : TenantEntity
    {
        public virtual int ProgramId { get; set; }
        public virtual int ReportType { get; set; }
        public virtual string Description { get; set; }

        public virtual string Spreadsheet { get; set; }

        public virtual string OriginalReport { get; set; }

        public virtual string SignedReport { get; set; }

        public virtual DateTime? ReportRunOn { get; set; }
        public virtual string ReportRunBy { get; set; }

        public virtual Program Program { get; set; }
    }
}