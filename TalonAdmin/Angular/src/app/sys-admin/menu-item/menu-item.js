angular.module('talon.sys-admin')
.config(function config($stateProvider) {
    $stateProvider
    .state('sys-admin.menu-items', {
        url: '/menu-item',
        template: "<div ui-view></div>",
        abstract: true,
        data: {
            settings: {
                entityManager: 'adminEntityManager',
                collectionType: "MenuItems",
                entityType: 'MenuItem',
                expand: ['category'],
                controlledLists: ['menuCategories'],
                form: 'sys-admin/menu-item/form.tpl.html'
            }
        }
    })

    .state('sys-admin.menu-items.list', {
        url: '/index',
        controller: 'GenericListCtrl as vm',
        templateUrl: 'index.tpl.html',
        data: {
            pageTitle: 'Menu Items',
            settings: {
                filter: {
                    'parentId': { '==': null }
                },
                columns: [
                    ['id', '#'],
                    ['title', 'Title'],
                    ['category.name', 'Category']
                ]
            }
        }
    })
    .state('sys-admin.menu-items.create', {
        url: '/create',
        controller: 'GenericEditCtrl as vm',
        templateUrl: 'create.tpl.html',
        data: {
            pageTitle: 'Menu Items',
            settings: {
            }
        }
    })
    .state('sys-admin.menu-items.edit', {
        url: '/{id:int}',
        controller: 'GenericEditCtrl as vm',
        templateUrl: 'edit.tpl.html',
        data: {
            pageTitle: 'Menu Items',
            settings: {
            }
        }
    })
    ;

})
;