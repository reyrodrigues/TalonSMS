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
  'talon.vendor-type',
  'talon.reporting'
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

        // NOT KOSHER, BUT MAYBE THE ONLY WAY TO DEAL WITH THIS 
        // TODO: refactor
        $(window).scrollTop(0);
    });

    $scope.$on('$stateChangeStart', function (event, toState, toParams, fromState, fromParams) {
        var def = $q.defer();
        $rootScope.authPromise = def.promise;

        var authData = $localStorage.authorizationData;
        if (authData && !$rootScope.currentUser) {
            authService.loadUserData().then(function () {
                $scope.isLoggedIn = true;
                def.resolve();
            }).catch(function () {
                delete $localStorage.authorizationData;
                $state.go('login', { location: 'replace' });
            });
        }

        if (authData) {
            $rootScope.token = $localStorage.authorizationData.token;
            def.resolve();
        }

        if (!authData && !toState.data.allowAnonymous) {
            event.preventDefault();

            $scope.isLoggedIn = false;
            $state.go('login', { location: 'replace' });
            def.reject();
        }

        entityManagerFactory.entityManager().rejectChanges();
        entityManagerFactory.adminEntityManager().rejectChanges();
    });

    $scope.$on('app:authenticated', function () {
        $scope.isLoggedIn = true;
    });

    $scope.logOut = logOut;
    $scope.$state = $state;
    $rootScope.canI = CanI;

    $scope.selectCountry = function (country) {
        if ($rootScope.country.Id != country.Id) {
            $localStorage.country = country;
            $rootScope.country = country;

            $state.transitionTo('dashboard', {}, { reload: true });
        }
    };

    $rootScope.app = {
        name: 'Talon',
        currentYear: moment().year(),
        fullName: 'Talon',
        version: '1.0.0b',
        serviceRoot: serviceRoot,
        timezoneOffset: moment().utcOffset() / 60,
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

    function CanI(action) {
        if (!$rootScope.currentUser && !$rootScope.currentUser.AvailableActions) {
            return false;
        }

        if ($rootScope.currentUser.IsSystemAdministrator) {
            //return true;
        }

        var result = $rootScope.currentUser.AvailableActions.filter(function (a) { return a.Name == action; }).length > 0;

        return result;
    }

    function logOut() {
        $scope.isLoggedIn = false;
        delete $localStorage.authorizationData;

        $state.go('login', { location: 'replace' });
    }
})

;