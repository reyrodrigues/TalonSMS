using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using TalonAdmin.Models.Admin;

namespace TalonAdmin.Extensions
{
    public static class ControllerExtensions
    {
        public static int GetCountryId(this ApiController controller)
        {
            var headers = controller.Request.Headers.AsEnumerable().ToDictionary(k => k.Key, v => v.Value);
            int countryId = Convert.ToInt32(headers.ContainsKey("X-Tenant-Country") ? headers["X-Tenant-Country"].First() : "0");

            return countryId;
        }

        public static int GetOrganizationId(this ApiController controller)
        {
            var headers = controller.Request.Headers.AsEnumerable().ToDictionary(k => k.Key, v => v.Value);
            int organizationId = Convert.ToInt32(headers.ContainsKey("X-Tenant-Organization") ? headers["X-Tenant-Organization"].First() : "0");

            return organizationId;
        }

        public static async Task<Models.Admin.ApplicationUser> WhoAmI(this ApiController self) {
            var UserManager = self.Request.GetOwinContext().GetUserManager<ApplicationUserManager>();
            var RoleManager = self.Request.GetOwinContext().GetUserManager<ApplicationRoleManager>();

            using (var admin = new Models.Admin.AdminContext())
            {
                admin.Configuration.ProxyCreationEnabled = false;
                admin.Configuration.LazyLoadingEnabled = false;

                string userId = self.User.Identity.GetUserId();
                if (admin.Users.Where(u => u.Id == userId).Any())
                {
                    var user = (await admin.Users
                        .Include("Countries")
                        .Include("Roles")
                        .Include("Countries.Country")
                        .Include("Organization")
                        .Where(u => u.Id == userId)
                        .ToListAsync()).First();

                    return user;
                }

                return null;
            }
        }

        public static IQueryable<T> FilterCountry<T>(this IQueryable<T> queryable, ApiController controller)
            where T : Models.Vouchers.CountryEntity
        {
            var tenantCountry = controller.GetCountryId();

            return queryable.Where(t => t.CountryId == tenantCountry);
        }

        public static IQueryable<T> FilterOrganization<T>(this IQueryable<T> queryable, ApiController controller)
            where T : Models.Vouchers.TenantEntity
        {
            var tenantOrganization = controller.GetOrganizationId();

            return queryable.Where(t => t.OrganizationId == tenantOrganization);
        }

        public static IQueryable<ApplicationUser> FilterOrganization(this IQueryable<ApplicationUser> queryable, ApiController controller)
        {
            var tenantOrganization = controller.GetOrganizationId();

            return queryable.Where(t => t.OrganizationId == tenantOrganization);
        }

        public static IQueryable<ApplicationUserCountry> FilterCountry(this IQueryable<ApplicationUserCountry> queryable, ApiController controller)
        {
            var tenantCountry = controller.GetCountryId();

            return queryable.Where(t => t.CountryId == tenantCountry);
        }

        public static IQueryable<OrganizationCountry> FilterOrganization(this IQueryable<OrganizationCountry> queryable, ApiController controller)
        {
            var tenantOrganization = controller.GetOrganizationId();

            return queryable.Where(t => t.OrganizationId == tenantOrganization);
        }

    }
}