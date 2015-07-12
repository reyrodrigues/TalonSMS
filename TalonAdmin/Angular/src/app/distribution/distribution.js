angular.module('talon.distribution', [
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
    .state('distributions', {
        url: '/distribution',
        views: {
            "app": {
                template: "<div ui-view></div>"
            }
        },
        abstract: true,
        data: {
            settings: {
                collectionType: "Distributions",
                entityType: 'Distribution',
                expand: ['vouchers'],
                form: 'distribution/form.tpl.html'
            }
        }
    })

    .state('distributions.list', {
        url: '/index',
        controller: 'GenericListCtrl as vm',
        templateUrl: 'index.tpl.html',
        data: {
            pageTitle: 'Distributions',
            settings: {
                columns: [
                    ['id', '#'],
                    ['title', 'Title']
                ]
            }
        }
    })

    .state('distributions.edit', {
        url: '/{id:int}',
        controller: 'GenericEditCtrl as vm',
        templateUrl: 'edit.tpl.html',
        data: {
            pageTitle: 'Distributions'
        }
    })

    .state('distributions.create', {
        url: '/create',
        controller: 'GenericEditCtrl as vm',
        templateUrl: 'create.tpl.html',
        data: {
            pageTitle: 'Distributions'
        }
    });
})

;
