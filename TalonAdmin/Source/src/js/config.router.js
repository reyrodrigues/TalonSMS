'use strict';

/**
 * Config for the router
 */
angular.module('app')
  .run(
    ['$rootScope', '$state', '$stateParams',
      function ($rootScope, $state, $stateParams) {
          $rootScope.$state = $state;
          $rootScope.$stateParams = $stateParams;
      }
    ]
  )
  .config(
    ['$stateProvider', '$urlRouterProvider', 'JQ_CONFIG', 'gettext',
      function ($stateProvider, $urlRouterProvider, JQ_CONFIG, gettext) {
          var defaultResolve = {
          };

          $urlRouterProvider.otherwise('/app/dashboard');
          $stateProvider
                .state('app', {
                    abstract: true,
                    url: '/app',
                    templateUrl: 'tpl/app.html'
                })

                .state('app.dashboard', {
                    url: '/dashboard',
                    templateUrl: 'tpl/app/dashboard.html',
                    controller: 'AppDashboardCtrl',
                    resolve: {
                    }
                })

                .state('access', {
                    url: '/access',
                    template: '<div ui-view class="smooth"></div>'
                })
                .state('access.signin', {
                    url: '/signin',
                    templateUrl: 'tpl/page_signin.html',
                    resolve: {
                    },
                    allowAnonymous: true
                })
                .state('access.forgotpwd', {
                    url: '/forgotpwd',
                    templateUrl: 'tpl/page_forgotpwd.html',
                    allowAnonymous: true
                })

                .state('beneficiaries', {
                    url: '/beneficiaries',
                    abstract: true,
                    templateUrl: 'tpl/app.html',
                    resolve: angular.extend({
                        settings: function () {
                            return {
                                entityType: 'Beneficiary',
                                collectionType: 'Beneficiaries',
                                listState: 'beneficiaries.list',
                                editState: 'beneficiaries.edit',
                                createState: 'beneficiaries.create',
                                title: 'Beneficiaries',
                                formTemplate: 'tpl/beneficiaries/form.html'
                            };
                        }
                    }, defaultResolve)
                })
                .state('beneficiaries.list', {
                    url: '/list',
                    templateUrl: 'tpl/beneficiaries/list.html',
                    controller: 'BeneficiariesListCtrl'
                })

                .state('beneficiaries.edit', {
                    url: '/edit/:id',
                    templateUrl: 'tpl/beneficiaries/edit.html',
                    controller: 'BeneficiariesEditCtrl'
                })

                .state('beneficiaries.bulkEdit', {
                    url: '/bulkEdit',
                    templateUrl: 'tpl/beneficiaries/bulkEdit.html',
                    controller: 'BeneficiariesBulkEditCtrl'
                })

                .state('beneficiaries.create', {
                    url: '/create',
                    templateUrl: 'tpl/beneficiaries/create.html',
                    controller: 'BeneficiariesCreateCtrl'
                })



                .state('groups', {
                    url: '/groups',
                    abstract: true,
                    templateUrl: 'tpl/app.html',
                    resolve: angular.extend({
                        settings: function () {
                            return {
                                entityType: 'BeneficiaryGroup',
                                collectionType: 'BeneficiaryGroups',
                                listState: 'groups.list',
                                editState: 'groups.edit',
                                createState: 'groups.create',
                                title: 'Beneficiary Groups',
                                formTemplate: 'tpl/groups/form.html'
                            };
                        }
                    }, defaultResolve)
                })
                .state('groups.list', {
                    url: '/list',
                    templateUrl: 'tpl/generic/list.html',
                    controller: 'GenericGridCtrl'
                })

                .state('groups.edit', {
                    url: '/edit/:id',
                    templateUrl: 'tpl/groups/edit.html',
                    controller: 'BeneficiaryGroupsEditCtrl'
                })
                .state('groups.create', {
                    url: '/create',
                    templateUrl: 'tpl/generic/create.html',
                    controller: 'GenericCreateCtrl'
                })


                .state('vendors', {
                    url: '/vendors',
                    abstract: true,
                    templateUrl: 'tpl/app.html',
                    resolve: angular.extend({
                        settings: function () {
                            return {
                                entityType: 'Vendor',
                                collectionType: 'vendors',
                                listState: 'vendors.list',
                                editState: 'vendors.edit',
                                createState: 'vendors.create',
                                title: 'Vendors',
                                formTemplate: 'tpl/vendors/form.html'
                            };
                        }
                    }, defaultResolve)
                })
                .state('vendors.list', {
                    url: '/list',
                    templateUrl: 'tpl/vendors/list.html',
                    controller: 'VendorsListCtrl'
                })

                .state('vendors.edit', {
                    url: '/edit/:id',
                    templateUrl: 'tpl/vendors/edit.html',
                    controller: 'VendorsEditCtrl'
                })
                .state('vendors.create', {
                    url: '/create',
                    templateUrl: 'tpl/vendors/create.html',
                    controller: 'VendorsCreateCtrl'
                })

                .state('vendor-types', {
                    url: '/vendor-types',
                    abstract: true,
                    templateUrl: 'tpl/app.html',
                    resolve: angular.extend({
                        settings: function () {
                            return {
                                entityType: 'VendorType',
                                collectionType: 'VendorTypes',
                                listState: 'vendor-types.list',
                                editState: 'vendor-types.edit',
                                createState: 'vendor-types.create',
                                title: 'Vendor Types',
                                formTemplate: 'tpl/country-admin/vendor-types/form.html'
                            };
                        }
                    }, defaultResolve)
                })
                .state('vendor-types.list', {
                    url: '/list',
                    templateUrl: 'tpl/generic/list.html',
                    controller: 'GenericGridCtrl'
                })

                .state('vendor-types.edit', {
                    url: '/edit/:id',
                    templateUrl: 'tpl/generic/edit.html',
                    controller: 'GenericEditCtrl'
                })
                .state('vendor-types.create', {
                    url: '/create',
                    templateUrl: 'tpl/generic/create.html',
                    controller: 'GenericCreateCtrl'
                })


                .state('vouchers', {
                    url: '/vouchers',
                    abstract: true,
                    templateUrl: 'tpl/app.html',
                    resolve: defaultResolve
                })
                .state('vouchers.assign', {
                    url: '/assign',
                    templateUrl: 'tpl/vouchers_assign.html',
                    controller: 'VoucherAssignCtrl'
                })
                .state('vouchers.list', {
                    url: '/list',
                    templateUrl: 'tpl/vouchers_list.html',
                    controller: 'VoucherListCtrl'
                })

                .state('programs', {
                    url: '/programs',
                    templateUrl: 'tpl/app.html',
                    abstract: true,
                    resolve: angular.extend({
                        settings: function () {
                            return {
                                entityType: 'Program',
                                collectionType: 'Programs',
                                listState: 'programs.list',
                                editState: 'programs.edit',
                                createState: 'programs.create',
                                title: 'Programs',
                                formTemplate: 'tpl/programs/form.html'
                            };
                        }
                    }, defaultResolve)
                })
                .state('programs.list', {
                    url: '/list',
                    templateUrl: 'tpl/generic/list.html',
                    controller: 'GenericGridCtrl'
                })

                .state('programs.edit', {
                    url: '/edit/:id',
                    templateUrl: 'tpl/programs/edit.html',
                    controller: 'ProgramsEditCtrl'
                })
                .state('programs.create', {
                    url: '/create',
                    templateUrl: 'tpl/generic/create.html',
                    controller: 'ProgramsCreateCtrl'
                })


                .state('distributions', {
                    url: '/distributions',
                    abstract: true,
                    templateUrl: 'tpl/app.html',
                    resolve: angular.extend({
                        settings: function () {
                            return {
                                entityType: 'Distribution',
                                collectionType: 'Distributions',
                                listState: 'distributions.list',
                                editState: 'distributions.edit',
                                createState: 'distributions.create',
                                title: 'Locations',
                                formTemplate: 'tpl/distributions/form.html'
                            };
                        }
                    }, defaultResolve)
                })

                .state('distributions.list', {
                    url: '/list',
                    templateUrl: 'tpl/distributions/list.html',
                    controller: 'DistributionsListCtrl'
                })
                .state('distributions.edit', {
                    url: '/edit-distribution/:id',
                    templateUrl: 'tpl/distributions/edit.html',
                    controller: 'DistributionsEditCtrl'
                })
                .state('distributions.create', {
                    url: '/create-distribution/',
                    templateUrl: 'tpl/distributions/create.html',
                    controller: 'DistributionsCreateCtrl'
                })


                .state('country-admin', {
                    abstract: true,
                    url: '/country-admin',
                    templateUrl: 'tpl/app.html',
                    resolve: defaultResolve
                })
                .state('country-admin.locations', {
                    url: '/locations',
                    abstract: true,
                    template: '<div ui-view></div>',
                    resolve: angular.extend({
                        settings: function () {
                            return {
                                entityType: 'Location',
                                collectionType: 'Locations',
                                listState: 'country-admin.locations.list',
                                editState: 'country-admin.locations.edit',
                                createState: 'country-admin.locations.create',
                                title: 'Locations',
                                formTemplate: 'tpl/country-admin/locations/form.html'
                            };
                        }
                    }, defaultResolve)
                })
                .state('country-admin.locations.list', {
                    url: '/list',
                    templateUrl: 'tpl/generic/list.html',
                    controller: 'GenericGridCtrl'
                })

                .state('country-admin.locations.edit', {
                    url: '/edit/:id',
                    templateUrl: 'tpl/generic/edit.html',
                    controller: 'GenericEditCtrl'
                })
                .state('country-admin.locations.create', {
                    url: '/create',
                    templateUrl: 'tpl/generic/create.html',
                    controller: 'GenericCreateCtrl'
                })
                .state('country-admin.voucher-types', {
                    url: '/voucher-types',
                    abstract: true,
                    template: '<div ui-view></div>',
                    resolve: angular.extend({
                        settings: function () {
                            return {
                                entityType: 'VoucherType',
                                collectionType: 'VoucherTypes',
                                listState: 'country-admin.voucher-types.list',
                                editState: 'country-admin.voucher-types.edit',
                                createState: 'country-admin.voucher-types.create',
                                title: 'Voucher Types',
                                formTemplate: 'tpl/country-admin/voucher-types/form.html'
                            };
                        }
                    }, defaultResolve)
                })
                .state('country-admin.voucher-types.list', {
                    url: '/list',
                    templateUrl: 'tpl/generic/list.html',
                    controller: 'GenericGridCtrl'
                })

                .state('country-admin.voucher-types.edit', {
                    url: '/edit/:id',
                    templateUrl: 'tpl/generic/edit.html',
                    controller: 'GenericEditCtrl'
                })
                .state('country-admin.voucher-types.create', {
                    url: '/create',
                    templateUrl: 'tpl/generic/create.html',
                    controller: 'GenericCreateCtrl'
                })
                .state('country-admin.users', {
                    url: '/users',
                    abstract: true,
                    template: '<div ui-view></div>',
                    resolve: angular.extend({
                        settings: ['adminBackendService', function (adminBackendService) {
                            return {
                                entityType: 'ApplicationUser',
                                collectionType: 'CountryUsers',
                                listState: 'country-admin.users.list',
                                editState: 'country-admin.users.edit',
                                createState: 'country-admin.users.create',
                                title: 'Registered Users',
                                formTemplate: 'tpl/country-admin/users/form.html',
                                backendService: adminBackendService,
                                expand: ["Roles"],
                                columns: [
                                    ["FullName", gettext("Name")],
                                    ["Email", gettext("Email")]
                                ]
                            };
                        }]
                    }, defaultResolve)
                })
                .state('country-admin.users.list', {
                    url: '/list',
                    templateUrl: 'tpl/generic/list.html',
                    controller: 'CountryUsersListCtrl'
                })

                .state('country-admin.users.edit', {
                    url: '/edit/:id',
                    templateUrl: 'tpl/country-admin/users/edit.html',
                    controller: 'GenericEditCtrl'
                })
                .state('country-admin.users.create', {
                    url: '/create',
                    templateUrl: 'tpl/generic/create.html',
                    controller: 'GenericCreateCtrl'
                })
                .state('country-admin.users.my-profile', {
                    url: '/my-profile',
                    templateUrl: 'tpl/country-admin/users/my-profile.html',
                    controller: 'EditCurrentUserCtrl'
                })



                .state('org-admin', {
                    abstract: true,
                    url: '/org-admin',
                    templateUrl: 'tpl/app.html',
                    resolve: defaultResolve
                })
                .state('org-admin.countries', {
                    url: '/countries',
                    abstract: true,
                    template: '<div ui-view></div>',
                    resolve: angular.extend({
                        settings: ['adminBackendService', function (adminBackendService) {
                            return {
                                entityType: 'OrganizationCountry',
                                collectionType: 'OrganizationCountries',
                                listState: 'org-admin.countries.list',
                                editState: 'org-admin.countries.edit',
                                createState: 'org-admin.countries.create',
                                title: 'Countries',
                                formTemplate: 'tpl/org-admin/countries/form.html',
                                backendService: adminBackendService,
                                columns: [
                                    ["Country.Name", gettext("Name")],
                                ]
                            };
                        }]
                    }, defaultResolve)
                })
                .state('org-admin.countries.list', {
                    url: '/list',
                    templateUrl: 'tpl/generic/list.html',
                    controller: 'GenericGridCtrl'
                })

                .state('org-admin.countries.edit', {
                    url: '/edit/:id',
                    templateUrl: 'tpl/generic/edit.html',
                    controller: 'GenericEditCtrl'
                })
                .state('org-admin.countries.create', {
                    url: '/create',
                    templateUrl: 'tpl/generic/create.html',
                    controller: 'GenericCreateCtrl'
                })
                .state('org-admin.users', {
                    url: '/users',
                    abstract: true,
                    template: '<div ui-view></div>',
                    resolve: angular.extend({
                        settings: ['adminBackendService', function (adminBackendService) {
                            return {
                                entityType: 'ApplicationUser',
                                collectionType: 'OrganizationUsers',
                                listState: 'org-admin.users.list',
                                editState: 'org-admin.users.edit',
                                createState: 'org-admin.users.create',
                                title: 'Organization Users',
                                formTemplate: 'tpl/org-admin/users/form.html',
                                backendService: adminBackendService,
                                columns: [
                                    ["FullName", gettext("Name")],
                                    ["Email", gettext("Email")]
                                ]
                            };
                        }]
                    }, defaultResolve)
                })
                .state('org-admin.users.list', {
                    url: '/list',
                    templateUrl: 'tpl/generic/list.html',
                    controller: 'OrganizationUsersListCtrl'
                })

                .state('org-admin.users.edit', {
                    url: '/edit/:id',
                    templateUrl: 'tpl/org-admin/users/edit.html',
                    controller: 'OrganizationUsersEditCtrl'
                })
                .state('org-admin.users.register', {
                    url: '/create',
                    templateUrl: 'tpl/org-admin/users/register.html',
                    controller: 'OrganizationUsersEditCtrl'
                })


                .state('system-admin', {
                    abstract: true,
                    url: '/system-admin',
                    templateUrl: 'tpl/app.html',
                    resolve: angular.extend({
                        settings: function () {
                            return {

                            };
                        }
                    }, defaultResolve)
                })
                .state('system-admin.users', {
                    url: '/users',
                    abstract: true,
                    template: '<div ui-view></div>',
                    resolve: angular.extend({
                        settings: function () {
                            return {
                                entityType: 'ApplicationUsers',
                                collectionType: 'Administrators',
                                listState: 'system-admin.users.list',
                                editState: 'system-admin.users.edit',
                                createState: 'system-admin.users.register',
                                title: 'System Administrators',
                                columns: [
                                    ["FullName", gettext("Name")],
                                    ["Email", gettext("Email")],
                                    ["Organization.Name", gettext("Organization")]
                                ]
                            };
                        }
                    }, defaultResolve)
                })
                .state('system-admin.users.list', {
                    url: '/list',
                    templateUrl: 'tpl/generic/list.html',
                    controller: 'SystemAdminUsersListCtrl'
                })
                .state('system-admin.users.edit', {
                    url: '/edit/:id',
                    templateUrl: 'tpl/system-admin/users/edit.html',
                    controller: 'SystemAdminUsersEditCtrl'
                })
                .state('system-admin.users.register', {
                    url: '/register',
                    templateUrl: 'tpl/system-admin/users/register.html',
                    controller: 'SystemAdminUsersEditCtrl'
                })
                .state('system-admin.countries', {
                    url: '/countries',
                    abstract: true,
                    template: '<div ui-view></div>',
                    resolve: angular.extend({
                        settings: ['adminBackendService', function (adminBackendService) {
                            return {
                                entityType: 'Country',
                                collectionType: 'Countries',
                                listState: 'system-admin.countries.list',
                                editState: 'system-admin.countries.edit',
                                createState: 'system-admin.countries.create',
                                title: 'Countries',
                                formTemplate: 'tpl/system-admin/countries/form.html',
                                backendService: adminBackendService,
                                expand: ["Settings", "Settings.PropertyCollection"],
                                resultMap: function (r) { return r;},
                                columns: [
                                    ["Name", gettext("Name")],
                                    ["IsoAlpha2", gettext("2-letter ISO Alpha Code")],
                                    ["IsoAlpha3", gettext("3-letter ISO Alpha Code")]
                                ]
                            };
                        }]
                    }, defaultResolve)
                })
                .state('system-admin.countries.list', {
                    url: '/list',
                    templateUrl: 'tpl/generic/list.html',
                    controller: 'GenericGridCtrl'
                })

                .state('system-admin.countries.edit', {
                    url: '/edit/:id',
                    templateUrl: 'tpl/generic/edit.html',
                    controller: 'SystemAdminCountriesEditCtrl'
                })
                .state('system-admin.countries.create', {
                    url: '/create',
                    templateUrl: 'tpl/generic/create.html',
                    controller: 'SystemAdminCountriesEditCtrl'
                })

                .state('system-admin.message-log', {
                    url: '/message-log',
                    templateUrl: 'tpl/system-admin/message-log/list.html',
                    controller: 'MessageLogCtrl'
                })

                .state('system-admin.organizations', {
                    url: '/organizations',
                    abstract: true,
                    template: '<div ui-view></div>',
                    resolve: angular.extend({
                        settings: ['adminBackendService', function (adminBackendService) {
                            return {
                                entityType: 'Organization',
                                collectionType: 'Organizations',
                                listState: 'system-admin.organizations.list',
                                editState: 'system-admin.organizations.edit',
                                createState: 'system-admin.organizations.create',
                                title: 'Organizations',
                                formTemplate: 'tpl/system-admin/organizations/form.html',
                                backendService: adminBackendService,
                                columns: [
                                    ["Name", gettext("Name")],
                                    ["Abbreviation", gettext("Abbreviation")]
                                ]
                            };
                        }]
                    }, defaultResolve)
                })
                .state('system-admin.organizations.list', {
                    url: '/list',
                    templateUrl: 'tpl/generic/list.html',
                    controller: 'GenericGridCtrl'
                })

                .state('system-admin.organizations.edit', {
                    url: '/edit/:id',
                    templateUrl: 'tpl/generic/edit.html',
                    controller: 'OrganizationsEditCtrl'
                })
                .state('system-admin.organizations.edit-country', {
                    url: '/:organizationId/edit-country/:id',
                    templateUrl: 'tpl/system-admin/organizations/edit-country.html',
                    controller: 'OrganizationCountriesEditCtrl',
                    resolve: angular.extend({
                        settings: ['adminBackendService', function (adminBackendService) {
                            return {
                                entityType: 'OrganizationCountry',
                                collectionType: 'OrganizationCountries',
                                title: 'Country',
                                backendService: adminBackendService,
                                expand: ['Country', 'Organization', 'Settings', 'Settings.PropertyCollection', 'Country.Settings', 'Country.Settings.PropertyCollection']
                            };
                        }]
                    }, defaultResolve)
                })
                .state('system-admin.organizations.new-country', {
                    url: '/:organizationId/new-country',
                    templateUrl: 'tpl/system-admin/organizations/edit-country.html',
                    controller: 'OrganizationCountriesEditCtrl',
                    resolve: angular.extend({
                        settings: ['adminBackendService', function (adminBackendService) {
                            return {
                                entityType: 'OrganizationCountry',
                                collectionType: 'OrganizationCountries',
                                title: 'Country',
                                backendService: adminBackendService,
                                expand: ['Country', 'Organization', 'Settings', 'Settings.PropertyCollection', 'Country.Settings', 'Country.Settings.PropertyCollection']
                            };
                        }]
                    }, defaultResolve)
                })
                .state('system-admin.organizations.create', {
                    url: '/create',
                    templateUrl: 'tpl/generic/create.html',
                    controller: 'GenericCreateCtrl'
                })

                .state('reporting', {
                    url: '/reporting',
                    abstract: true,
                    templateUrl: 'tpl/app.html',
                    resolve: angular.extend({
                        settings: function () {
                            return {};
                        }
                    }, defaultResolve)
                })
                .state('reporting.distribution', {
                    url: '/distribution',
                    templateUrl: 'tpl/reports/distribution.html',
                    controller: 'DistributionReportingCtrl'
                })
                .state('reporting.vendor-financial', {
                    url: '/vendor-financial',
                    templateUrl: 'tpl/reports/vendor-financial.html',
                    controller: 'VendorFinancialReportingCtrl'
                })
                .state('reporting.payment-schedule', {
                    url: '/payment-schedule',
                    templateUrl: 'tpl/reports/payment-schedule.html',
                    controller: 'PaymentScheduleReportCtrl'
                })

                .state('reconciliation', {
                    url: '/reconciliation',
                    abstract: true,
                    templateUrl: 'tpl/app.html',
                    resolve: angular.extend({
                        settings: function () {
                            return {};
                        }
                    }, defaultResolve)
                })
                .state('reconciliation.report-history', {
                    url: '/report-history',
                    templateUrl: 'tpl/reconciliation/report-history.html',
                    controller: 'ReportHistoryReconciliationCtrl'
                })
                .state('reconciliation.vendor-receipt', {
                    url: '/vendor-receipt',
                    templateUrl: 'tpl/reconciliation/vendor-receipt.html',
                    controller: 'VendorReceiptReconciliationCtrl'
                })

          ;

      }
    ]
  );
