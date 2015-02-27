'use strict';

/* Controllers */
// signin controller
app.controller('SigninFormController', ['$scope', '$http', '$state', 'authService', 'localStorageService', '$rootScope', 'ngAuthSettings',
    function ($scope, $http, $state, authService, localStorageService, $rootScope, ngAuthSettings) {
        $scope.user = {};
        $scope.authError = null;

        $scope.login = function () {
            $scope.authError = null;

            authService.login({
                'userName': $scope.user.email,
                'password': $scope.user.password
            }).then(function (response) {
                $rootScope.$emit('app:authenticated');
            }, function (error) {
                $scope.authError = error.error_description;
            });
            // Try to login
        };
    }])
;