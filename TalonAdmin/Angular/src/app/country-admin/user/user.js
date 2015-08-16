angular.module('talon.country-admin')
.config(function config($stateProvider) {
    $stateProvider
    .state('country-admin.users', {
        url: '/users',
        template: "<div ui-view></div>",
        abstract: true,
        data: {
            settings: {
                entityManager: 'adminEntityManager',
                collectionType: "Users",
                entityType: 'ApplicationUser',
                form: 'country-admin/user/form.tpl.html',
                controlledLists: ['organizations'],
                defaults: {
                    id: '00000000-0000-0000-0000-000000000000'
                }
            }
        }
    })

    .state('country-admin.users.my-profile', {
        url: '/my-profile',
        controller: 'MyProfileEditController as vm',
        templateUrl: 'edit.tpl.html',
        data: {
            pageTitle: 'My Profile',
            settings: {
            }
        }
    })
    ;

})
.controller('MyProfileEditController', MyProfileEditController)
;

// Custom Save Marker
MyProfileEditController.prototype.save = function save(continueEditing) {
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

    var url = serviceRoot + 'Api/ApplicationUser/';

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

function MyProfileEditController($injector, $scope) {
    this.password = {};
    var $state = $injector.get('$state');
    $state.params.id = $scope.currentUser.Id;

    EditController.call(this, $injector, $scope);

    this.canEdit = true;
    this.showBack = false;
}