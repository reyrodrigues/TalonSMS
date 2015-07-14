angular.module('talon.sys-admin')
.config(function config($stateProvider) {
    $stateProvider
    .state('sys-admin.roles', {
        url: '/roles',
        template: "<div ui-view></div>",
        abstract: true,
        data: {
            settings: {
                entityManager: 'adminEntityManager',
                collectionType: "Roles",
                entityType: 'IdentityRole',
                form: 'sys-admin/roles/form.tpl.html'
            }
        }
    })

    .state('sys-admin.roles.list', {
        url: '/index',
        controller: 'GenericListCtrl as vm',
        templateUrl: 'index.tpl.html',
        data: {
            pageTitle: 'Application Role',
            settings: {
                columns: [
                    ['id', '#'],
                    ['name', 'Name']
                ]
            }
        }
    })
    .state('sys-admin.roles.create', {
        url: '/create',
        controller: 'GenericEditCtrl as vm',
        templateUrl: 'create.tpl.html',
        data: {
            pageTitle: 'Application Role',
            settings: {
            }
        }
    })
    .state('sys-admin.roles.edit', {
        url: '/edit/{id}',
        controller: 'RoleEditController as vm',
        templateUrl: 'edit.tpl.html',
        data: {
            pageTitle: 'Application Role',
            settings: {
            }
        }
    })
    ;

})
.controller('RoleEditController', RoleEditController)
;

RoleEditController.prototype.configure = function () {
    var $q = this.$injector.get('$q');
    var entityManagerFactory = this.$injector.get('entityManagerFactory');
    var entityManager = entityManagerFactory.adminEntityManager();
    var self = this;

    this.userQuery = entityManagerFactory.entityQuery('Users')
        .using(entityManager)
        .execute().then(function (r) {
            self.$scope.users = r.results;
        });

    this.mergeUsers = MergeUsers;
    this.mergeActions = MergeActions;
    this.mergeMenuCategories = MergeMenuCategories;

    this.$scope.removeAction = RemoveAction;
    this.$scope.addAction = AddAction;
    this.$scope.removeCategory = RemoveCategory;
    this.$scope.addCategory = AddCategory;


    function userById(id) {
        if (self.$scope.users) {
            return self.$scope.users.filter(function (u) { return u.id == id; })[0];
        }
        return {};
    }

    function RemoveAction(action, grid) {
        entityManagerFactory.entityQuery('ActionRoles')
            .where({
                and: [
                    { actionId: { '==': action.id } }, ,
                    { roleId: { '==': self.entity.id } },
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
                    console.log(error);
                });
            });
    }

    function AddAction(action, grid) {
        var entity = entityManager.createEntity('ActionRole', {
            actionId: action.id,
            roleId: self.entity.id
        });

        entityManager.saveChanges([entity]).then(function () {
            grid.api.custom.reloadData();
        }).catch(function (error) {
            console.log(error);

        });
    }

    function RemoveCategory(category, grid) {
        entityManagerFactory.entityQuery('MenuCategoryRoles')
            .where({
                and: [
                    { categoryId: { '==': category.id } }, ,
                    { roleId: { '==': self.entity.id } },
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
                    console.log(error);
                });
            });
    }

    function AddCategory(category, grid) {
        var entity = entityManager.createEntity('MenuCategoryRole', {
            categoryId: category.id,
            roleId: self.entity.id
        });

        entityManager.saveChanges([entity]).then(function () {
            grid.api.custom.reloadData();
        }).catch(function (error) {
            console.log(error);

        });
    }

    function MergeActions(q) {
        var def = $q.defer();

        entityManagerFactory.entityQuery('ActionRoles')
            .where({
                and: [
                    { roleId: { '==': self.entity.id } },
                ]
            })
            .using(entityManager)
            .execute()
            .then(function (response) {
                var ids = response.results.map(function (r) { return r.actionId; });
                var results = q.map(function (r) {
                    r.isLinked = ids.indexOf(r.id) > -1;
                    return r;
                });

                def.resolve(results);
            });

        return def.promise;
    }

    function MergeMenuCategories(q) {
        var def = $q.defer();

        entityManagerFactory.entityQuery('MenuCategoryRoles')
            .where({
                and: [
                    { roleId: { '==': self.entity.id } },
                ]
            })
            .using(entityManager)
            .execute()
            .then(function (response) {
                var ids = response.results.map(function (r) { return r.categoryId; });
                var results = q.map(function (r) {
                    r.isLinked = ids.indexOf(r.id) > -1;
                    return r;
                });

                def.resolve(results);
            })
            .catch(console.log.bind(console));

        return def.promise;
    }

    function MergeUsers(results) {
        var def = $q.defer();

        def.resolve(results.map(function (r) {
            r.user = userById(r.userId);
            return r;
        }));

        return def.promise;
    }
};

function RoleEditController($injector, $scope) {
    EditController.call(this, $injector, $scope);
}