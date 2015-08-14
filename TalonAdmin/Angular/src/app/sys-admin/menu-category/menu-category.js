angular.module('talon.sys-admin')
.config(function config($stateProvider) {
    $stateProvider
    .state('sys-admin.menu-categories', {
        url: '/menu-category',
        template: "<div ui-view></div>",
        abstract: true,
        data: {
            settings: {
                entityManager: 'adminEntityManager',
                collectionType: "MenuCategories",
                entityType: 'MenuCategory',
                expand: 'roles',
                form: 'sys-admin/menu-category/form.tpl.html'
            }
        }
    })

    .state('sys-admin.menu-categories.list', {
        url: '/index',
        controller: 'GenericListCtrl as vm',
        templateUrl: 'index.tpl.html',
        data: {
            pageTitle: 'Menu Categories',
            settings: {
                columns: [
                    ['id', '#'],
                    ['name', 'Name']
                ]
            }
        }
    })
    .state('sys-admin.menu-categories.create', {
        url: '/create',
        controller: 'GenericEditCtrl as vm',
        templateUrl: 'create.tpl.html',
        data: {
            pageTitle: 'Menu Categories',
            settings: {
            }
        }
    })
    .state('sys-admin.menu-categories.edit', {
        url: '/{id:int}',
        controller: 'MenuCategoryEditController as vm',
        templateUrl: 'edit.tpl.html',
        data: {
            pageTitle: 'Menu Categories',
            settings: {
            }
        }
    })
    ;

})
.controller('MenuCategoryEditController', MenuCategoryEditController)
;

MenuCategoryEditController.prototype.configure = function configure() {
    var toaster = this.$injector.get('toaster');
    var $q = this.$injector.get('$q');
    var dialogs = this.$injector.get('dialogs');
    var entityManagerFactory = this.$injector.get('entityManagerFactory');
    var entityManager = entityManagerFactory.adminEntityManager();
    var self = this;

    this.mergeRoles = MergeRoles;

    this.$scope.restrictAccess = RestrictAccess;
    this.$scope.allowAccess = AllowAccess;

    function RestrictAccess(role, grid) {
        entityManagerFactory.entityQuery('MenuCategoryRoles')
            .where({
                and: [
                    { roleId: { '==': role.id } },
                    { categoryId: { '==': self.entity.id } },
                ]
            })
            .using(entityManager)
            .execute()
            .then(function (r) {
                var entity = r.results[0];
                entity.entityAspect.setDeleted();

                entityManager.saveChanges([entity]).then(function () {
                    grid.api.custom.reloadData();
                }).catch(function (error) {
                });
        });
    }

    function AllowAccess(role, grid) {
        var entity = entityManager.createEntity('MenuCategoryRole', {
            roleId: role.id,
            categoryId: self.entity.id
        });

        entityManager.saveChanges([entity]).then(function () {
            grid.api.custom.reloadData();
        }).catch(function (error) {
        });
    }

    function MergeRoles(q) {
        var def = $q.defer();
        
        entityManagerFactory.entityQuery('MenuCategoryRoles')
            .where({
                and: [
                    { categoryId: { '==': self.entity.id } },
                ]
            })
            .using(entityManager)
            .execute()
            .then(function (response) {
                var ids = response.results.map(function (r) { return r.roleId; });
                var results = q.map(function (r) {
                    r.isLinked = ids.indexOf(r.id) > -1;
                    return r;
                });

                def.resolve(results);
        });

        return def.promise;
    }
};


function MenuCategoryEditController($injector, $scope) {
    EditController.call(this, $injector, $scope);
}