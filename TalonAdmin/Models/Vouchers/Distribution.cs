using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace TalonAdmin.Models.Vouchers
{
    public class Distribution : TenantEntity
    {
        public Distribution()
        {
            VoucherCodeLength = 6;
            CreatedOn = DateTime.UtcNow;
            ModifiedOn = DateTime.UtcNow;
        }

        public virtual int? Number { get; set; }
        public virtual string Title { get; set; }
        public virtual string FundCodes { get; set; }
        public virtual string ClosedBy { get; set; }
        public virtual string CreatedBy { get; set; }
        public virtual string ModifiedBy { get; set; }

        [DefaultValue(6)] public virtual int VoucherCodeLength { get; set; }
        public virtual int? ProgramId { get; set; }
        public virtual int? LocationId { get; set; }

        public virtual bool IsClosed { get; set; }
        public virtual int? GroupId { get; set; }

        public virtual DateTime? ClosedOn { get; set; }
        public virtual DateTime CreatedOn { get; set; }
        public virtual DateTime ModifiedOn { get; set; }

        [JsonIgnore]
        public virtual Program Program { get; set; }
        public virtual BeneficiaryGroup Group { get; set; }
        public virtual Location Location { get; set; }
        public virtual ICollection<DistributionVoucherCategory> Categories { get; set; }
        public virtual ICollection<Voucher> Vouchers { get; set; }
        public virtual ICollection<BeneficiaryDistribution> Beneficiaries { get; set; }

    }

    public enum DistributionStatus
    {
        Created = 1,
        Started = 2,
        Closed = 3
    }
}