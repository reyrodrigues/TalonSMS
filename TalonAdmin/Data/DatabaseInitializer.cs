using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TalonAdmin.Models.Admin;

namespace TalonAdmin.Data
{
    /// <summary>
    /// Creates the schema and a minimal set of reference rows so the application can be run
    /// locally without restoring a production database. The EF6 migrations that used to live in
    /// Migrations/ referenced a namespace the project stopped using years ago and could not be
    /// applied, so they were not carried forward.
    /// </summary>
    public class DatabaseInitializer
    {
        public const string SystemAdministratorRole = "System Administrator";
        public const string OrganizationAdministratorRole = "Organization Administrator";
        public const string CountryAdministratorRole = "Country Administrator";

        private readonly AdminContext _adminContext;
        private readonly Models.Vouchers.Context _voucherContext;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<ApplicationRole> _roleManager;
        private readonly IConfiguration _configuration;
        private readonly ILogger<DatabaseInitializer> _logger;

        public DatabaseInitializer(
            AdminContext adminContext,
            Models.Vouchers.Context voucherContext,
            UserManager<ApplicationUser> userManager,
            RoleManager<ApplicationRole> roleManager,
            IConfiguration configuration,
            ILogger<DatabaseInitializer> logger)
        {
            _adminContext = adminContext;
            _voucherContext = voucherContext;
            _userManager = userManager;
            _roleManager = roleManager;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
            if (!_configuration.GetValue("Database:CreateAndSeedOnStartup", false))
            {
                return;
            }

            EnsureDataDirectoryExists();

            await _adminContext.Database.EnsureCreatedAsync();
            await _voucherContext.Database.EnsureCreatedAsync();

            await SeedRolesAsync();
            var organization = await SeedOrganizationAsync();
            await SeedCountryAsync(organization);
            await SeedAdministratorAsync(organization);
            await SeedMenuAsync();

            if (_configuration.GetValue("Database:SeedDemoData", true))
            {
                await SeedDemoDataAsync(organization);
            }

            _logger.LogInformation("Local database ready.");
        }

        private void EnsureDataDirectoryExists()
        {
            foreach (var name in new[] { "AdminContext", "VoucherContext" })
            {
                var connectionString = _configuration.GetConnectionString(name);
                if (string.IsNullOrEmpty(connectionString) || !connectionString.Contains("Data Source="))
                {
                    continue;
                }

                var path = connectionString.Split(new[] { "Data Source=" }, StringSplitOptions.None)
                    .Last()
                    .Split(';')
                    .First()
                    .Trim();

                var directory = Path.GetDirectoryName(Path.GetFullPath(path));
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }
            }
        }

        private async Task SeedRolesAsync()
        {
            foreach (var roleName in new[]
                     {
                         SystemAdministratorRole,
                         OrganizationAdministratorRole,
                         CountryAdministratorRole,
                         "Reporting"
                     })
            {
                if (!await _roleManager.RoleExistsAsync(roleName))
                {
                    await _roleManager.CreateAsync(new ApplicationRole(roleName));
                }
            }
        }

        private async Task<Organization> SeedOrganizationAsync()
        {
            var organization = await _adminContext.Organizations.FirstOrDefaultAsync();
            if (organization != null)
            {
                return organization;
            }

            organization = new Organization
            {
                Name = "International Rescue Committee",
                Abbreviation = "IRC"
            };

            _adminContext.Organizations.Add(organization);
            await _adminContext.SaveChangesAsync();

            return organization;
        }

        private async Task SeedCountryAsync(Organization organization)
        {
            if (await _adminContext.Countries.AnyAsync())
            {
                return;
            }

            var settings = new CountrySettings
            {
                SmsBackendType = (int)Models.Admin.SmsBackendType.RescueSMS
            };
            _adminContext.CountrySettings.Add(settings);
            await _adminContext.SaveChangesAsync();

            var country = new Country
            {
                Name = "Local Development",
                IsoAlpha2 = "ZZ",
                IsoAlpha3 = "ZZZ",
                CurrencyIsoCode = "USD",
                CurrencyUnicodeSymbol = "$",
                CountryCallingCode = "1",
                LanguageCode = "en",
                SettingsId = settings.Id
            };

            _adminContext.Countries.Add(country);
            await _adminContext.SaveChangesAsync();

            _adminContext.OrganizationCountries.Add(new OrganizationCountry
            {
                OrganizationId = organization.Id,
                CountryId = country.Id,
                SettingsId = settings.Id
            });
            await _adminContext.SaveChangesAsync();
        }

        private async Task SeedAdministratorAsync(Organization organization)
        {
            const string userName = "admin";

            var existing = await _userManager.FindByNameAsync(userName);
            if (existing != null)
            {
                return;
            }

            var password = _configuration["Database:SeedAdminPassword"] ?? "Talon!23";
            var country = await _adminContext.Countries.FirstAsync();

            var user = new ApplicationUser
            {
                UserName = userName,
                Email = "admin@localhost",
                FullName = "Local Administrator",
                OrganizationId = organization.Id,
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                _logger.LogError(
                    "Could not create the seed administrator: {Errors}",
                    string.Join("; ", result.Errors.Select(e => e.Description)));
                return;
            }

            await _userManager.AddToRoleAsync(user, SystemAdministratorRole);
            await _userManager.AddToRoleAsync(user, OrganizationAdministratorRole);

            _adminContext.ApplicationUserCountries.Add(new ApplicationUserCountry
            {
                ApplicationUserId = user.Id,
                CountryId = country.Id
            });
            await _adminContext.SaveChangesAsync();

            _logger.LogWarning(
                "Seeded local administrator '{UserName}' with the development password. Change it before exposing this instance.",
                userName);
        }

        /// <summary>
        /// A single small distribution with vouchers issued and partly redeemed. Enough for the
        /// dashboard, the Breeze queries and every report to return real rows instead of failing on
        /// the First() calls they inherited from the EF6 code.
        /// </summary>
        private async Task SeedDemoDataAsync(Organization organization)
        {
            if (await _voucherContext.Programs.AnyAsync())
            {
                return;
            }

            var country = await _adminContext.Countries.FirstAsync();
            var tenant = new { CountryId = country.Id, OrganizationId = organization.Id };

            var location = new Models.Vouchers.Location { Name = "Central District", CountryId = tenant.CountryId };
            var vendorType = new Models.Vouchers.VendorType { Name = "General Store", CountryId = tenant.CountryId };
            var voucherType = new Models.Vouchers.VoucherType
            {
                Name = "Food",
                CountryId = tenant.CountryId,
                OrganizationId = tenant.OrganizationId
            };
            _voucherContext.AddRange(location, vendorType, voucherType);
            await _voucherContext.SaveChangesAsync();

            var vendor = new Models.Vouchers.Vendor
            {
                Name = "Demo Vendor",
                FirstName = "Dana",
                LastName = "Vendor",
                MobileNumber = "+15550000001",
                UserName = "vendor",
                CountryId = tenant.CountryId,
                LocationId = location.Id,
                TypeId = vendorType.Id
            };
            vendor.SetPassword("Talon!23");
            _voucherContext.Vendors.Add(vendor);

            var group = new Models.Vouchers.BeneficiaryGroup
            {
                Name = "Demo Group",
                CountryId = tenant.CountryId,
                OrganizationId = tenant.OrganizationId
            };
            _voucherContext.BeneficiaryGroups.Add(group);
            await _voucherContext.SaveChangesAsync();

            var beneficiaries = Enumerable.Range(1, 5).Select(i => new Models.Vouchers.Beneficiary
            {
                FirstName = "Beneficiary",
                LastName = i.ToString("D2"),
                NationalId = string.Format("NID{0:D6}", i),
                MobileNumber = string.Format("+1555000{0:D4}", i),
                BirthYear = (1980 + i).ToString(),
                Sex = i % 2 == 0 ? 1 : 2,
                PIN = string.Format("{0:D4}", 1000 + i),
                GroupId = group.Id,
                LocationId = location.Id,
                CountryId = tenant.CountryId,
                OrganizationId = tenant.OrganizationId
            }).ToArray();
            _voucherContext.Beneficiaries.AddRange(beneficiaries);

            var program = new Models.Vouchers.Program
            {
                Name = "Demo Cash Programme",
                FundCodes = "DEMO-001",
                DistributionMechanism = 1,
                VoucherCodeLength = 6,
                ShowInDashboard = true,
                CreatedBy = "seed",
                ModifiedBy = "seed",
                CreatedOn = DateTime.UtcNow,
                ModifiedOn = DateTime.UtcNow,
                CountryId = tenant.CountryId,
                OrganizationId = tenant.OrganizationId
            };
            _voucherContext.Programs.Add(program);
            await _voucherContext.SaveChangesAsync();

            var distribution = new Models.Vouchers.Distribution
            {
                Number = 1,
                Title = "Demo Distribution",
                FundCodes = "DEMO-001",
                ProgramId = program.Id,
                LocationId = location.Id,
                GroupId = group.Id,
                VoucherCodeLength = 6,
                CreatedBy = "seed",
                ModifiedBy = "seed",
                CountryId = tenant.CountryId,
                OrganizationId = tenant.OrganizationId
            };
            _voucherContext.Distributions.Add(distribution);
            await _voucherContext.SaveChangesAsync();

            var category = new Models.Vouchers.DistributionVoucherCategory
            {
                TypeId = voucherType.Id,
                DistributionId = distribution.Id,
                VendorTypeId = vendorType.Id,
                NumberOfVouchers = beneficiaries.Length,
                IssuedVouchers = beneficiaries.Length,
                Value = 25m,
                ValidAfter = DateTime.UtcNow.AddDays(-7),
                CountryId = tenant.CountryId,
                OrganizationId = tenant.OrganizationId
            };
            _voucherContext.DistributionVoucherCategories.Add(category);
            await _voucherContext.SaveChangesAsync();

            for (var i = 0; i < beneficiaries.Length; i++)
            {
                var redeemed = i < 3;

                var voucher = new Models.Vouchers.Voucher
                {
                    CategoryId = category.Id,
                    DistributionId = distribution.Id,
                    VoucherCode = string.Format("{0:D6}", 100000 + i),
                    SequentialCode = (i + 1).ToString(),
                    Status = redeemed ? 3 : 1,
                    Value = 25m,
                    CountryId = tenant.CountryId,
                    OrganizationId = tenant.OrganizationId
                };
                _voucherContext.Vouchers.Add(voucher);
                await _voucherContext.SaveChangesAsync();

                // Type 1 = issued to a beneficiary.
                _voucherContext.VoucherTransactionRecords.Add(new Models.Vouchers.VoucherTransactionRecord
                {
                    BeneficiaryId = beneficiaries[i].Id,
                    VoucherId = voucher.Id,
                    Type = 1,
                    Value = 25m,
                    CreatedOn = DateTime.UtcNow.AddDays(-5),
                    LastModifiedOn = DateTime.UtcNow.AddDays(-5),
                    ConfirmationCode = string.Format("{0:D6}", 200000 + i),
                    CountryId = tenant.CountryId,
                    OrganizationId = tenant.OrganizationId
                });

                if (redeemed)
                {
                    // Type 2 = redeemed at a vendor.
                    _voucherContext.VoucherTransactionRecords.Add(new Models.Vouchers.VoucherTransactionRecord
                    {
                        BeneficiaryId = beneficiaries[i].Id,
                        VoucherId = voucher.Id,
                        VendorId = vendor.Id,
                        Type = 2,
                        Value = 25m,
                        CreatedOn = DateTime.UtcNow.AddDays(-2),
                        LastModifiedOn = DateTime.UtcNow.AddDays(-2),
                        CountryId = tenant.CountryId,
                        OrganizationId = tenant.OrganizationId
                    });
                }

                await _voucherContext.SaveChangesAsync();
            }

            _logger.LogInformation(
                "Seeded demo programme '{Programme}' with {Count} beneficiaries and vouchers.",
                program.Name, beneficiaries.Length);
        }

        private async Task SeedMenuAsync()
        {
            if (await _adminContext.MenuCategories.AnyAsync())
            {
                return;
            }

            var dashboard = new MenuCategory { Name = "Dashboard", SortOrder = 1, NeedsCountry = true };
            _adminContext.MenuCategories.Add(dashboard);
            await _adminContext.SaveChangesAsync();

            _adminContext.MenuItems.Add(new MenuItem
            {
                CategoryId = dashboard.Id,
                State = "app.dashboard",
                Title = "Dashboard",
                CssClass = "fa fa-dashboard"
            });
            await _adminContext.SaveChangesAsync();
        }
    }
}
