angular.module('talon.auth', [
  'ngStorage',
  'ui.router'
])


.service('authService', function AuthService($http, $localStorage, $q, $rootScope) {
    var serviceRoot = window.location.protocol + '//' + window.location.host + '/';

    return {
        logIn: logIn,
        logOut: logOut,
        loadUserData: loadUserData
    };

    function logIn(username, password) {
        var data = "grant_type=password&username=" + username + "&password=" + password;
        var deferred = $q.defer();

        $http.post(serviceRoot + 'token', data, { headers: { 'Content-Type': 'application/x-www-form-urlencoded' } }).success(function (response) {
            $localStorage.authorizationData = { token: response.access_token, userName: username };
            deferred.resolve(true);
        }).error(function (err, status) {
            logOut();
            deferred.reject(false);
        });

        return deferred.promise;
    }

    function logOut() {
        if (window.localStorage) {
            delete window.localStorage['ngStorage-navigationItems'];
            delete window.localStorage['ngStorage-country'];
            delete window.localStorage['ngStorage-organization'];
            delete window.localStorage['ngStorage-authorizationData'];
        }
    }

    function loadUserData() {
        var deferred = $q.defer();
        $http.get(serviceRoot + 'api/Account/Me')
        .then(function (response) {
            $rootScope.currentUser = response.data;

            $rootScope.organization = $rootScope.currentUser.Organization;
            $localStorage.organization = $rootScope.currentUser.Organization;
            var countries = $rootScope.currentUser.Countries.map(function (c) {
                return c.Country;
            });

            if (!$localStorage.country) {
                $localStorage.country = countries[0];
            }

            $rootScope.country = $localStorage.country;

            if ($rootScope.currentUser.Countries.length > 1) {
                $rootScope.availableCountries = countries;
            }
            else {
                $rootScope.availableCountries = false;
            }

            if (!$localStorage.navigationItems) {
                $http.get(serviceRoot + 'Home/NavigationItems').then(function (response) {
                    $localStorage.navigationItems = response.data;
                    $rootScope.navigationItems = response.data;

                    deferred.resolve();
                });
            } else {
                $rootScope.navigationItems = $localStorage.navigationItems;

                deferred.resolve();
            }
        })
        .catch(function () {
            console.log(arguments);
            deferred.reject(arguments);
        });

        return deferred.promise;
    }
})

.factory('authInterceptorService', ['$q', '$injector', '$location', '$localStorage', function AuthInterceptor($q, $injector, $location, $localStorage) {

    var authInterceptorServiceFactory = {};

    var _request = function (config) {

        config.headers = config.headers || {};

        var authData = $localStorage.authorizationData;
        if (authData) {
            config.headers.Authorization = 'Token ' + authData.token;
        }

        return config;
    };

    var _responseError = function (rejection) {
        if (rejection.status === 401) {
            var authService = $injector.get('authService');

            authService.logOut();
            $location.path('/login');
        }
        return $q.reject(rejection);
    };

    authInterceptorServiceFactory.request = _request;
    authInterceptorServiceFactory.responseError = _responseError;

    return authInterceptorServiceFactory;
}])
;
