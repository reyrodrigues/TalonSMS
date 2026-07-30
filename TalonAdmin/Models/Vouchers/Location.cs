using System;
using System.Collections.Generic;
using System.Linq;

namespace TalonAdmin.Models.Vouchers
{
    public class Location : CountryEntity
    {
        public virtual string Name { get; set; }
    }
}