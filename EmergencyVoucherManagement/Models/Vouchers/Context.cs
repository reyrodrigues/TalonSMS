using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.Core.Objects;



namespace EmergencyVoucherManagement.Models.Vouchers
{
    public class Context : DbContext
    {
        public Context()
            : base("name=VoucherContext")
        {
        }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
#if DEBUG
            Database.SetInitializer(new System.Data.Entity.NullDatabaseInitializer<Context>());
#endif

            modelBuilder.Entity<DistributionVoucherCategory>()
                .HasRequired(p => p.Distribution)
                .WithMany(p => p.Categories);

            modelBuilder.Entity<DistributionVendor>()
                .HasRequired(p => p.Distribution)
                .WithMany(p => p.Vendors);

            modelBuilder.Entity<Voucher>()
                .HasOptional(p => p.TransactionRecord)
                .WithOptionalDependent(p => p.Voucher)
                .Map(p=>p.MapKey("TransactionRecordId"));

            modelBuilder.Entity<Voucher>()
                .HasRequired(p => p.Distribution)
                .WithMany(p => p.Vouchers);

            modelBuilder.Entity<BeneficiaryDistribution>()
                .HasRequired(p => p.Beneficiary)
                .WithMany(p => p.Distributions);

            modelBuilder.Entity<BeneficiaryDistribution>()
                .HasRequired(p => p.Distribution)
                .WithMany(p => p.Beneficiaries);

            modelBuilder.Entity<Beneficiary>()
                .HasOptional(p => p.Group)
                .WithMany(p => p.Beneficiaries);
        }

        public DbSet<Vendor> Vendors { get; set; }
        public DbSet<Beneficiary> Beneficiaries { get; set; }
        public DbSet<BeneficiaryGroup> BeneficiaryGroups { get; set; }
        public DbSet<BeneficiaryDistribution> BeneficiaryDistributions { get; set; }
        public DbSet<Location> Locations { get; set; }
        public DbSet<Distribution> Distributions { get; set; }
        public DbSet<DistributionVendor> DistributionVendors { get; set; }
        public DbSet<DistributionVoucherCategory> DistributionVoucherCategories { get; set; }
        public DbSet<Voucher> Vouchers { get; set; }
        public DbSet<VoucherType> VoucherTypes { get; set; }
        public DbSet<VoucherTransactionRecord> VoucherTransactionRecords { get; set; }
    }
}