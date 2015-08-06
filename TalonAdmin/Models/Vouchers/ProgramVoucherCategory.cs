using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Web;

namespace TalonAdmin.Models.Vouchers
{
    public class ProgramVoucherCategory : TenantEntity
    {
        public ProgramVoucherCategory()
        {
            IssuedVouchers = 0;
        }


        public virtual int TypeId { get; set; }
        public virtual int ProgramId { get; set; }

        public virtual int NumberOfVouchers { get; set; }
        public virtual decimal? Value { get; set; }

        public virtual int? VendorTypeId { get; set; }

        [JsonIgnore]
        public virtual Program Program { get; set; }
        public virtual VoucherType Type { get; set; }
        public virtual VendorType VendorType { get; set; }
        public virtual int? ValidAfterOffset { get; set; }
        public virtual int? ValidAfterOffsetType { get; set; }

        [DefaultValue(0)]
        public virtual int IssuedVouchers { get; set; }
    }
}