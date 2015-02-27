using EmergencyVoucherManagement.Models.Vouchers;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web;
using System.Web.Http;

namespace EmergencyVoucherManagement.Controllers.OData
{
    [Authorize]
    public class VendorsController : AbstractODataController<Context, Models.Vouchers.Vendor>
    {
        protected override DbSet<Vendor> Collection
        {
            get { return db.Vendors; }
        }
    }

    [Authorize]
    public class BeneficiariesController : AbstractODataController<Models.Vouchers.Context, Models.Vouchers.Beneficiary>
    {
        protected override System.Data.Entity.DbSet<Models.Vouchers.Beneficiary> Collection
        {
            get { return db.Beneficiaries; }
        }
    }

    [Authorize]
    public class VouchersController : AbstractODataController<Models.Vouchers.Context, Models.Vouchers.Voucher>
    {
        protected override System.Data.Entity.DbSet<Models.Vouchers.Voucher> Collection
        {
            get { return db.Vouchers; }
        }
    }

    [Authorize]
    public class VoucherVerificationItemsController : AbstractODataController<Models.Vouchers.Context, Models.Vouchers.VoucherTransactionRecord>
    {
        protected override System.Data.Entity.DbSet<Models.Vouchers.VoucherTransactionRecord> Collection
        {
            get { return db.VoucherTransactionRecords; }
        }
    }
}