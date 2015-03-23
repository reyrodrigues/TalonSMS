using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace EmergencyVoucherManagement.Models.Vouchers
{
    public class TenantEntity : CountryEntity
    {
        public int OrganizationId { get; set; }
    }
}