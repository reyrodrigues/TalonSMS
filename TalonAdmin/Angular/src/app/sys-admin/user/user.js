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
                collectionType: "Users",
                entityType: 'ApplicationUser',
                form: 'sys-admin/user/form.tpl.html',
                controlledLists: ['organizations'],
                expand: ['organization'],
                defaults: {
                    id: '00000000-0000-0000-0000-000000000000'
                }
            }
        }
    })

    .state('sys-admin.users.list', {
        url: '/index',
        controller: 'SysUserListController as vm',
        templateUrl: 'index.tpl.html',
        data: {
            pageTitle: 'All Users',
            settings: {
                expand: ['organization', 'roles', 'claims', 'logins'],
                columns: [
                    ['userName', 'User Name'],
                    ['fullName', 'Full Name'],
                    ['organization.name', 'Organization'],
                    ['email', 'Email']
                ]
            }
        }
    })

    .state('sys-admin.users.create', {
        url: '/create',
        controller: 'SysUserEditController as vm',
        templateUrl: 'create.tpl.html',
        data: {
            pageTitle: 'All Users'
        }
    })
    .state('sys-admin.users.edit', {
        url: '/{id}',
        controller: 'SysUserEditController as vm',
        templateUrl: 'edit.tpl.html',
        data: {
            pageTitle: 'All Users',
            settings: {
            }
        }
    })
    ;

})
.controller('SysUserEditController', SysUserEditController)
.controller('SysUserListController', SysUserListController)
;

SysUserListController.prototype.remove = function (id) {
    var toaster = this.$injector.get('toaster');
    var dialogs = this.$injector.get('dialogs');
    var $rootScope = this.$injector.get('$rootScope');
    var entityManagerFactory = this.$injector.get('entityManagerFactory');
    var entityManager = entityManagerFactory.adminEntityManager();
    var self = this;
    var url = serviceRoot + 'Api/ApplicationUser/';
    var $http = this.$injector.get('$http');

    if (id == $rootScope.currentUser.Id) {
        toaster.pop('error', 'Error!', 'You can\'t delete yourself from the system.');
    } else {
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
    }
};

SysUserEditController.prototype.configure = function configure() {
    var $http = this.$injector.get('$http');
    var $q = this.$injector.get('$q');
    var entityManagerFactory = this.$injector.get('entityManagerFactory');
    var entityManager = entityManagerFactory.adminEntityManager();
    var self = this;

    this.mergeRoles = MergeRoles;
    this.mergeCountries = MergeCountries;

    this.$scope.removeRole = RemoveRole;
    this.$scope.removeCountry = RemoveCountry;
    this.$scope.addRole = AddRole;
    this.$scope.addCountry = AddCountry;

    function RemoveCountry(country, grid) {
        entityManagerFactory.entityQuery('ApplicationUserCountries')
            .where({
                and: [
                    { countryId: { '==': country.id } },
                    { applicationUserId: { '==': self.entity.id } },
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

    function AddCountry(country, grid) {
        var entity = entityManager.createEntity('ApplicationUserCountry', {
            countryId: country.id,
            applicationUserId: self.entity.id
        });

        entityManager.saveChanges([entity]).then(function () {
            grid.api.custom.reloadData();
        }).catch(function (error) {
            console.log(error);

        });
    }

    function RemoveRole(role, grid) {
        $http.post(serviceRoot + 'Api/ApplicationUser/' + self.entity.id + '/RemoveUserFromRole', {
            roleId: role.id
        })
        .then(function () {
            grid.api.custom.reloadData();
        }).catch(function (error) {
            console.log(error);

        });
    }

    function AddRole(role, grid) {
        $http.post(serviceRoot + 'Api/ApplicationUser/' + self.entity.id + '/AddUserToRole', {
            roleId: role.id
        })
        .then(function () {
            grid.api.custom.reloadData();
        }).catch(function (error) {
            console.log(error);

        });
    }

    function MergeRoles(q) {
        var def = $q.defer();

        entityManagerFactory.entityQuery('UserRoles')
            .where({
                and: [
                    { userId: { '==': self.entity.id } },
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

    function MergeCountries(q) {
        var def = $q.defer();

        entityManagerFactory.entityQuery('ApplicationUserCountries')
            .where({
                and: [
                    { applicationUserId: { '==': self.entity.id } },
                ]
            })
            .using(entityManager)
            .execute()
            .then(function (response) {
                var ids = response.results.map(function (r) { return r.countryId; });
                var results = q.map(function (r) {
                    r.isLinked = ids.indexOf(r.id) > -1;
                    return r;
                });

                def.resolve(results);
            });

        return def.promise;
    }
};

// Custom Save Marker
SysUserEditController.prototype.save = function save(continueEditing) {
    var self = this;
    var $state = this.$injector.get('$state');
    var $http = this.$injector.get('$http');
    var entityManagerFactory = this.$injector.get('entityManagerFactory');
    var entityManager = entityManagerFactory.adminEntityManager();

    var url = serviceRoot + 'Api/ApplicationUser/';

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

    self.isEditing = false;

    var payload = {
        fullName: self.entity.fullName,
        userName: self.entity.userName,
        organizationId: self.entity.organizationId,
        email: self.entity.email,
        password: self.password.password,
        confirmPassword: self.password.confirmPassword,
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

            var defaults = self.defaults();

            self.entity = self.entityManager.createEntity(self.settings.entityType, defaults);

            $state.go('^.edit', { id: ne.data.Id });
        }
        
        try {
            if (self.entity.entityAspect) {
                self.entity.entityAspect.setUnchanged();
            }
        } catch (e) { }
        
        self.isEditing = continueEditing;
    }).catch(function (error) {
        console.log(error);
    });
};

function SysUserListController($injector, $scope) {
    ListController.call(this, $injector, $scope);
}

function SysUserEditController($injector, $scope) {
    this.password = {};

    EditController.call(this, $injector, $scope);
}