using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;
using Microsoft.AspNet.Identity.Owin;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using System.Xml.Serialization;
using System.Runtime.Serialization;

namespace EmergencyVoucherManagement.Models.Admin
{
    public class ApplicationUser : IdentityUser
    {
        public virtual string FullName { get; set; }

        public virtual int OrganizationId { get; set; }

        public virtual Organization Organization { get; set; }

        [JsonIgnore, XmlIgnore, IgnoreDataMember]
        public virtual ICollection<ApplicationUserCountry> ApplicationUserCountries { get; set; }

        public IEnumerable<Country> Countries
        {
            get
            {
                return this.ApplicationUserCountries.Select(u => u.Country);
            }
        }

        public async Task<ClaimsIdentity> GenerateUserIdentityAsync(UserManager<ApplicationUser> manager, string authenticationType)
        {
            // Note the authenticationType must match the one defined in CookieAuthenticationOptions.AuthenticationType
            var userIdentity = await manager.CreateIdentityAsync(this, authenticationType);
            // Add custom user claims here
            return userIdentity;
        }
    }
}