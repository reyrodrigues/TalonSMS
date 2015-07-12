angular.module('talon.sys-admin')
.config(function config($stateProvider) {
    $stateProvider
    .state('sys-admin.users', {
        url: '/users',
        template: "<div ui-view></div>",
        abstract: true,
        data: {
            settings: {
                entityManager: 'adminEntityManager',
                collectionType: "OrganizationUsers",
                entityType: 'ApplicationUser',
            }
        }
    })

    .state('sys-admin.users.list', {
        url: '/index',
        controller: 'GenericListCtrl as vm',
        templateUrl: 'index.tpl.html',
        data: {
            pageTitle: 'Application User',
            settings: {
                expand: ['roles', 'claims', 'logins'],
                columns: [
                    ['id', '#'],
                    ['userName', 'User Name']
                ]
            }
        }
    })
    ;

})
;