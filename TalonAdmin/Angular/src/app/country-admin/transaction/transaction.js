angular.module('talon.country-admin')

.config(function config($stateProvider) {
    $stateProvider
    .state('country-admin.transaction', {
        url: '/transaction',
        template: "<div ui-view></div>",
        abstract: true,
        data: {
        }
    })


    ;
})

;
