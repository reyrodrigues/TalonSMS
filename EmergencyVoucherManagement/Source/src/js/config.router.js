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
          var fetchMetadata = ['serviceBase', 'backendService', '$q', function (serviceBase, backendService, $q) {
              var defer = $q.defer();
              backendService.metadataStore.fetchMetadata(serviceBase + 'Breeze/EVM')
                  .then(function() {  defer.resolve(); })
                  .catch(function(){ defer.resolve();});

              return defer.promise;
          }];
 

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
                  templateUrl: 'tpl/app_dashboard.html',
                  resolve: {
                      deps: ['$ocLazyLoad',
                        function ($ocLazyLoad) {
                            return $ocLazyLoad.load(['js/controllers/dashboard.js']);
                        }]
                  }
              })



              .state('access', {
                  url: '/access',
                  template: '<div ui-view class="fade-in-right-big smooth"></div>'
              })
              .state('access.signin', {
                  url: '/signin',
                  templateUrl: 'tpl/page_signin.html',
                  resolve: {
                      deps: ['uiLoad',
                        function (uiLoad) {
                            return uiLoad.load(['js/controllers/signin.js']);
                        }]
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
                  resolve: {
                      metadata: fetchMetadata,
                      locations: ['controlledListService', function (controlledListService) {
                          return controlledListService.getLocations();
                      }],
                      voucherTypes: ['controlledListService', function (controlledListService) {
                          return controlledListService.getVoucherTypes();
                      }],
                      groups: ['controlledListService', function (controlledListService) {
                          return controlledListService.getBeneficiaryGroups();
                      }]
                  }
              })
              .state('beneficiaries.list', {
                  url: '/list',
                  templateUrl: 'tpl/beneficiaries/list.html',
                  controller: 'BeneficiaryGridCtrl'
              })

              .state('beneficiaries.edit', {
                  url: '/edit/:id',
                  templateUrl: 'tpl/beneficiaries/edit.html',
                  controller: 'BeneficiaryEditCtrl'
              })
              .state('beneficiaries.create', {
                  url: '/create',
                  templateUrl: 'tpl/beneficiaries/create.html',
                  controller: 'BeneficiaryRegisterCtrl'
              })



              .state('groups', {
                  url: '/groups',
                  abstract: true,
                  templateUrl: 'tpl/app.html',
                  resolve: {
                      metadata: fetchMetadata,
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
                  }
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

              .state('admin', {
                  abstract: true,
                  url: '/admin',
                  templateUrl: 'tpl/app.html',
                  resolve: {
                      metadata: fetchMetadata
                  }
              })
              .state('admin.locations', {
                  url: '/locations',
                  abstract: true,
                  template: '<div ui-view class="fade-in-up"></div>',
                  resolve: {
                      settings: function () {
                          return {
                              entityType: 'Location',
                              collectionType: 'Locations',
                              listState: 'admin.locations.list',
                              editState: 'admin.locations.edit',
                              createState: 'admin.locations.create',
                              title: 'Locations',
                              formTemplate: 'tpl/admin/locations/form.html'
                          };
                      }
                  }
              })
              .state('admin.locations.list', {
                  url: '/list',
                  templateUrl: 'tpl/generic/list.html',
                  controller: 'GenericGridCtrl'
              })

              .state('admin.locations.edit', {
                  url: '/edit/:id',
                  templateUrl: 'tpl/generic/edit.html',
                  controller: 'GenericEditCtrl'
              })
              .state('admin.locations.create', {
                  url: '/create',
                  templateUrl: 'tpl/generic/create.html',
                  controller: 'GenericCreateCtrl'
              })


              .state('admin.voucherTypes', {
                  url: '/voucher-types',
                  abstract: true,
                  template: '<div ui-view class="fade-in-up"></div>',
                  resolve: {
                      settings: function () {
                          return {
                              entityType: 'VoucherType',
                              collectionType: 'VoucherTypes',
                              listState: 'admin.voucherTypes.list',
                              editState: 'admin.voucherTypes.edit',
                              createState: 'admin.voucherTypes.create',
                              title: 'Voucher Types',
                              formTemplate: 'tpl/admin/voucherTypes/form.html'
                          };
                      }
                  }
              })
              .state('admin.voucherTypes.list', {
                  url: '/list',
                  templateUrl: 'tpl/generic/list.html',
                  controller: 'GenericGridCtrl'
              })

              .state('admin.voucherTypes.edit', {
                  url: '/edit/:id',
                  templateUrl: 'tpl/generic/edit.html',
                  controller: 'GenericEditCtrl'
              })
              .state('admin.voucherTypes.create', {
                  url: '/create',
                  templateUrl: 'tpl/generic/create.html',
                  controller: 'GenericCreateCtrl'
              })

              .state('vendors', {
                  url: '/vendors',
                  abstract: true,
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
              .state('vendors.list', {
                  url: '/list',
                  templateUrl: 'tpl/vendors/list.html',
                  controller: 'VendorGridCtrl'
              })

              .state('vendors.edit', {
                  url: '/edit/:id',
                  templateUrl: 'tpl/vendors/edit.html',
                  controller: 'VendorEditCtrl'
              })
              .state('vendors.create', {
                  url: '/create',
                  templateUrl: 'tpl/vendors/create.html',
                  controller: 'VendorRegisterCtrl'
              })

              .state('vouchers', {
                  url: '/vouchers',
                  abstract: true,
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

              .state('distributions.list', {
                  url: '/list',
                  templateUrl: 'tpl/distributions/list.html',
                  controller: 'VoucherDistributionGridCtrl'
              })
              .state('distributions.edit', {
                  url: '/edit-distribution/:id',
                  templateUrl: 'tpl/distributions/edit.html',
                  controller: 'VoucherDistributionEditCtrl'
              })
              .state('distributions.create', {
                  url: '/create-distribution/',
                  templateUrl: 'tpl/distributions/create.html',
                  controller: 'VoucherDistributionCreateCtrl'
              })


      }
    ]
  );
