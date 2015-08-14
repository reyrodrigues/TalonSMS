using Microsoft.AspNet.Identity.EntityFramework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.Core.Objects;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json.Linq;

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

        public DbSet<IdentityUserRole> UserRoles { get; set; }
        public DbSet<Action> Actions { get; set; }
        public DbSet<ActionRole> ActionRoles { get; set; }
        public DbSet<Country> Countries { get; set; }
        public DbSet<MenuItem> MenuItems { get; set; }
        public DbSet<MenuCategoryRole> MenuCategoryRoles { get; set; }
        public DbSet<MenuCategory> MenuCategories { get; set; }
        public DbSet<Organization> Organizations { get; set; }
        public DbSet<OrganizationCountry> OrganizationCountries { get; set; }
        public DbSet<ApplicationUserCountry> ApplicationUserCountries { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<ApplicationUserCountry>()
                .HasRequired(p => p.ApplicationUser)
                .WithMany(p => p.Countries);

            modelBuilder.Entity<MenuCategory>()
                .HasMany(p => p.Items)
                .WithOptional(p => p.Category);

            modelBuilder.Entity<MenuCategory>()
                .HasMany(p => p.Roles)
                .WithRequired(p => p.Category);

            modelBuilder.Entity<MenuItem>()
                .HasMany(p => p.Children)
                .WithOptional(p => p.Parent);


            Database.SetInitializer(new NullDatabaseInitializer<AdminContext>());
        }
    }

}