using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Http;
using TalonAdmin.Models.Admin;

namespace TalonAdmin.Extensions
{
    public static class IQueryableExtensions
    {
        public static IQueryable<T> FilterCountry<T>(this IQueryable<T> queryable, ApiController controller)
            where T : Models.Vouchers.CountryEntity
        {
            var headers = controller.Request.Headers.AsEnumerable().ToDictionary(k => k.Key, v => v.Value);
            int tenantCountry = Convert.ToInt32(headers.ContainsKey("X-Tenant-Country") ? headers["X-Tenant-Country"].First() : "0");


            return queryable.Where(t => t.CountryId == tenantCountry);
        }

        public static IQueryable<T> FilterOrganization<T>(this IQueryable<T> queryable, ApiController controller)
            where T : Models.Vouchers.TenantEntity
        {
            var headers = controller.Request.Headers.AsEnumerable().ToDictionary(k => k.Key, v => v.Value);
            int tenantOrganization = Convert.ToInt32(headers.ContainsKey("X-Tenant-Organization") ? headers["X-Tenant-Organization"].First() : "0");

            return queryable.Where(t => t.OrganizationId == tenantOrganization);
        }

        public static IQueryable<ApplicationUser> FilterOrganization(this IQueryable<ApplicationUser> queryable, ApiController controller)
        {
            var headers = controller.Request.Headers.AsEnumerable().ToDictionary(k => k.Key, v => v.Value);
            int tenantOrganization = Convert.ToInt32(headers.ContainsKey("X-Tenant-Organization") ? headers["X-Tenant-Organization"].First() : "0");

            return queryable.Where(t => t.OrganizationId == tenantOrganization);
        }

        public static IQueryable<ApplicationUserCountry> FilterCountry(this IQueryable<ApplicationUserCountry> queryable, ApiController controller)
        {
            var headers = controller.Request.Headers.AsEnumerable().ToDictionary(k => k.Key, v => v.Value);
            int tenantCountry = Convert.ToInt32(headers.ContainsKey("X-Tenant-Country") ? headers["X-Tenant-Country"].First() : "0");


            return queryable.Where(t => t.CountryId == tenantCountry);
        }
        public static IQueryable<OrganizationCountry> FilterOrganization(this IQueryable<OrganizationCountry> queryable, ApiController controller)
        {
            var headers = controller.Request.Headers.AsEnumerable().ToDictionary(k => k.Key, v => v.Value);
            int tenantOrganization = Convert.ToInt32(headers.ContainsKey("X-Tenant-Organization") ? headers["X-Tenant-Organization"].First() : "0");

            return queryable.Where(t => t.OrganizationId == tenantOrganization);
        }

    }
}