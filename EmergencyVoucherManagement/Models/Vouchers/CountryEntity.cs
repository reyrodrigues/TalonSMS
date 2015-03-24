using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace TalonAdmin.Models.Vouchers
{
    public class CountryEntity : Entity
    {
        [Index]
        public int CountryId { get; set; }
    }
}