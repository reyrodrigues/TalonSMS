using System;
using System.Collections.Generic;
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
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IConfiguration _configuration;
        private readonly ILogger<DatabaseInitializer> _logger;

        public DatabaseInitializer(
            AdminContext adminContext,
            Models.Vouchers.Context voucherContext,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
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

            await _adminContext.Database.EnsureCreatedAsync();
            await _voucherContext.Database.EnsureCreatedAsync();

            await SeedRolesAsync();
            var organization = await SeedOrganizationAsync();
            await SeedCountryAsync(organization);
            await SeedAdministratorAsync(organization);
            await SeedMenuAsync();
            await SeedActionsAsync();

            if (_configuration.GetValue("Database:SeedDemoData", true))
            {
                await SeedDemoDataAsync(organization);
            }

            _logger.LogInformation("Local database ready.");
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
                    await _roleManager.CreateAsync(new IdentityRole(roleName));
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

            // Distributing vouchers copies the programme's categories onto the new distribution, so
            // without one here "Distribute Vouchers" would succeed but hand out nothing.
            _voucherContext.ProgramVoucherCategories.Add(new Models.Vouchers.ProgramVoucherCategory
            {
                ProgramId = program.Id,
                TypeId = voucherType.Id,
                VendorTypeId = vendorType.Id,
                NumberOfVouchers = beneficiaries.Length,
                Value = 25m,
                CountryId = tenant.CountryId,
                OrganizationId = tenant.OrganizationId
            });
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

        /// <summary>
        /// The client gates every create/edit/delete button and several toolbar operations on
        /// Action rows granted to the signed-in user's roles ($rootScope.canI). With none present
        /// the UI is read-only and the create states bounce straight back to their list, so the
        /// full set is granted to System Administrator here.
        /// </summary>
        private async Task SeedActionsAsync()
        {
            if (await _adminContext.Actions.AnyAsync())
            {
                return;
            }

            var collections = new[]
            {
                "Actions", "Beneficiaries", "BeneficiaryGroups", "Countries", "Distributions",
                "Locations", "MenuCategories", "MenuItems", "OrganizationCountries",
                "OrganizationUsers", "Organizations", "Programs", "Roles", "TransactionLogItems",
                "Users", "VendorTypes", "Vendors", "VoucherTypes"
            };

            var names = new List<string>();
            foreach (var collection in collections)
            {
                names.Add("Create " + collection);
                names.Add("Edit " + collection);
                names.Add("Delete " + collection);
            }

            names.AddRange(new[]
            {
                "Add Sales Person", "Cancel Vouchers", "Disable Beneficiaries", "Distribute Vouchers",
                "Export Beneficiaries", "Export CardLoads", "Generate Test Sheet",
                "Import Beneficiaries", "Import Vendor Data", "Process Quarantined Transactions"
            });

            var actions = names.Select(n => new Models.Admin.Action { Name = n }).ToArray();
            _adminContext.Actions.AddRange(actions);
            await _adminContext.SaveChangesAsync();

            var systemAdministrator = await _roleManager.FindByNameAsync(SystemAdministratorRole);
            if (systemAdministrator == null)
            {
                return;
            }

            _adminContext.ActionRoles.AddRange(
                actions.Select(a => new ActionRole { ActionId = a.Id, RoleId = systemAdministrator.Id }));
            await _adminContext.SaveChangesAsync();

            _logger.LogInformation("Granted {Count} actions to '{Role}'.", actions.Length, SystemAdministratorRole);
        }

        /// <summary>
        /// The sidebar is data-driven: categories hold expandable items, and each item holds the
        /// child links that carry the ui-router state name. Dashboard is hard-coded in nav.tpl.html,
        /// so it is deliberately not repeated here.
        /// </summary>
        private async Task SeedMenuAsync()
        {
            if (await _adminContext.MenuCategories.AnyAsync())
            {
                return;
            }

            var menu = new[]
            {
                new MenuSeed("Programme", 1, true, null, new[]
                {
                    new MenuGroupSeed("Beneficiaries", "fa fa-users", new[]
                    {
                        new MenuLinkSeed("Beneficiaries", "beneficiaries.list"),
                        new MenuLinkSeed("Cycles", "groups.list")
                    }),
                    new MenuGroupSeed("Distribution", "fa fa-share-alt", new[]
                    {
                        new MenuLinkSeed("Programs", "programs.list"),
                        new MenuLinkSeed("Distributions", "distributions.list")
                    }),
                    new MenuGroupSeed("Vendors", "fa fa-shopping-cart", new[]
                    {
                        new MenuLinkSeed("Vendors", "vendors.list"),
                        new MenuLinkSeed("Vendor Types", "vendor-types.list")
                    })
                }),
                new MenuSeed("Reporting", 2, true, null, new[]
                {
                    new MenuGroupSeed("Reports", "fa fa-bar-chart", new[]
                    {
                        new MenuLinkSeed("Distribution Report", "reporting.distribution"),
                        new MenuLinkSeed("Program Closure Report", "reporting.program-closure"),
                        new MenuLinkSeed("Vendor Financial Report", "reporting.vendor-financial")
                    }),
                    new MenuGroupSeed("Reconciliation", "fa fa-check-square-o", new[]
                    {
                        new MenuLinkSeed("Report History", "reconciliation.report-history"),
                        new MenuLinkSeed("Vendor Receipt", "reconciliation.vendor-receipt")
                    })
                }),
                new MenuSeed("Country Administration", 3, true,
                    new[] { CountryAdministratorRole, OrganizationAdministratorRole, SystemAdministratorRole }, new[]
                {
                    new MenuGroupSeed("Transactions", "fa fa-exchange", new[]
                    {
                        new MenuLinkSeed("Transaction Log", "country-admin.transaction.transaction-log"),
                        new MenuLinkSeed("Quarantine", "country-admin.transaction.quarantine"),
                        new MenuLinkSeed("Card Load Log", "country-admin.transaction.card-load-log")
                    }),
                    new MenuGroupSeed("Reference Data", "fa fa-map-marker", new[]
                    {
                        new MenuLinkSeed("Locations", "country-admin.locations.list"),
                        new MenuLinkSeed("Voucher Types", "country-admin.voucher-types.list")
                    }),
                    new MenuGroupSeed("My Account", "fa fa-user", new[]
                    {
                        new MenuLinkSeed("My Profile", "country-admin.users.my-profile")
                    })
                }),
                new MenuSeed("Organization Administration", 4, false,
                    new[] { OrganizationAdministratorRole, SystemAdministratorRole }, new[]
                {
                    new MenuGroupSeed("Organization", "fa fa-building", new[]
                    {
                        new MenuLinkSeed("Users", "org-admin.users.list"),
                        new MenuLinkSeed("Country Settings", "org-admin.country-settings.list")
                    })
                }),
                new MenuSeed("System Administration", 5, false,
                    new[] { SystemAdministratorRole }, new[]
                {
                    new MenuGroupSeed("Access", "fa fa-lock", new[]
                    {
                        new MenuLinkSeed("Users", "sys-admin.users.list"),
                        new MenuLinkSeed("Roles", "sys-admin.roles.list"),
                        new MenuLinkSeed("Actions", "sys-admin.actions.list")
                    }),
                    new MenuGroupSeed("Reference Data", "fa fa-globe", new[]
                    {
                        new MenuLinkSeed("Organizations", "sys-admin.organizations.list"),
                        new MenuLinkSeed("Countries", "sys-admin.countries.list")
                    }),
                    new MenuGroupSeed("Navigation", "fa fa-bars", new[]
                    {
                        new MenuLinkSeed("Menu Categories", "sys-admin.menu-categories.list"),
                        new MenuLinkSeed("Menu Items", "sys-admin.menu-items.list")
                    })
                })
            };

            foreach (var categorySeed in menu)
            {
                var category = new MenuCategory
                {
                    Name = categorySeed.Name,
                    SortOrder = categorySeed.SortOrder,
                    NeedsCountry = categorySeed.NeedsCountry
                };
                _adminContext.MenuCategories.Add(category);
                await _adminContext.SaveChangesAsync();

                if (categorySeed.Roles != null)
                {
                    foreach (var roleName in categorySeed.Roles)
                    {
                        var role = await _roleManager.FindByNameAsync(roleName);
                        if (role != null)
                        {
                            _adminContext.MenuCategoryRoles.Add(new MenuCategoryRole
                            {
                                CategoryId = category.Id,
                                RoleId = role.Id
                            });
                        }
                    }

                    await _adminContext.SaveChangesAsync();
                }

                foreach (var groupSeed in categorySeed.Groups)
                {
                    // Top-level items belong to the category; their children carry only ParentId,
                    // so MenuCategory.Items stays the list of expandable groups.
                    var group = new MenuItem
                    {
                        CategoryId = category.Id,
                        Title = groupSeed.Title,
                        CssClass = groupSeed.CssClass,
                        State = groupSeed.Links.First().State
                    };
                    _adminContext.MenuItems.Add(group);
                    await _adminContext.SaveChangesAsync();

                    _adminContext.MenuItems.AddRange(groupSeed.Links.Select(l => new MenuItem
                    {
                        ParentId = group.Id,
                        Title = l.Title,
                        State = l.State
                    }));
                    await _adminContext.SaveChangesAsync();
                }
            }

            _logger.LogInformation(
                "Seeded {Categories} menu categories with {Links} links.",
                menu.Length, menu.Sum(c => c.Groups.Sum(g => g.Links.Length)));
        }

        private sealed record MenuSeed(string Name, int SortOrder, bool NeedsCountry, string[] Roles, MenuGroupSeed[] Groups);

        private sealed record MenuGroupSeed(string Title, string CssClass, MenuLinkSeed[] Links);

        private sealed record MenuLinkSeed(string Title, string State);

    }
}
