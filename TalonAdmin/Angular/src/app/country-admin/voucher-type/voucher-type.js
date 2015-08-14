angular.module('talon.country-admin')

.config(function config($stateProvider) {
    $stateProvider
    .state('country-admin.voucher-types', {
        url: '/voucher-type',
        template: "<div ui-view></div>",
        abstract: true,
        data: {
            settings: {
                collectionType: "VoucherTypes",
                entityType: 'VoucherType',
                form: 'country-admin/voucher-type/form.tpl.html'
            }
        }
    })

    .state('country-admin.voucher-types.list', {
        url: '/index',
        controller: 'GenericListCtrl as vm',
        templateUrl: 'index.tpl.html',
        data: {
            pageTitle: 'Voucher Types',
            settings: {
                columns: [
                    ['id', '#'],
                    ['name', 'Name']
                ]
            }
        }
    })

    .state('country-admin.voucher-types.edit', {
        url: '/{id:int}',
        controller: 'GenericEditCtrl as vm',
        templateUrl: 'edit.tpl.html',
        data: {
            pageTitle: 'Voucher Types'
        }
    })

    .state('country-admin.voucher-types.create', {
        url: '/create',
        controller: 'GenericEditCtrl as vm',
        templateUrl: 'create.tpl.html',
        data: {
            pageTitle: 'Voucher Types'
        }
    });
})

;
