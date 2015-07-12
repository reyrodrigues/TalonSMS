angular.module('talon', [
  'templates-app',
  'templates-common',
  'ui.router',
  'ui.bootstrap',
  'ngStorage',
  'toaster',
  'ngAnimate',
  'blockUI',

  // modules
  'talon.auth',
  'talon.dataAccess',
  'talon.multitenant',
  'talon.common',

  // Pages
  'talon.dashboard',
  'talon.login',
  'talon.sys-admin',
  'talon.org-admin',
  'talon.country-admin',
  'talon.group',
  'talon.beneficiary',
  'talon.distribution',
  'talon.program',
  'talon.vendor',
  'talon.vendorType'
])
.config(function myAppConfig($stateProvider, $urlRouterProvider, $httpProvider) {
    $urlRouterProvider.otherwise('/dashboard');
    $httpProvider.interceptors.push('authInterceptorService');
    $httpProvider.interceptors.push('multiTenantInterceptorService');

})

.run(function run() {
})
.controller('AppCtrl', function AppCtrl($scope, $location, $localStorage, $http, $state, $rootScope, $q, entityManagerFactory, authService) {
    $scope.$on('$stateChangeSuccess', function (event, toState, toParams, fromState, fromParams) {
        if (angular.isDefined(toState.data.pageTitle)) {
            $scope.pageTitle = 'Talon | ' + toState.data.pageTitle;
        }
    });

    $scope.$on('$stateChangeStart', function (event, toState, toParams, fromState, fromParams) {
        var authData = $localStorage.authorizationData;
        if (authData && !$rootScope.currentUser) {
            $rootScope.authPromise = authService.loadUserData().then(function () {
                $scope.isLoggedIn = true;
            }).catch(function () {
                delete $localStorage.authorizationData;
                $state.go('login', { location: 'replace' });
            });
        }

        if (authData) {
            $rootScope.token = $localStorage.authorizationData.token;
            var def = $q.defer();
            def.resolve();
            $rootScope.authPromise = def.promise;
        }

        if (!authData && !toState.data.allowAnonymous) {
            event.preventDefault();

            $scope.isLoggedIn = false;
            $state.go('login', { location: 'replace' });
        }

        var entityManager = entityManagerFactory.entityManager();
        entityManager.rejectChanges();

    });

    $scope.$on('app:authenticated', function () {
        $scope.isLoggedIn = true;
    });

    $scope.logOut = logOut;
    $scope.$state = $state;


    $scope.selectCountry = function (country) {
        if ($rootScope.country.Id != country.Id) {
            $localStorage.country = country;
            $rootScope.country = country;

            $state.transitionTo('dashboard', {}, { reload: true });
        }
    };


    $scope.app = {
        name: 'Talon',
        currentYear: moment().year(),
        fullName: 'Talon',
        version: '1.0.0b',
        serviceRoot: serviceRoot,
        // for chart colors
        color: {
            primary: '#7266ba',
            info: '#23b7e5',
            success: '#27c24c',
            warning: '#fad733',
            danger: '#f05050',
            light: '#e8eff0',
            dark: '#3a3f51',
            black: '#1c2b36'
        },
        settings: {
            themeID: 1,
            navbarHeaderColor: 'bg-black',
            navbarCollapseColor: 'bg-white-only',
            asideColor: 'bg-black',
            headerFixed: true,
            asideFixed: false,
            asideFolded: false,
            asideDock: false,
            container: false
        }
    };


    function logOut() {
        $scope.isLoggedIn = false;
        delete $localStorage.authorizationData;

        $state.go('login', { location: 'replace' });
    }
})

;