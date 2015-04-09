using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.Core.Objects;
using System.ComponentModel.DataAnnotations;
using System.Data.Entity.Infrastructure.Annotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity.ModelConfiguration.Conventions;
using System.Data.Entity.ModelConfiguration.Configuration;



namespace TalonAdmin.Models.Vouchers
{
    public class DataTypePropertyAttributeConvention
    : PrimitivePropertyAttributeConfigurationConvention<DataTypeAttribute>
    {
        public override void Apply(ConventionPrimitivePropertyConfiguration configuration,
            DataTypeAttribute attribute)
        {
            if (attribute.DataType == DataType.Date)
            {
                configuration.HasColumnType("Date");
            }
        }
    }

    public class Context : DbContext
    {
        public Context()
            : base("name=VoucherContext")
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

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Conventions.Add(new DataTypePropertyAttributeConvention());

#if DEBUG
            Database.SetInitializer(new System.Data.Entity.DropCreateDatabaseIfModelChanges<Context>());
#endif
            modelBuilder.Entity<DistributionVoucherCategory>()
                .HasRequired(p => p.Distribution)
                .WithMany(p => p.Categories);

            modelBuilder.Entity<Voucher>()
                .HasRequired(p => p.Distribution)
                .WithMany(p => p.Vouchers)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<VoucherTransactionRecord>()
                .HasRequired(p => p.Voucher)
                .WithMany(p => p.TransactionRecords)
                .WillCascadeOnDelete(false);

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
        public DbSet<VendorType> VendorTypes { get; set; }
        public DbSet<Beneficiary> Beneficiaries { get; set; }
        public DbSet<BeneficiaryGroup> BeneficiaryGroups { get; set; }
        public DbSet<BeneficiaryDistribution> BeneficiaryDistributions { get; set; }
        public DbSet<Location> Locations { get; set; }
        public DbSet<Distribution> Distributions { get; set; }
        public DbSet<DistributionLog> DistributionLogs { get; set; }
        public DbSet<DistributionVoucherCategory> DistributionVoucherCategories { get; set; }
        public DbSet<Voucher> Vouchers { get; set; }
        public DbSet<VoucherType> VoucherTypes { get; set; }
        public DbSet<VoucherTransactionRecord> VoucherTransactionRecords { get; set; }
        public DbSet<MessageLog> MessageLogs { get; set; }
    }
}