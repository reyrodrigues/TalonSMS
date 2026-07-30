using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace TalonAdmin.Models.Admin
{
    public class AdminContext : IdentityDbContext<ApplicationUser, ApplicationRole, string>
    {
        public AdminContext(DbContextOptions<AdminContext> options)
            : base(options)
        {
        }

        public DbSet<Action> Actions { get; set; }
        public DbSet<ActionRole> ActionRoles { get; set; }
        public DbSet<Country> Countries { get; set; }
        public DbSet<CountrySettings> CountrySettings { get; set; }
        public DbSet<MenuItem> MenuItems { get; set; }
        public DbSet<MenuCategoryRole> MenuCategoryRoles { get; set; }
        public DbSet<MenuCategory> MenuCategories { get; set; }
        public DbSet<Organization> Organizations { get; set; }
        public DbSet<OrganizationCountry> OrganizationCountries { get; set; }
        public DbSet<ApplicationUserCountry> ApplicationUserCountries { get; set; }

        protected override void ConfigureConventions(ModelConfigurationBuilder builder)
        {
            base.ConfigureConventions(builder);

            builder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
            builder.Properties<DateTime?>().HaveConversion<NullableUtcDateTimeConverter>();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Re-establish the Identity 2 navigations that Core Identity leaves off the model.
            modelBuilder.Entity<ApplicationUser>()
                .HasMany(u => u.Roles)
                .WithOne()
                .HasForeignKey(ur => ur.UserId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ApplicationRole>()
                .HasMany(r => r.Users)
                .WithOne()
                .HasForeignKey(ur => ur.RoleId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ApplicationUserCountry>()
                .HasOne(p => p.ApplicationUser)
                .WithMany(p => p.Countries)
                .HasForeignKey(p => p.ApplicationUserId)
                .IsRequired();

            modelBuilder.Entity<MenuCategory>()
                .HasMany(p => p.Items)
                .WithOne(p => p.Category)
                .HasForeignKey(p => p.CategoryId)
                .IsRequired(false);

            modelBuilder.Entity<MenuCategory>()
                .HasMany(p => p.Roles)
                .WithOne(p => p.Category)
                .HasForeignKey(p => p.CategoryId)
                .IsRequired();

            modelBuilder.Entity<MenuItem>()
                .HasMany(p => p.Children)
                .WithOne(p => p.Parent)
                .HasForeignKey(p => p.ParentId)
                .IsRequired(false);

            // EF6 declared this key with [Key, Column(Order = ...)]; EF Core needs it in fluent config.
            modelBuilder.Entity<CountrySettingsProperty>(b =>
            {
                b.HasKey(p => new { p.SettingsId, p.Name });
                b.Property(p => p.Name).HasMaxLength(200);
                b.HasOne(p => p.Settings)
                    .WithMany(p => p.PropertyCollection)
                    .HasForeignKey(p => p.SettingsId)
                    .IsRequired();
            });

            modelBuilder.Entity<Country>()
                .HasOne(p => p.Settings)
                .WithMany()
                .HasForeignKey(p => p.SettingsId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<OrganizationCountry>()
                .HasOne(p => p.Settings)
                .WithMany()
                .HasForeignKey(p => p.SettingsId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
