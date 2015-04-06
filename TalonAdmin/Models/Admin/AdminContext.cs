using Microsoft.AspNet.Identity.EntityFramework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.Core.Objects;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json.Linq;

namespace TalonAdmin.Models.Admin
{
    public class AdminContext : IdentityDbContext<ApplicationUser>
    {
        public AdminContext()
            : base("name=AdminContext")
        {
            ((IObjectContextAdapter)this).ObjectContext.ObjectMaterialized += (sender, e) => ApplyUTC(e.Entity);

        }

        private static void ApplyUTC(object entity)
        {
            if (entity == null)
                return;

            var properties = entity.GetType().GetProperties()
                .Where(x => x.PropertyType == typeof(DateTime) || x.PropertyType == typeof(DateTime?));

            foreach (var property in properties)
            {

                var dt = property.PropertyType == typeof(DateTime?)
                    ? (DateTime?)property.GetValue(entity, null)
                    : (DateTime)property.GetValue(entity, null);

                if (dt == null)
                    continue;

                property.SetValue(entity, DateTime.SpecifyKind(dt.Value, DateTimeKind.Utc), null);
            }
        }

        public static AdminContext Create()
        {
            return new AdminContext();
        }

        public DbSet<Country> Countries { get; set; }
        public DbSet<MenuItem> MenuItems { get; set; }
        public DbSet<MenuCategoryRole> MenuCategoryRoles { get; set; }
        public DbSet<MenuCategory> MenuCategories { get; set; }
        public DbSet<Organization> Organizations { get; set; }
        public DbSet<OrganizationCountry> OrganizationCountries { get; set; }
        public DbSet<ApplicationUserCountry> ApplicationUserCountries { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<ApplicationUserCountry>()
                .HasRequired(p => p.ApplicationUser)
                .WithMany(p => p.Countries);

            modelBuilder.Entity<MenuCategory>()
                .HasMany(p => p.Items)
                .WithOptional(p => p.Category);

            modelBuilder.Entity<MenuCategory>()
                .HasMany(p => p.Roles)
                .WithRequired(p => p.Category);

            modelBuilder.Entity<MenuItem>()
                .HasMany(p => p.Children)
                .WithOptional(p => p.Parent);
    

#if DEBUG
            Database.SetInitializer(new CleanDbInitializer());
#endif
        }
    }

    public class CleanDbInitializer : DropCreateDatabaseIfModelChanges<AdminContext>
    {
        protected override void Seed(AdminContext context)
        {
            #region Countries and Sys Admin
            string ircsLogo = "";
            if (HttpContext.Current != null)
            {
                ircsLogo = "data:image/svg+xml;base64," + Convert.ToBase64String(System.IO.File.ReadAllBytes(HttpContext.Current.Server.MapPath("~/Content/IRCLogo.svg")));
            }
            var irc = new Organization
            {
                Abbreviation = "IRC",
                Name = "The International Rescue Committee",
                LogoSVG = ircsLogo
            };

            var ukraine = new Country
            {
                Name = "Ukraine",
                IsoAlpha2 = "UA",
                IsoAlpha3 = "UKR",
                CurrencyIsoCode = "UAH",
                CurrencyUnicodeSymbol = "₴",
                Settings = new CountrySettings()
            };


            var systemAdmin = new ApplicationUser
            {
                UserName = "reynaldor",
                FullName = "Rey Rodrigues",
                Email = "reynaldo.rodrigues@rescue.org",
                Organization = irc
            };

            var uaMembership = new ApplicationUserCountry
            {
                Country = ukraine,
                ApplicationUser = systemAdmin
            };

            var systemAdminRole = new IdentityRole
            {
                Name = "System Administrator"
            };

            var countryAdminRole = new IdentityRole
            {
                Name = "Country Administrator"
            };

            var orgAdminRole = new IdentityRole
            {
                Name = "Organization Administrator"
            };

            var progManager = new IdentityRole
            {
                Name = "Program Manager"
            };

            context.Roles.Add(systemAdminRole);
            context.Roles.Add(countryAdminRole);
            context.Roles.Add(orgAdminRole);
            context.Roles.Add(progManager);


            context.Users.Add(systemAdmin);

            context.Organizations.Add(irc);

            context.Countries.Add(ukraine);

            context.OrganizationCountries.Add(new  OrganizationCountry{ 
                Country = ukraine,
                Organization = irc,
                Settings = new CountrySettings()
            });

            context.ApplicationUserCountries.Add(uaMembership);
            #endregion

            #region Menu Items

            var categories = new MenuCategory[] {
                new MenuCategory { 
                    SortOrder = 1,
                    Name = "Beneficiary",
                    NeedsCountry = true,
                    Items = new MenuItem[] { 
                        new MenuItem {
                            State = "beneficiaries",
                            Title = "Enrollment",
                            CssClass = "icon-user icon text-success-lter",
                            Children = new MenuItem[] {
                                new MenuItem {
                                    State = "beneficiaries.list",
                                    Title = "List",
                                },
                                new MenuItem {
                                    State = "beneficiaries.create",
                                    Title = "Create",
                                },
                            }
                        }, 
                        new MenuItem {
                            State = "groups",
                            Title = "Groups",
                            CssClass = "icon-users icon text-success-lter",
                            Children = new MenuItem[] {
                                new MenuItem {
                                    State = "groups.list",
                                    Title = "List",
                                },
                                new MenuItem {
                                    State = "groups.create",
                                    Title = "Create",
                                },
                            }
                        },
                    }
                },
                new MenuCategory { 
                    SortOrder = 2,
                    Name = "Vendors",
                    NeedsCountry = true,
                    Items = new MenuItem[] { 
                        new MenuItem {
                            State = "vendors",
                            Title = "Enrollment",
                            CssClass = "fa fa-building-o text-info-lter",
                            Children = new MenuItem[] {
                                new MenuItem {
                                    State = "vendors.list",
                                    Title = "List",
                                },
                                new MenuItem {
                                    State = "vendors.create",
                                    Title = "Create",
                                },
                            }
                        }, 
                        new MenuItem {
                            State = "vendor-types",
                            Title = "Types",
                            CssClass = "fa fa-tags text-info-lter",
                            Children = new MenuItem[] {
                                new MenuItem {
                                    State = "vendor-types.list",
                                    Title = "List",
                                },
                                new MenuItem {
                                    State = "vendor-types.create",
                                    Title = "Create",
                                },
                            }
                        },
                    }
                },
                
                new MenuCategory { 
                    SortOrder = 3,
                    Name = "Vouchers",
                    NeedsCountry = true,
                    Roles = new MenuCategoryRole[] {
                        new MenuCategoryRole { Role = systemAdminRole },
                        new MenuCategoryRole { Role = orgAdminRole },
                        new MenuCategoryRole { Role = progManager }
                    },
                    Items = new MenuItem[] { 
                        new MenuItem {
                            State = "distributions",
                            Title = "Distributions",
                            CssClass = "fa fa-truck text-primary-lter",
                            Children = new MenuItem[] {
                                new MenuItem {
                                    State = "distributions.list",
                                    Title = "List",
                                },
                                new MenuItem {
                                    State = "distributions.create",
                                    Title = "Register",
                                },
                            }
                        }, 
                    }
                },
                new MenuCategory { 
                    SortOrder = 4,
                    Name = "Reporting",
                    NeedsCountry = true,
                    Roles = new MenuCategoryRole[] {
                        new MenuCategoryRole { Role = systemAdminRole },
                        new MenuCategoryRole { Role = orgAdminRole },
                        new MenuCategoryRole { Role = progManager }
                    },
                    Items = new MenuItem[] { 
                        new MenuItem {
                            State = "reporting",
                            Title = "Reports",
                            CssClass = "icon-bar-chart icon text-success-dker",
                            Children = new MenuItem[] {
                                new MenuItem {
                                    State = "reporting.distribution",
                                    Title = "Distribution Report",
                                },
                                new MenuItem {
                                    State = "reporting.vendor-receipt",
                                    Title = "Vendor Receipt Report",
                                },
                            }
                        }, 
                    }
                },
                new MenuCategory { 
                    SortOrder = 5,
                    Name = "Country Administration",
                    Roles = new MenuCategoryRole[] {
                        new MenuCategoryRole { Role = systemAdminRole },
                        new MenuCategoryRole { Role = orgAdminRole },
                        new MenuCategoryRole { Role = countryAdminRole }
                    },
                    Items = new MenuItem[] { 
                        new MenuItem {
                            State = "country-admin.voucher-types",
                            Title = "Voucher Types",
                            CssClass = "fa fa-tags text-info-dker",
                            Children = new MenuItem[] {
                                new MenuItem {
                                    State = "country-admin.voucher-types.list",
                                    Title = "List",
                                },
                                new MenuItem {
                                    State = "country-admin.voucher-types.create",
                                    Title = "Create",
                                },
                            }
                        }, 
                        new MenuItem {
                            State = "country-admin.locations",
                            Title = "Locations",
                            CssClass = "icon icon-pin text-info-dker",
                            Children = new MenuItem[] {
                                new MenuItem {
                                    State = "country-admin.locations.list",
                                    Title = "List",
                                },
                            }
                        }, 
                        new MenuItem {
                            State = "country-admin.users",
                            Title = "Registered Users",
                            CssClass = "icon-user icon text-info-dker",
                            Children = new MenuItem[] {
                                new MenuItem {
                                    State = "country-admin.users.list",
                                    Title = "List",
                                },
                                new MenuItem {
                                    State = "country-admin.users.register",
                                    Title = "Register",
                                },
                            }
                        }, 
                    },
                },
                new MenuCategory { 
                    SortOrder = 6,
                    Name = "Organization Administration",
                    Roles = new MenuCategoryRole[] {
                        new MenuCategoryRole { Role = systemAdminRole },
                        new MenuCategoryRole { Role = orgAdminRole },
                    },
                    Items = new MenuItem[] { 
                        new MenuItem {
                            State = "org-admin.users",
                            Title = "Org Users",
                            CssClass = "icon-user icon text-warning-lter",
                            Children = new MenuItem[] {
                                new MenuItem {
                                    State = "org-admin.users.list",
                                    Title = "List",
                                },
                                new MenuItem {
                                    State = "org-admin.users.register",
                                    Title = "Register",
                                },
                            }
                        }, 
                        new MenuItem {
                            State = "org-admin.countries",
                            Title = "Countries",
                            CssClass = "fa fa-globe text-warning-lter",
                            Children = new MenuItem[] {
                                new MenuItem {
                                    State = "org-admin.countries.list",
                                    Title = "List",
                                },
                                new MenuItem {
                                    State = "org-admin.countries.create",
                                    Title = "Create",
                                },
                            }
                        },
                    },
                },
                new MenuCategory { 
                    SortOrder = 7,
                    Name = "System Administration",
                    Roles = new MenuCategoryRole[] {
                        new MenuCategoryRole { Role = systemAdminRole },
                    },
                    Items = new MenuItem[] { 
                        new MenuItem {
                            State = "system-admin.users",
                            Title = "System Admins",
                            CssClass = "icon-user icon text-danger-lter",
                            Children = new MenuItem[] {
                                new MenuItem {
                                    State = "system-admin.users.list",
                                    Title = "List",
                                },
                                new MenuItem {
                                    State = "system-admin.users.register",
                                    Title = "Register",
                                },
                            }
                        }, 
                        new MenuItem {
                            State = "system-admin.countries",
                            Title = "Countries",
                            CssClass = "fa fa-globe text-danger-lter",
                            Children = new MenuItem[] {
                                new MenuItem {
                                    State = "system-admin.countries.list",
                                    Title = "List",
                                },
                                new MenuItem {
                                    State = "system-admin.countries.create",
                                    Title = "Create",
                                },
                            },
                        },
                        new MenuItem {
                            State = "system-admi.organizations",
                            Title = "Organizations",
                            CssClass = "fa fa-circle text-danger-lter",
                            Children = new MenuItem[] {
                                new MenuItem {
                                    State = "system-admin.organizations.list",
                                    Title = "List",
                                },
                                new MenuItem {
                                    State = "system-admin.organizations.create",
                                    Title = "Create",
                                },
                            },
                        },
                    }
                },

            };

            context.MenuCategories.AddRange(categories);
            #endregion

            context.SaveChanges();


            systemAdmin.Roles.Add(new IdentityUserRole
            {
                UserId = systemAdmin.Id,
                RoleId = systemAdminRole.Id
            });

            base.Seed(context);
        }
    }
}