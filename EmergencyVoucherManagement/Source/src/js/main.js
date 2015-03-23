'use strict';

/* Controllers */

angular.module('app')
  .controller('AppCtrl',
  ['$scope', '$localStorage', '$window', '$q', '$http', 'authService', 'ngAuthSettings', '$state', '$rootScope', 'localStorageService', 'gettext', 'gettextCatalog',
function ($scope, $localStorage, $window, $q, $http, authService, ngAuthSettings, $state, $rootScope, localStorageService, gettext, gettextCatalog) {
    // add 'ie' classes to html
    var isIE = !!navigator.userAgent.match(/MSIE/i);
    isIE && angular.element($window.document.body).addClass('ie');
    isSmartDevice($window) && angular.element($window.document.body).addClass('smart');


    $rootScope.$on('app:authenticated', function () {
        $http.get(ngAuthSettings.apiServiceBaseUri + 'api/Account/UserInfo')
        .then(function (userInfo) {
            $rootScope.me = userInfo.data;
            $rootScope.myOrganization = $rootScope.me.Organization;
            $localStorage.organization = $rootScope.me.Organization;
            if (!$localStorage.selectedCountry) {
                $localStorage.selectedCountry =  $rootScope.me.Countries[0];
            }
            $rootScope.myCountry = $localStorage.selectedCountry;

            if ($rootScope.me.Countries.length > 1)
                $rootScope.availableCountries = $rootScope.me.Countries;
            else
                $rootScope.availableCountries = false;
        });
    });

    $scope.selectCountry = function (country) {
        if ($rootScope.myCountry.Id != country.Id) {
            $localStorage.selectedCountry = country
            $rootScope.myCountry = country;

            $state.transitionTo('app.dashboard', {}, { reload: true });
        }
    };

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
        name: 'TalonSMS',
        currentYear: moment().year(),
        fullName: 'TalonSMS',
        version: '1.0.0b',
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
        en_US: gettext('English'),
        ua: gettext('Ukrainian'),
        ru: gettext('Russian'),
        pt_BR: gettext('Portuguese')
    };

    gettextCatalog.debug = false;
    gettextCatalog.setCurrentLanguage($scope.app.settings.selectedLanguage);

    $scope.selectLang = $scope.langs[$scope.app.settings.selectedLanguage] || "English";
    $scope.setLang = function (langKey, $event) {
        $scope.app.settings.selectedLanguage = langKey;
        $localStorage.settings.selectedLanguage = langKey;
        gettextCatalog.setCurrentLanguage(langKey);

        // set the current lang
        $scope.selectLang = $scope.langs[langKey];
        // You can change the language during runtime
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
