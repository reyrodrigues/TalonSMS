using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace TalonAdmin.Models.Vouchers
{
    public class Context : DbContext
    {
        public Context(DbContextOptions<Context> options)
            : base(options)
        {
        }

        public DbSet<AuditLogItem> AuditLogItems { get; set; }
        public DbSet<Vendor> Vendors { get; set; }
        public DbSet<VendorAdditionalData> VendorAdditionalData { get; set; }
        public DbSet<VendorSalesPerson> VendorSalesPersons { get; set; }
        public DbSet<VendorDevice> VendorDevices { get; set; }
        public DbSet<VendorType> VendorTypes { get; set; }
        public DbSet<Beneficiary> Beneficiaries { get; set; }
        public DbSet<BeneficiaryAdditionalData> BeneficiaryAdditionalData { get; set; }
        public DbSet<BeneficiaryGroup> BeneficiaryGroups { get; set; }
        public DbSet<Location> Locations { get; set; }
        public DbSet<Program> Programs { get; set; }
        public DbSet<ProgramVoucherCategory> ProgramVoucherCategories { get; set; }
        public DbSet<Distribution> Distributions { get; set; }
        public DbSet<DistributionLog> DistributionLogs { get; set; }
        public DbSet<ExportedReport> ExportedReports { get; set; }
        public DbSet<DistributionVoucherCategory> DistributionVoucherCategories { get; set; }
        public DbSet<Voucher> Vouchers { get; set; }
        public DbSet<VoucherType> VoucherTypes { get; set; }
        public DbSet<VoucherTransactionRecord> VoucherTransactionRecords { get; set; }
        public DbSet<MessageLog> MessageLogs { get; set; }
        public DbSet<TransactionLogItem> TransactionLogItems { get; set; }
        public DbSet<CardLoad> CardLoads { get; set; }

        protected override void ConfigureConventions(ModelConfigurationBuilder builder)
        {
            base.ConfigureConventions(builder);

            builder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
            builder.Properties<DateTime?>().HaveConversion<NullableUtcDateTimeConverter>();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Beneficiary>()
                .HasOne(p => p.Group)
                .WithMany(p => p.Beneficiaries)
                .HasForeignKey(p => p.GroupId)
                .IsRequired(false);

            modelBuilder.Entity<Distribution>()
                .HasMany(p => p.Categories)
                .WithOne(p => p.Distribution)
                .HasForeignKey(p => p.DistributionId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Distribution>()
                .HasMany(p => p.Vouchers)
                .WithOne(p => p.Distribution)
                .HasForeignKey(p => p.DistributionId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Voucher>()
                .HasMany(p => p.TransactionRecords)
                .WithOne(p => p.Voucher)
                .HasForeignKey(p => p.VoucherId)
                .OnDelete(DeleteBehavior.Restrict);

            // Voucher.Category and Distribution.Categories would otherwise give SQL Server two
            // cascade paths into Voucher.
            modelBuilder.Entity<Voucher>()
                .HasOne(p => p.Category)
                .WithMany()
                .HasForeignKey(p => p.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Program>()
                .HasMany(p => p.Distributions)
                .WithOne(p => p.Program)
                .HasForeignKey(p => p.ProgramId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<BeneficiaryAdditionalData>().ToTable("BeneficiaryAdditionalData");
            modelBuilder.Entity<VendorAdditionalData>().ToTable("VendorAdditionalData");

            // Replaces the EF6 property-level [Index] attributes on CountryEntity/TenantEntity.
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                var clrType = entityType.ClrType;
                if (typeof(CountryEntity).IsAssignableFrom(clrType))
                {
                    modelBuilder.Entity(clrType).HasIndex(nameof(CountryEntity.CountryId));
                }

                if (typeof(TenantEntity).IsAssignableFrom(clrType))
                {
                    modelBuilder.Entity(clrType).HasIndex(nameof(TenantEntity.OrganizationId));
                }
            }

            modelBuilder.Entity<Voucher>().HasIndex(v => v.VoucherCode);
            modelBuilder.Entity<Voucher>().HasIndex(v => v.SequentialCode);

            // Restrict the remaining conventional required relationships so a delete cannot
            // fan out through several paths at once (SQL Server rejects that outright).
            foreach (var fk in modelBuilder.Model.GetEntityTypes()
                         .SelectMany(t => t.GetForeignKeys())
                         .Where(fk => fk.DeleteBehavior == DeleteBehavior.Cascade
                                      && fk.DeclaringEntityType.ClrType != typeof(DistributionVoucherCategory)
                                      && fk.DeclaringEntityType.ClrType != typeof(Distribution)))
            {
                fk.DeleteBehavior = DeleteBehavior.Restrict;
            }
        }
    }
}
