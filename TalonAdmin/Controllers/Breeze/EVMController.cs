using Breeze.ContextProvider;
using Breeze.ContextProvider.EF6;
using Breeze.WebApi2;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Web;
using System.Web.Http;
using TalonAdmin.Extensions;
using TalonAdmin.Attributes;

namespace TalonAdmin.Controllers.Breeze
{
    [BreezeController, AuthorizeTenant, EnableBreezeQuery(MaxExpansionDepth = 5)]
    public class EVMController : ApiController
    {

        readonly EFContextProvider<Models.Vouchers.Context> _contextProvider =
            new EFContextProvider<Models.Vouchers.Context>();

        // ~/breeze/todos/Metadata 
        [HttpGet]
        public string Metadata()
        {
            return _contextProvider.Metadata();
        }

        [HttpGet]
        public IQueryable<Models.Vouchers.Beneficiary> Beneficiaries()
        {

            return _contextProvider.Context
                .Beneficiaries
                .FilterCountry(this)
                .FilterOrganization(this);
        }

        [HttpGet]
        public IQueryable<Models.Vouchers.BeneficiaryGroup> BeneficiaryGroups()
        {
            return _contextProvider.Context.BeneficiaryGroups
                .FilterCountry(this)
                .FilterOrganization(this);
        }

        [HttpGet]
        public IQueryable<Models.Vouchers.Distribution> Distributions()
        {
            return _contextProvider.Context.Distributions
                .FilterCountry(this)
                .FilterOrganization(this);
        }
        

        [HttpGet]
        public IQueryable<Models.Vouchers.DistributionVendorReconciliation> DistributionVendorReconciliations()
        {
            return _contextProvider.Context.DistributionVendorReconciliations
                .FilterCountry(this)
                .FilterOrganization(this);
        }

        [HttpGet]
        public IQueryable<Models.Vouchers.DistributionLog> DistributionLogs()
        {
            return _contextProvider.Context.DistributionLogs
                .FilterCountry(this)
                .FilterOrganization(this);
        }

        [HttpGet]
        public IQueryable<Models.Vouchers.BeneficiaryDistribution> BeneficiaryDistributions()
        {
            return _contextProvider.Context.BeneficiaryDistributions
                .FilterCountry(this)
                .FilterOrganization(this);
        }

        [HttpGet]
        public IQueryable<Models.Vouchers.DistributionVoucherCategory> DistributionVoucherCategories()
        {
            return _contextProvider.Context.DistributionVoucherCategories
                .FilterCountry(this)
                .FilterOrganization(this);
        }

        [HttpGet]
        public IQueryable<Models.Vouchers.Location> Locations()
        {
            return _contextProvider.Context.Locations
                .FilterCountry(this);
        }

        [HttpGet]
        public IQueryable<Models.Vouchers.MessageLog> MessageLogs()
        {
            return _contextProvider.Context.MessageLogs;
        }

        [HttpGet]
        public IQueryable<Models.Vouchers.Vendor> Vendors()
        {
            return _contextProvider.Context.Vendors
                .FilterCountry(this);
        }

        [HttpGet]
        public IQueryable<Models.Vouchers.VendorType> VendorTypes()
        {
            return _contextProvider.Context.VendorTypes
                .FilterCountry(this);
        }

        [HttpGet]
        public IQueryable<Models.Vouchers.Voucher> Vouchers()
        {
            return _contextProvider.Context.Vouchers
                .FilterCountry(this)
                .FilterOrganization(this); 
        }

        [HttpGet]
        public IQueryable<Models.Vouchers.VoucherType> VoucherTypes()
        {
            return _contextProvider.Context.VoucherTypes
                .FilterCountry(this)
                .FilterOrganization(this); 
        }

        [HttpGet]
        public IQueryable<Models.Vouchers.VoucherTransactionRecord> VoucherTransactionRecords()
        {
            return _contextProvider.Context.VoucherTransactionRecords
                .FilterCountry(this)
                .FilterOrganization(this);
        }

        [HttpGet]
        public IQueryable<Models.Vouchers.Vendor> DistributionVendors(int distributionId)
        {
            return _contextProvider.Context.VoucherTransactionRecords
                .Where(v => v.Voucher.DistributionId == distributionId && v.VendorId != null)
                .Select(v=>v.Vendor)
                .Distinct();
        }


        [HttpPost]
        public SaveResult SaveChanges(JObject saveBundle)
        {
            return _contextProvider.SaveChanges(saveBundle);
        }
    }
}