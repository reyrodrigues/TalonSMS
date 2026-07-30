namespace TalonAdmin.Auth
{
    public static class AuthConstants
    {
        /// <summary>Policy scheme that routes each request to Basic or Bearer.</summary>
        public const string SmartScheme = "Smart";

        public const string BasicScheme = "Basic";

        /// <summary>Header carrying the tenant country, set by the client or by <see cref="TenantFormMiddleware"/>.</summary>
        public const string CountryHeader = "X-Tenant-Country";

        public const string OrganizationHeader = "X-Tenant-Organization";

        public const string VendorIdHeader = "X-Vendor-Id";
    }
}
