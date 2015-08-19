angular.module('talon', [
  'templates-app',
  'templates-common',
  'ui.router',
  'ui.bootstrap',
  'ngStorage',
  'toaster',
  'ngAnimate',
  'ngMessages',
  'blockUI',
  'gettext',
  'SignalR',

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

.run(function run(gettextCatalog) {
    gettextCatalog.setCurrentLanguage('en');
})
.controller('AppCtrl', function AppCtrl($scope, $location, $localStorage, $http, $state, $rootScope, $q,
    $timeout, toaster, entityManagerFactory, authService, Hub) {
    $scope.$on('$stateChangeSuccess', function (event, toState, toParams, fromState, fromParams) {
        if (angular.isDefined(toState.data.pageTitle)) {
            $scope.pageTitle = 'Talon | ' + toState.data.pageTitle;
        }

        // NOT KOSHER, BUT MAYBE THE ONLY WAY TO DEAL WITH THIS 
        // TODO: refactor
        $timeout(function () {
            $(window).scrollTop(0);
        }, 100);
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
        version: '1.2.0b',
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


    if (!window.dashboardHub) {
        window.dashboardHub = new Hub('dashboardHub', {
            //client side methods
            listeners: {
                'message': function (type, title, message) {
                    toaster.pop(type, title, message);
                },
                'updateDashboard': function () {
                    if (window.loadDashboard) {
                        $timeout(function () {
                            window.loadDashboard();
                        });
                        $rootScope.$apply();
                    }
                }
            },
            methods: [],
            //handle connection error
            errorHandler: function (error) {
                console.error(error);
            },
            //specify a non default root
            rootPath: serviceRoot + 'signalR/hubs/',

            hubDisconnected: function () {
                if (window.dashboardHub.connection.lastError) {
                    window.dashboardHub.connection.start()
                    .done(function () {
                        if (window.dashboardHub.connection.state === 0) {
                            $timeout(function () { }, 2000);
                        }
                        else {
                        }
                    })
                    .fail(function (reason) {
                        console.log(reason);
                    });
                }
            }
        });
    }

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

        $localStorage.$reset();
        location.reload();
    }
})

;
