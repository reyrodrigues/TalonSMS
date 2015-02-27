'use strict';

/* Controllers */

angular.module('app')
  .controller('AppCtrl', ['$scope', '$translate', '$localStorage', '$window', '$q', '$http', 'authService', 'ngAuthSettings', '$state', '$rootScope', 'localStorageService',
function ($scope, $translate, $localStorage, $window, $q, $http, authService, ngAuthSettings, $state, $rootScope, localStorageService) {
    // add 'ie' classes to html
    var isIE = !!navigator.userAgent.match(/MSIE/i);
    isIE && angular.element($window.document.body).addClass('ie');
    isSmartDevice($window) && angular.element($window.document.body).addClass('smart');



    $rootScope.$on('app:authenticated', function () {
        $http.get(ngAuthSettings.apiServiceBaseUri + 'api/Account/UserInfo')
        .then(function (userInfo) {
            $rootScope.navigationMenu = window.NavigationMenu(userInfo.data);
            $rootScope.me = userInfo.data;

            //$state.go('app.dashboard');

        });
    });

    $rootScope.$on('$stateChangeStart', function (event, toState, toParams, fromState, fromParams) {
        var authData = localStorageService.get('authorizationData');
        if (authData) {
            $rootScope.$emit('app:authenticated');
        }

        if (!authData && !toState.allowAnonymous) {
            event.preventDefault();
            $state.go('access.signin');
        }
    });
    // config
    $scope.app = {
        name: 'EVM',
        fullName: 'Emergency Voucher Management',
        version: '0.0.1',
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
            container: false,
            baseUrl: 'http://localhost:1099/emergencyvouchermanagement',
            restUrl: 'http://localhost:1099/emergencyvouchermanagement/api',
            odataUrl: 'http://localhost:1099/emergencyvouchermanagement/odata'
        }
    }

    $rootScope.logOut = function () {
        authService.logOut();
        $state.go('access.signin');
    };

    // save settings to local storage
    if (angular.isDefined($localStorage.settings)) {
        $scope.app.settings = $localStorage.settings;
    } else {
        $localStorage.settings = $scope.app.settings;
    }
    $scope.$watch('app.settings', function () {
        if ($scope.app.settings.asideDock && $scope.app.settings.asideFixed) {
            // aside dock and fixed must set the header fixed.
            $scope.app.settings.headerFixed = true;
        }
        // save to local storage
        $localStorage.settings = $scope.app.settings;
    }, true);

    // angular translate
    $scope.lang = {
        isopen: false
    };
    $scope.langs = {
        en: 'English',
        ua: 'Ukrainian',
        ru: 'Russian'
    };
    $scope.selectLang = $scope.langs[$translate.proposedLanguage()] || "English";
    $scope.setLang = function (langKey, $event) {
        // set the current lang
        $scope.selectLang = $scope.langs[langKey];
        // You can change the language during runtime
        $translate.use(langKey);
        $scope.lang.isopen = !$scope.lang.isopen;
    };

    function isSmartDevice($window) {
        // Adapted from http://www.detectmobilebrowsers.com
        var ua = $window['navigator']['userAgent'] || $window['navigator']['vendor'] || $window['opera'];
        // Checks for iOs, Android, Blackberry, Opera Mini, and Windows mobile devices
        return (/iPhone|iPod|iPad|Silk|Android|BlackBerry|Opera Mini|IEMobile/).test(ua);
    }

}
  ]);
