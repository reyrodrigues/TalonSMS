using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace TalonAdmin.Models.Admin
{
    public class Country : Entity
    {
        public virtual string Name { get; set; }
        public virtual string IsoAlpha2 { get; set; }
        public virtual string IsoAlpha3 { get; set; }

        public virtual string CurrencyIsoCode { get; set; }
        public virtual string CountryCallingCode { get; set; }

        public virtual int SettingsId { get; set; }

        /// <summary>
        /// Not all currencies can afford the luxury of living above the number four.
        /// </summary>
        public virtual string CurrencyUnicodeSymbol { get; set; }

        public virtual CountrySettings Settings { get; set; }
    }
}