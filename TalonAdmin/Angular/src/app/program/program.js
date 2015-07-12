angular.module('talon.program', [
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
    .state('programs', {
        url: '/program',
        views: {
            "app": {
                template: "<div ui-view></div>"
            }
        },
        abstract: true,
        data: {
            settings: {
                collectionType: "Programs",
                entityType: 'Program',
                expand: ['distributions'],
                form: 'program/form.tpl.html'
            }
        }
    })

    .state('programs.list', {
        url: '/index',
        controller: 'GenericListCtrl as vm',
        templateUrl: 'index.tpl.html',
        data: {
            pageTitle: 'programs',
            settings: {
                columns: [
                    ['id', '#'],
                    ['title', 'Title']
                ]
            }
        }
    })

    .state('programs.edit', {
        url: '/{id:int}',
        controller: 'GenericEditCtrl as vm',
        templateUrl: 'edit.tpl.html',
        data: {
            pageTitle: 'programs'
        }
    })

    .state('programs.create', {
        url: '/create',
        controller: 'GenericEditCtrl as vm',
        templateUrl: 'create.tpl.html',
        data: {
            pageTitle: 'programs'
        }
    });
})

;
