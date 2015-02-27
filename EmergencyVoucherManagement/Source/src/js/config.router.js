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


          $urlRouterProvider.otherwise('/app/dashboard');
          $stateProvider
              .state('app', {
                  abstract: true,
                  url: '/app',
                  templateUrl: 'tpl/app.html',
                  resolve: {
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
                      locations: ['controlledListService', function (controlledListService) {
                          return controlledListService.getLocations();
                      }],
                      voucherTypes: ['controlledListService', function (controlledListService) {
                          return controlledListService.getVoucherTypes();
                      }],
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
                      locations: ['controlledListService', function (controlledListService) {
                          return controlledListService.getLocations();
                      }],
                      voucherTypes: ['controlledListService', function (controlledListService) {
                          return controlledListService.getVoucherTypes();
                      }],
                  }
              })
              .state('groups.list', {
                  url: '/list',
                  templateUrl: 'tpl/groups/list.html',
                  controller: 'BeneficiaryGroupGridCtrl'
              })

              .state('groups.edit', {
                  url: '/edit/:id',
                  templateUrl: 'tpl/groups/edit.html',
                  controller: 'BeneficiaryGroupEditCtrl'
              })
              .state('groups.create', {
                  url: '/create',
                  templateUrl: 'tpl/groups/create.html',
                  controller: 'BeneficiaryGroupRegisterCtrl'
              })

              .state('vendors', {
                  url: '/vendors',
                  abstract: true,
                  templateUrl: 'tpl/app.html',
                  resolve: {
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
