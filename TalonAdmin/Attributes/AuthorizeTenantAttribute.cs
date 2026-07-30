using Microsoft.AspNetCore.Authorization;

namespace TalonAdmin.Attributes
{
    /// <summary>
    /// Marks an action as requiring an authenticated user acting within a tenant.
    ///
    /// The EF6 version queried the user then returned true unconditionally — the per-tenant
    /// check had been commented out — so it only ever enforced authentication. That behaviour is
    /// preserved here rather than silently tightening access during the migration; the header
    /// values it would validate are available via ControllerExtensions.GetCountryId/GetOrganizationId.
    /// </summary>
    public class AuthorizeTenantAttribute : AuthorizeAttribute
    {
    }
}
