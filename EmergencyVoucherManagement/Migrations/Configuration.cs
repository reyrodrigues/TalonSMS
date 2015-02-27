namespace EmergencyVoucherManagement.Migrations
{
    using EmergencyVoucherManagement.Models.Vouchers;
    using System;
    using System.Data.Entity;
    using System.Data.Entity.Migrations;
    using System.Linq;

    internal sealed class Configuration : DbMigrationsConfiguration<EmergencyVoucherManagement.Models.Vouchers.Context>
    {
        public Configuration()
        {
            AutomaticMigrationsEnabled = false;
            ContextKey = "EmergencyVoucherManagement.Models.Context";
        }

        protected override void Seed(EmergencyVoucherManagement.Models.Vouchers.Context context)
        {
            context.Vendors.AddOrUpdate(
               v => v.MobileNumber,
               new Vendor { MobileNumber = "5551234", Name = "Vendor 1", OwnerName = "Rey Rodrigues" }
           );

            context.Beneficiaries.AddOrUpdate(
                b => b.NationalId,
                new Beneficiary { NationalId = "111222333444", PIN = "1234", Name = "Rey Rodrigues" }
            );

            context.Vouchers.AddOrUpdate(
                new Voucher { Value = new Random().Next(1000, 10000) },
                new Voucher { Value = new Random().Next(1000, 10000) },
                new Voucher { Value = new Random().Next(1000, 10000) },
                new Voucher { Value = new Random().Next(1000, 10000) },
                new Voucher { Value = new Random().Next(1000, 10000) },
                new Voucher { Value = new Random().Next(1000, 10000) },
                new Voucher { Value = new Random().Next(1000, 10000) },
                new Voucher { Value = new Random().Next(1000, 10000) }
            );

            //  This method will be called after migrating to the latest version.

            //  You can use the DbSet<T>.AddOrUpdate() helper extension method 
            //  to avoid creating duplicate seed data. E.g.
            //
            //    context.People.AddOrUpdate(
            //      p => p.FullName,
            //      new Person { FullName = "Andrew Peters" },
            //      new Person { FullName = "Brice Lambson" },
            //      new Person { FullName = "Rowan Miller" }
            //    );
            //
        }
    }
}
