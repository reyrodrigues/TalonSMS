angular.module('talon.sys-admin')
.config(function config($stateProvider) {
    $stateProvider
    .state('sys-admin.actions', {
        url: '/actions',
        template: "<div ui-view></div>",
        abstract: true,
        data: {
            settings: {
                entityManager: 'adminEntityManager',
                collectionType: "Actions",
                entityType: 'Action',
                form: 'sys-admin/actions/form.tpl.html'
            }
        }
    })

    .state('sys-admin.actions.list', {
        url: '/index',
        controller: 'GenericListCtrl as vm',
        templateUrl: 'index.tpl.html',
        data: {
            pageTitle: 'Actions',
            settings: {
                columns: [
                    ['id', '#'],
                    ['name', 'Name']
                ]
            }
        }
    })
    .state('sys-admin.actions.create', {
        url: '/create',
        controller: 'GenericEditCtrl as vm',
        templateUrl: 'edit.tpl.html',
        data: {
            pageTitle: 'Actions',
            settings: {
            }
        }
    })
    .state('sys-admin.actions.edit', {
        url: '/{id:int}',
        controller: 'GenericEditCtrl as vm',
        templateUrl: 'edit.tpl.html',
        data: {
            pageTitle: 'Actions',
            settings: {
            }
        }
    })
    ;

})
;