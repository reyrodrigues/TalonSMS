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
    ['$stateProvider', '$urlRouterProvider', 'JQ_CONFIG',
      function ($stateProvider, $urlRouterProvider, JQ_CONFIG) {
          var fetchMetadata = ['serviceBase', 'backendService', '$q', 'adminBackendService', '$localStorage',
              function (serviceBase, backendService, $q, adminBackendService, $localStorage) {
                  var defer = $q.defer();

                  $q.all([
                      backendService.metadataStore.fetchMetadata(serviceBase + 'Breeze/EVM'),
                      adminBackendService.metadataStore.fetchMetadata(serviceBase + 'Breeze/Admin')
                  ]).then(function () { defer.resolve(); })
                  .catch(function () { defer.resolve(); });

                  return defer.promise;
              }];

          var defaultResolve = {
              metadata: fetchMetadata,
              locations: ['controlledListService', function (controlledListService) {
                  return controlledListService.getLocations();
              }],
              voucherTypes: ['controlledListService', function (controlledListService) {
                  return controlledListService.getVoucherTypes();
              }],
              groups: ['controlledListService', function (controlledListService) {
                  return controlledListService.getBeneficiaryGroups();
              }],
              vendorTypes: ['controlledListService', function (controlledListService) {
                  return controlledListService.getVendorTypes();
              }],
              organizations: ['controlledListService', function (controlledListService) {
                  return controlledListService.getOrganizations();
              }],
              countries: ['controlledListService', function (controlledListService) {
                  return controlledListService.getCountries();
              }]
          }

          $urlRouterProvider.otherwise('/app/dashboard');
          $stateProvider
              .state('app', {
                  abstract: true,
                  url: '/app',
                  templateUrl: 'tpl/app.html',
                  resolve: {
                      metadata: fetchMetadata,
                      locations: ['controlledListService', function (controlledListService) {
                          return controlledListService.getLocations();
                      }],
                      voucherTypes: ['controlledListService', function (controlledListService) {
                          return controlledListService.getVoucherTypes();
                      }],
                  }
              })

              .state('app.dashboard', {
                  url: '/dashboard',
                  templateUrl: 'tpl/app/dashboard.html',
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
                  resolve: defaultResolve
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
                  templateUrl: 'tpl/generic/edit.html',
                  controller: 'GenericEditCtrl'
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
                  resolve: defaultResolve
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
                          return{
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
              .state('distributions', {
                  url: '/distributions',
                  abstract: true,
                  templateUrl: 'tpl/app.html',
                  resolve: defaultResolve
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
                      settings: function () {
                          return {
                              
                          };
                      }
                  }, defaultResolve)
              })
              .state('country-admin.users.my-profile', {
                  url: '/my-profile',
                  templateUrl: 'tpl/country-admin/users/my-profile.html',
                  controller: 'CurrentUserProfileCtrl'
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
                                collectionType: 'SystemAdministrators',
                                listState: 'system-admin.users.list',
                                editState: 'system-admin.users.edit',
                                createState: 'system-admin.users.register',
                                title: 'System Administrators',
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
                  controller: 'SystemAdminUsersRegisterCtrl'
              })

          ;

      }
    ]
  );
