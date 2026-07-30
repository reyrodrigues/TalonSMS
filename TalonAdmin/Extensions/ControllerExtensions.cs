using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TalonAdmin.Auth;
using TalonAdmin.Models.Admin;

namespace TalonAdmin.Extensions
{
    public static class ControllerExtensions
    {
        public static int GetCountryId(this ControllerBase controller)
        {
            return ReadIntHeader(controller, AuthConstants.CountryHeader);
        }

        public static int GetOrganizationId(this ControllerBase controller)
        {
            return ReadIntHeader(controller, AuthConstants.OrganizationHeader);
        }

        private static int ReadIntHeader(ControllerBase controller, string name)
        {
            var values = controller.Request.Headers[name];
            int parsed;

            return values.Count > 0 && int.TryParse(values[0], out parsed) ? parsed : 0;
        }

        public static string GetUserId(this ControllerBase controller)
        {
            return controller.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        }

        /// <summary>
        /// Loads the signed-in user with the graph the UI needs. The context is passed in rather
        /// than constructed here, so the request's scoped DbContext is reused.
        /// </summary>
        public static async Task<ApplicationUser> WhoAmI(this ControllerBase controller, AdminContext admin)
        {
            var userId = controller.GetUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return null;
            }

            return await admin.Users
                .AsNoTracking()
                .Include(u => u.Countries)
                .ThenInclude(c => c.Country)
                .Include(u => u.Roles)
                .Include(u => u.Organization)
                .FirstOrDefaultAsync(u => u.Id == userId);
        }

        public static IQueryable<T> FilterCountry<T>(this IQueryable<T> queryable, ControllerBase controller)
            where T : Models.Vouchers.CountryEntity
        {
            var tenantCountry = controller.GetCountryId();

            return queryable.Where(t => t.CountryId == tenantCountry);
        }

        public static IQueryable<T> FilterOrganization<T>(this IQueryable<T> queryable, ControllerBase controller)
            where T : Models.Vouchers.TenantEntity
        {
            var tenantOrganization = controller.GetOrganizationId();

            return queryable.Where(t => t.OrganizationId == tenantOrganization);
        }

        public static IQueryable<ApplicationUser> FilterOrganization(this IQueryable<ApplicationUser> queryable, ControllerBase controller)
        {
            var tenantOrganization = controller.GetOrganizationId();

            return queryable.Where(t => t.OrganizationId == tenantOrganization);
        }

        public static IQueryable<ApplicationUserCountry> FilterCountry(this IQueryable<ApplicationUserCountry> queryable, ControllerBase controller)
        {
            var tenantCountry = controller.GetCountryId();

            return queryable.Where(t => t.CountryId == tenantCountry);
        }

        public static IQueryable<OrganizationCountry> FilterOrganization(this IQueryable<OrganizationCountry> queryable, ControllerBase controller)
        {
            var tenantOrganization = controller.GetOrganizationId();

            return queryable.Where(t => t.OrganizationId == tenantOrganization);
        }
    }
}
