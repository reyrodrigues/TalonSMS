using Microsoft.AspNet.Identity.EntityFramework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.Core.Objects;
using System.ComponentModel.DataAnnotations;

namespace EmergencyVoucherManagement.Models.Admin
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

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

#if DEBUG
            Database.SetInitializer(new CleanDbInitializer());
#endif
        }
    }

    public class CleanDbInitializer : DropCreateDatabaseAlways<AdminContext>
    {
        protected override void Seed(AdminContext context)
        {
            context.Users.Add(new ApplicationUser
            {
                UserName = "reynaldor",
                FullName = "Rey Rodrigues",
                Email = "reynaldo.rodrigues@rescue.org"
            });

            context.Organizations.Add(new Organization
            {
                Abbreviation = "IRC",
                Name = "The International Rescue Committee"
            });

            context.Countries.Add(new Country
            {
                Name = "Ukraine",
                IsoAlpha2 = "UA",
                IsoAlpha3 = "UKR"
            });


            context.SaveChanges();

            base.Seed(context);
        }
    }
}