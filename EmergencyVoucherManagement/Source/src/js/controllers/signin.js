'use strict';

/* Controllers */
// signin controller
app.controller('SigninFormController', ['$scope', '$http', '$state', 'authService', '$localStorage', '$rootScope', 'ngAuthSettings',
    function ($scope, $http, $state, authService, $localStorage, $rootScope, ngAuthSettings) {
        $scope.user = {};
        $scope.authError = null;

        $scope.login = function () {
            $scope.authError = null;

            authService.login({
                'userName': $scope.user.userName,
                'password': $scope.user.password
            }).then(function (response) {
                authService.loadUserData().then(function () {
                    $rootScope.$emit('app:authenticated');
                    $state.go('app.dashboard');
                });
            }, function (error) {
                $scope.authError = error.error_description;
            });
        };
    }])
;