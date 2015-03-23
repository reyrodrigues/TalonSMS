using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Http;

namespace EmergencyVoucherManagement.Extensions
{
    public static class IQueryableExtensions
    {
        public static IQueryable<T> FilterCountry<T>(this IQueryable<T> queryable, ApiController controller)
            where T : Models.Vouchers.CountryEntity
        {

            int tenantCountry = Convert.ToInt32(controller.Request.Headers.GetValues("X-Tenant-Country").First() ?? "0");
            return queryable.Where(t => t.CountryId == tenantCountry);
        }

        public static IQueryable<T> FilterOrganization<T>(this IQueryable<T> queryable, ApiController controller)
            where T : Models.Vouchers.TenantEntity
        {
            int tenantOrganization = Convert.ToInt32(controller.Request.Headers.GetValues("X-Tenant-Organization").First() ?? "0");
            return queryable.Where(t => t.CountryId == tenantOrganization);
        }
    }
}