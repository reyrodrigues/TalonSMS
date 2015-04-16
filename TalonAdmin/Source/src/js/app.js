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
}])

app.run(['$templateCache', '$interpolate', function ($templateCache, $interpolate) {

	    // get interpolation symbol (possible that someone may have changed it in their application instead of using '{{}}')
	    var startSym = $interpolate.startSymbol();
	    var endSym = $interpolate.endSymbol();

	    $templateCache.put('/dialogs/error.html', '<div class="modal-header dialog-header-error"><button type="button" class="close" ng-click="close()">&times;</button><h4 class="modal-title text-danger"><span class="glyphicon glyphicon-warning-sign"></span> <span ng-bind-html="header"></span></h4></div><div class="modal-body text-danger" ng-bind-html="msg"></div><div class="modal-footer"><button type="button" class="btn btn-default" ng-click="close()">' + startSym + '"Close" | translate' + endSym + '</button></div>');
	    $templateCache.put('/dialogs/wait.html', '<div class="modal-header dialog-header-wait"><h4 class="modal-title"><span class="glyphicon glyphicon-time"></span> ' + startSym + 'header' + endSym + '</h4></div><div class="modal-body"><p ng-bind-html="msg"></p><div class="progress progress-striped active"><div class="progress-bar progress-bar-info" ng-style="getProgress()"></div><span class="sr-only">' + startSym + 'progress' + endSym + '' + startSym + '"Percent Complete" | translate' + endSym + '</span></div></div>');
	    $templateCache.put('/dialogs/notify.html', '<div class="modal-header dialog-header-notify"><button type="button" class="close" ng-click="close()" class="pull-right">&times;</button><h4 class="modal-title text-info"><span class="glyphicon glyphicon-info-sign"></span> ' + startSym + 'header' + endSym + '</h4></div><div class="modal-body text-info" ng-bind-html="msg"></div><div class="modal-footer"><button type="button" class="btn btn-primary" ng-click="close()">' + startSym + '"Ok" | translate' + endSym + '</button></div>');
	    $templateCache.put('/dialogs/confirm.html', '<div class="modal-header dialog-header-confirm"><button type="button" class="close" ng-click="no()">&times;</button><h4 class="modal-title"><span class="glyphicon glyphicon-check"></span> ' + startSym + 'header' + endSym + '</h4></div><div class="modal-body" ng-bind-html="msg"></div><div class="modal-footer"><button type="button" class="btn btn-default" ng-click="yes()">' + startSym + '"Yes" | translate' + endSym + '</button><button type="button" class="btn btn-primary" ng-click="no()">' + startSym + '"No" | translate' + endSym + '</button></div>');
	}]);;
