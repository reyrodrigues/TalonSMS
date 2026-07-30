using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace TalonAdmin.Models.Vouchers
{
    public class TenantEntity : CountryEntity
    {
        public int OrganizationId { get; set; }
    }
}