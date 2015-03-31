'use strict';

var app = angular.module('app', [
  'ngAnimate',
  'ngCookies',
  'ngResource',
  'ngSanitize',
  'ngTouch',
  'ngStorage',
  'ngGrid',
  'ui.router',
  'ui.bootstrap',
  'ui.utils',
  'ui.load',
  'ui.jq',
  'breeze.angular',
  'LocalStorageModule',
  'dialogs.main',
  'toaster',
  'SignalR',
  'gettext',
  'angularFileUpload',
  'cgBusy',
  'angular.filter'
]);

app.constant('ngAuthSettings', {
    apiServiceBaseUri: window.BaseUrl,
    clientId: 'ngAuthApp'
});

app.constant('serviceBase', window.BaseUrl);

app.config(['$httpProvider', function ($httpProvider) {
    $httpProvider.interceptors.push('authInterceptorService');
    $httpProvider.interceptors.push('multiTenantInterceptorService');
}]);


app.run(['authService', 'breeze', function(authService, breeze) {
    authService.fillAuthData();
}]);
