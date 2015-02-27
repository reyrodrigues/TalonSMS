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
  'oc.lazyLoad',
  'pascalprecht.translate',
  'LocalStorageModule',
  'dialogs.default-translations',
  'dialogs.main',
  'toaster'
]);


var serviceBase = '/emergencyvouchermanagement/';
app.constant('ngAuthSettings', {
    apiServiceBaseUri: serviceBase,
    clientId: 'ngAuthApp'
});

app.constant('serviceBase', serviceBase);

app.config(['$httpProvider', function($httpProvider) {

  $httpProvider.interceptors.push('authInterceptorService');
}]);

app.run(['authService', 'breeze', function(authService, breeze) {
    authService.fillAuthData();



}]);
