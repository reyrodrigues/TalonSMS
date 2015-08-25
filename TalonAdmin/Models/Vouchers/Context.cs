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

            //modelBuilder.Conventions.Add(new DataTypePropertyAttributeConvention());

#if !DEBUG
            Database.SetInitializer(new NullDatabaseInitializer<Context>());
#endif
#if DEBUG
            Database.SetInitializer(new NullDatabaseInitializer<Context>());
           // Database.SetInitializer(new CreateDatabaseIfNotExists<Context>());
            // Database.SetInitializer(new DropCreateDatabaseAlways<Context>());
#endif

            modelBuilder.Entity<Beneficiary>()
                .HasOptional(p => p.Group)
                .WithMany(p => p.Beneficiaries);

            modelBuilder.Entity<Distribution>()
                .HasMany(p => p.Categories)
                .WithRequired(p => p.Distribution)
                .WillCascadeOnDelete();

            modelBuilder.Entity<Distribution>()
                .HasMany(p => p.Vouchers)
                .WithRequired(p => p.Distribution)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Voucher>()
                .HasMany(p => p.TransactionRecords)
                .WithRequired(p => p.Voucher)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Program>()
                .HasMany(p => p.Distributions)
                .WithRequired(p => p.Program)
                .WillCascadeOnDelete();

            modelBuilder.Entity<BeneficiaryAdditionalData>().ToTable("BeneficiaryAdditionalData");
            modelBuilder.Entity<VendorAdditionalData>().ToTable("VendorAdditionalData");
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

    }
}