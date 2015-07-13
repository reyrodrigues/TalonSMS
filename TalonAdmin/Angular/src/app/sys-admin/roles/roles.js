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
        templateUrl: 'edit.tpl.html',
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

    this.mergeUsers = function (results) {
        var def = $q.defer();

        def.resolve(results.map(function (r) {
            r.user = userById(r.userId);
            return r;
        }));

        return def.promise;
    };

    function userById(id) {
        if (self.$scope.users) {
            return self.$scope.users.filter(function (u) { return u.id == id; })[0];
        }
        return {};
    }
};


function RoleEditController($injector, $scope) {
    EditController.call(this, $injector, $scope);
}