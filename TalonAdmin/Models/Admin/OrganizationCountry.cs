using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace TalonAdmin.Models.Admin
{
    public class OrganizationCountry : Entity
    {
        public virtual int OrganizationId { get; set; }
        public virtual int CountryId { get; set; }

        public virtual int? SettingsId { get; set; }

        public virtual CountrySettings Settings { get; set; }

        public virtual Organization Organization { get; set; }
        public virtual Country Country { get; set; }
    }
}