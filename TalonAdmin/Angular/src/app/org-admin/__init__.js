angular.module('talon.org-admin', [
  'ui.router',
  'datatables',
  'datatables.bootstrap',
  'talon.dataAccess',
  'talon.common',
  'dialogs.main',
  'ngFileUpload'
])
.config(function config($stateProvider) {
    $stateProvider
    .state('org-admin', {
        url: '/org-admin',
        views: {
            "app": {
                template: "<div ui-view></div>"
            }
        },
        abstract: true,
    });
})
;