angular.module('talon.vendorType', [
  'ui.router',
  'datatables',
  'datatables.bootstrap',
  'talon.dataAccess',
  'talon.common',
  'dialogs.main',
  'ngFileUpload'
])

.config(function config($stateProvider) {
    $stateProvider
    .state('vendor-types', {
        url: '/vendor-type',
        views: {
            "app": {
                template: "<div ui-view></div>"
            }
        },
        abstract: true,
        data: {
            settings: {
                collectionType: "VendorTypes",
                entityType: 'VendorType',
                expand: ['vendors'],
                form: 'vendorType/form.tpl.html'
            }
        }
    })

    .state('vendor-types.list', {
        url: '/index',
        controller: 'GenericListCtrl as vm',
        templateUrl: 'index.tpl.html',
        data: {
            pageTitle: 'Vendor Types',
            settings: {
                columns: [
                    ['id', '#'],
                    ['name', 'Name'],
                    ['vendors.length', 'Number of Vendors']
                ]
            }
        }
    })

    .state('vendor-types.edit', {
        url: '/{id:int}',
        controller: 'GenericEditCtrl as vm',
        templateUrl: 'edit.tpl.html',
        data: {
            pageTitle: 'Vendor Types'
        }
    })

    .state('vendor-types.create', {
        url: '/create',
        controller: 'GenericEditCtrl as vm',
        templateUrl: 'create.tpl.html',
        data: {
            pageTitle: 'Vendor Types'
        }
    });
})

;
