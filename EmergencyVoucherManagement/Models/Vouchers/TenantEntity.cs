using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace TalonAdmin.Models.Vouchers
{
    public class TenantEntity : CountryEntity
    {
        [Index]
        public int OrganizationId { get; set; }
    }
}