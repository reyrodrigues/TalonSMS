angular.module('talon.country-admin')

.config(function config($stateProvider) {
    $stateProvider
    .state('country-admin.locations', {
        url: '/location',
        template: "<div ui-view></div>",
        abstract: true,
        data: {
            settings: {
                collectionType: "Locations",
                entityType: 'Location',
                form: 'country-admin/locations/form.tpl.html'
            }
        }
    })

    .state('country-admin.locations.list', {
        url: '/index',
        controller: 'GenericListCtrl as vm',
        templateUrl: 'index.tpl.html',
        data: {
            pageTitle: 'Locations',
            settings: {
                columns: [
                    ['id', '#'],
                    ['name', 'Name']
                ]
            }
        }
    })

    .state('country-admin.locations.edit', {
        url: '/{id:int}',
        controller: 'GenericEditCtrl as vm',
        templateUrl: 'edit.tpl.html',
        data: {
            pageTitle: 'Locations'
        }
    })

    .state('country-admin.locations.create', {
        url: '/create',
        controller: 'GenericEditCtrl as vm',
        templateUrl: 'create.tpl.html',
        data: {
            pageTitle: 'Locations'
        }
    });
})

;
