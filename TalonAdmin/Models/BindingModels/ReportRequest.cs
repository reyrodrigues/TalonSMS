using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TalonAdmin.Controllers.BindingModels
{
    public class ReportRequestBindingModel
    {
        public int? DistributionId { get; set; }
        public int? VendorId { get; set; }
        public int? ProgramId { get; set; }
        public int CountryId { get; set; }
        public int OrganizationId { get; set; }
        public string PeriodStart { get; set; }
        public string PeriodEnd { get; set; }
    }
}
