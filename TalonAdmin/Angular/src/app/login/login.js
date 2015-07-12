angular.module('talon.login', [
    'talon.auth',
  'ui.router',
  'ngStorage'
])
.config(function config($stateProvider) {
    $stateProvider.state('login', {
        url: '/login',
        views: {
            "full": {
                controller: 'LoginCtrl',
                templateUrl: 'login/login.tpl.html'
            }
        },
        data: {
            pageTitle: 'Login',
            allowAnonymous: true
        }
    });
})

.controller('LoginCtrl', function LoginController($scope, $state, authService, $localStorage) {
    var authData = $localStorage.authorizationData;
    if (authData) {
        $state.go('dashboard', { location: 'replace'});
    }

    $scope.logIn = function logIn() {
        authService.logIn($scope.username, $scope.password)
            .then(function () {
                delete $scope.authError;

                $scope.$emit('app:authenticated');
                $state.go('dashboard', {location: 'replace'});
            })
            .catch(function () {
                $scope.authError = "Invalid username or password.";
            });
    };
})
;

