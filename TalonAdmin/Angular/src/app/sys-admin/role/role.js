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
                form: 'sys-admin/role/form.tpl.html'
            }
        }
    })

    .state('sys-admin.roles.list', {
        url: '/index',
        controller: 'RoleListController as vm',
        templateUrl: 'index.tpl.html',
        data: {
            pageTitle: 'Application Role',
            settings: {
                columns: [
                    ['name', 'Name']
                ]
            }
        }
    })
    .state('sys-admin.roles.create', {
        url: '/create',
        controller: 'RoleEditController as vm',
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
.controller('RoleListController', RoleListController)
;

RoleListController.prototype.remove = function (id) {
    var self = this;
    var $state = this.$injector.get('$state');
    var dialogs = this.$injector.get('dialogs');
    var $http = this.$injector.get('$http');
    var url = serviceRoot + 'Api/ApplicationRole/';
    var dlg = dialogs.confirm("Confirm", "Are you sure you would like to delete this record? This operation cannot be reversed.");
    dlg.result.then(function () {
        $http.delete(url + id)
           .then(function (ne) {
               self.success('Record successfully deleted.');

               self.instance.rerender();
           }).catch(function (error) {
               self.failure(error);
           });
    });
};

RoleEditController.prototype.load = function () {
    var $state = this.$injector.get('$state');
    var $q = this.$injector.get('$q');
    var entityManagerFactory = this.$injector.get('entityManagerFactory');
    var self = this;

    if (self.settings.controlledLists) {
        self.settings.controlledLists.forEach(function (l) {
            controlledLists[l]().then(function (list) {
                self.lists[l] = list;
            });
        });
    }

    var defer = $q.defer();
    $q.when(self.preLoad()).then(function () {
        if ($state.params.id) {
            self.isNew = false;
            self.isEditing = false;

            var query = entityManagerFactory.entityQuery(self.settings.collectionType)
                .using(self.entityManager);

            if (self.settings.expand) {
                query = query.expand(self.settings.expand);
            }

            query = query.where("id", "==", $state.params.id)
                .take(1);

            if (self.settings.entityType) {
                var entityType = self.entityManager.metadataStore.getEntityType(self.settings.entityType);
                query = query.toType(entityType);
            }

            query.execute()
                .then(function (res) {
                    if (res.results) {
                        var entity = res.results.pop();

                        self.entity = entity;
                        defer.resolve(entity);
                    }
                }).catch(function () {
                    console.log(arguments);
                });
        } else {
            if (!self.canCreate) {
                $state.go('^.list');
            }
            self.isNew = true;
            self.isEditing = true;

            var defaults = self.defaults();

            self.entity = {};
            defer.resolve(self.entity);
        }
    });

    return defer.promise;
};

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

// Custom Save Marker
RoleEditController.prototype.save = function save(continueEditing) {
    var self = this;
    var $state = this.$injector.get('$state');
    var $http = this.$injector.get('$http');
    var entityManagerFactory = this.$injector.get('entityManagerFactory');
    var entityManager = entityManagerFactory.adminEntityManager();

    var $scope = this.$scope;
    if ($scope.dataForm.$invalid) {
        angular.forEach($scope.dataForm.$error.required, function (field) {
            field.$setDirty();
            field.$setTouched();
        });

        return;
    }

    $scope.dataForm.$setPristine();
    $scope.dataForm.$setUntouched();

    var url = serviceRoot + 'Api/ApplicationRole/';

    self.isEditing = false;

    var payload = {
        name: self.entity.name
    };
    var def = null;
    if (self.isNew) {
        def = $http.post(url, payload);
    } else {
        def = $http.put(url + self.entity.id, payload);
    }

    def
    .then(function (ne) {
        self.success('Record successfully saved.');
        if (self.isNew) {
            self.entity.id = ne.Id;

            $state.go('^.edit', { id: ne.data.Id });
        }
        if (self.entity.entityAspect) {
            self.entity.entityAspect.setUnchanged();
        }

        self.isEditing = continueEditing;
    }).catch(function (error) {
        console.log(error);
    });
};

function RoleEditController($injector, $scope) {
    EditController.call(this, $injector, $scope);
}

function RoleListController($injector, $scope) {
    ListController.call(this, $injector, $scope);
}