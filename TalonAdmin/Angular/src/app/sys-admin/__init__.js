angular.module('talon.sys-admin', [
  'ui.router',
  'datatables',
  'datatables.bootstrap',
  'talon.dataAccess',
  'talon.common',
  'dialogs.main',
  'ngFileUpload'
].concat(ALL_IMPORTS))
.config(function config($stateProvider) {
    $stateProvider
    .state('sys-admin', {
        url: '/sys-admin',
        views: {
            "app": {
                template: "<div ui-view></div>"
            }
        },
        abstract: true,
    });
})
;