using System.Linq;
using Breeze.AspNetCore;
using Breeze.Persistence;
using Breeze.Persistence.EFCore;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using TalonAdmin.Attributes;
using TalonAdmin.Extensions;

namespace TalonAdmin.Controllers.Breeze
{
    [AuthorizeTenant]
    [BreezeQueryFilter(MaxDepth = 5)]
    [Route("breeze/EVM/[action]")]
    public class EVMController : Controller
    {
        private readonly EFPersistenceManager<Models.Vouchers.Context> _persistenceManager;

        public EVMController(Models.Vouchers.Context context)
        {
            _persistenceManager = new EFPersistenceManager<Models.Vouchers.Context>(context);
        }

        private Models.Vouchers.Context Db
        {
            get { return _persistenceManager.Context; }
        }

        [HttpGet]
        public string Metadata()
        {
            return _persistenceManager.Metadata();
        }

        [HttpGet]
        public IQueryable<Models.Vouchers.Beneficiary> Beneficiaries()
        {
            return Db.Beneficiaries
                .FilterCountry(this)
                .FilterOrganization(this);
        }

        [HttpGet]
        public IQueryable<Models.Vouchers.BeneficiaryGroup> BeneficiaryGroups()
        {
            return Db.BeneficiaryGroups
                .FilterCountry(this)
                .FilterOrganization(this);
        }

        [HttpGet]
        public IQueryable<Models.Vouchers.Program> Programs()
        {
            return Db.Programs
                .FilterCountry(this)
                .FilterOrganization(this);
        }

        [HttpGet]
        public IQueryable<Models.Vouchers.Distribution> Distributions()
        {
            return Db.Distributions
                .FilterCountry(this)
                .FilterOrganization(this);
        }

        [HttpGet]
        public IQueryable<Models.Vouchers.Beneficiary> DistributionBeneficiaries(int distributionId)
        {
            return Db.VoucherTransactionRecords
                .FilterCountry(this)
                .Where(v => v.Type == 1)
                .Where(v => v.Voucher.DistributionId == distributionId)
                .Select(v => v.Beneficiary)
                .Distinct();
        }

        [HttpGet]
        public IQueryable<Models.Vouchers.ExportedReport> ExportedReports()
        {
            return Db.ExportedReports
                .FilterCountry(this)
                .FilterOrganization(this);
        }

        [HttpGet]
        public IQueryable<Models.Vouchers.DistributionLog> DistributionLogs()
        {
            return Db.DistributionLogs
                .FilterCountry(this)
                .FilterOrganization(this);
        }

        [HttpGet]
        public IQueryable<Models.Vouchers.DistributionVoucherCategory> DistributionVoucherCategories()
        {
            return Db.DistributionVoucherCategories
                .FilterCountry(this)
                .FilterOrganization(this);
        }

        [HttpGet]
        public IQueryable<Models.Vouchers.Location> Locations()
        {
            return Db.Locations
                .FilterCountry(this);
        }

        [HttpGet]
        public IQueryable<Models.Vouchers.MessageLog> MessageLogs()
        {
            return Db.MessageLogs;
        }

        [HttpGet]
        public IQueryable<Models.Vouchers.AuditLogItem> AuditLogItems()
        {
            return Db.AuditLogItems;
        }

        [HttpGet]
        public IQueryable<Models.Vouchers.Vendor> Vendors()
        {
            return Db.Vendors
                .FilterCountry(this);
        }

        [HttpGet]
        public IQueryable<Models.Vouchers.VendorDevice> VendorDevices()
        {
            return Db.VendorDevices
                .FilterCountry(this);
        }

        [HttpGet]
        public IQueryable<Models.Vouchers.VendorSalesPerson> VendorSalesPersons()
        {
            return Db.VendorSalesPersons
                .FilterCountry(this);
        }

        [HttpGet]
        public IQueryable<Models.Vouchers.VendorType> VendorTypes()
        {
            return Db.VendorTypes
                .FilterCountry(this);
        }

        [HttpGet]
        public IQueryable<Models.Vouchers.Voucher> Vouchers()
        {
            return Db.Vouchers
                .FilterCountry(this)
                .FilterOrganization(this);
        }

        [HttpGet]
        public IQueryable<Models.Vouchers.VoucherType> VoucherTypes()
        {
            return Db.VoucherTypes
                .FilterCountry(this)
                .FilterOrganization(this);
        }

        [HttpGet]
        public IQueryable<Models.Vouchers.VoucherTransactionRecord> VoucherTransactionRecords()
        {
            return Db.VoucherTransactionRecords
                .FilterCountry(this)
                .FilterOrganization(this);
        }

        /// <summary>
        /// Filtered Voucher Transactions limited to just vouchers issued.
        /// </summary>
        [HttpGet]
        public IQueryable<Models.Vouchers.VoucherTransactionRecord> IssuedVoucherTransactionRecords()
        {
            return Db.VoucherTransactionRecords
                .Where(v => v.Type == 1)
                .FilterCountry(this)
                .FilterOrganization(this);
        }

        [HttpGet]
        public IQueryable<Models.Vouchers.TransactionLogItem> TransactionLogItems()
        {
            return Db.TransactionLogItems
                .FilterCountry(this)
                .FilterOrganization(this);
        }

        [HttpGet]
        public IQueryable<Models.Vouchers.CardLoad> CardLoads()
        {
            return Db.CardLoads
                .FilterCountry(this)
                .FilterOrganization(this);
        }

        [HttpGet]
        public IQueryable<Models.Vouchers.Vendor> DistributionVendors(int distributionId)
        {
            return Db.VoucherTransactionRecords
                .Where(v => v.Voucher.DistributionId == distributionId && v.VendorId != null)
                .Select(v => v.Vendor)
                .Distinct();
        }

        [HttpGet]
        public IQueryable<Models.Vouchers.Vendor> ProgramVendors(int programId)
        {
            return Db.VoucherTransactionRecords
                .Where(v => v.Voucher.Distribution.ProgramId == programId && v.VendorId != null)
                .Select(v => v.Vendor)
                .Distinct();
        }

        [HttpPost]
        public SaveResult SaveChanges([FromBody] JObject saveBundle)
        {
            return _persistenceManager.SaveChanges(saveBundle);
        }
    }
}
