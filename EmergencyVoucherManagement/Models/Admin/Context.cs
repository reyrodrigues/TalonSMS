using Microsoft.AspNet.Identity.EntityFramework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.Core.Objects;
using System.ComponentModel.DataAnnotations;

namespace TalonAdmin.Models.Admin
{
    public class AdminContext : IdentityDbContext<ApplicationUser>
    {
        public AdminContext()
            : base("name=AdminContext")
        {
            ((IObjectContextAdapter)this).ObjectContext.ObjectMaterialized += (sender, e) => ApplyUTC(e.Entity);

        }

        private static void ApplyUTC(object entity)
        {
            if (entity == null)
                return;

            var properties = entity.GetType().GetProperties()
                .Where(x => x.PropertyType == typeof(DateTime) || x.PropertyType == typeof(DateTime?));

            foreach (var property in properties)
            {

                var dt = property.PropertyType == typeof(DateTime?)
                    ? (DateTime?)property.GetValue(entity, null)
                    : (DateTime)property.GetValue(entity, null);

                if (dt == null)
                    continue;

                property.SetValue(entity, DateTime.SpecifyKind(dt.Value, DateTimeKind.Utc), null);
            }
        }

        public static AdminContext Create()
        {
            return new AdminContext();
        }

        public DbSet<Country> Countries { get; set; }
        public DbSet<Organization> Organizations { get; set; }
        public DbSet<ApplicationUserCountry> ApplicationUserCountries { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<ApplicationUserCountry>()
                .HasRequired(p => p.ApplicationUser)
                .WithMany(p => p.ApplicationUserCountries);

#if DEBUG
            Database.SetInitializer(new CleanDbInitializer());
#endif
        }
    }

    public class CleanDbInitializer : DropCreateDatabaseAlways<AdminContext>
    {
        protected override void Seed(AdminContext context)
        {
            var ircsLogo = Convert.ToBase64String(System.IO.File.ReadAllBytes(HttpContext.Current.Server.MapPath("~/Content/IRCLogo.svg")));
            var irc = new Organization
            {
                Abbreviation = "IRC",
                Name = "The International Rescue Committee",
                LogoSVG = ircsLogo
            };

            var stcsLogo = Convert.ToBase64String(System.IO.File.ReadAllBytes(HttpContext.Current.Server.MapPath("~/Content/STCLogo.svg")));
            var stc = new Organization
            {
                Abbreviation = "Save",
                Name = "Save The Children",
                LogoSVG = stcsLogo
            };
            var ukraine = new Country
            {
                Name = "Ukraine",
                IsoAlpha2 = "UA",
                IsoAlpha3 = "UKR"
            };
            var unitedStates = new Country
            {
                Name = "United States of America",
                IsoAlpha2 = "US",
                IsoAlpha3 = "USA"
            };

            var reynaldor = new ApplicationUser
            {
                UserName = "reynaldor",
                FullName = "Rey Rodrigues",
                Email = "reynaldo.rodrigues@rescue.org",
                Organization = irc
            };

            var uaMembership = new ApplicationUserCountry
            {
                Country = ukraine,
                ApplicationUser = reynaldor
            };

            var usMembership = new ApplicationUserCountry
            {
                Country = unitedStates,
                ApplicationUser = reynaldor
            };


            context.Users.Add(reynaldor);

            context.Organizations.Add(irc);
            context.Organizations.Add(stc);

            context.Countries.Add(ukraine);

            context.ApplicationUserCountries.Add(uaMembership);
            context.ApplicationUserCountries.Add(usMembership);


            context.SaveChanges();

            base.Seed(context);
        }
    }
}