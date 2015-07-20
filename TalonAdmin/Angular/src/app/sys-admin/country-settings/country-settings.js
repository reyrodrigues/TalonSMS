angular.module('talon.sys-admin')
.config(function config($stateProvider) {
    $stateProvider
    .state('sys-admin.countries', {
        url: '/country',
        template: "<div ui-view></div>",
        abstract: true,
        data: {
            settings: {
                entityManager: 'adminEntityManager',
                collectionType: "Countries",
                entityType: 'Country',
                form: 'sys-admin/country-settings/form.tpl.html',
                controlledLists: ['organizations', 'smsBackends'],
                expand: ['settings', 'settings.propertyCollection'],
                defaults: {
                }
            }
        }
    })

    .state('sys-admin.countries.list', {
        url: '/index',
        controller: 'GenericListCtrl as vm',
        templateUrl: 'index.tpl.html',
        data: {
            pageTitle: 'Countries',
            settings: {
                columns: [
                    ['id', '#'],
                    ['name', 'Name']
                ]
            }
        }
    })

    .state('sys-admin.countries.create', {
        url: '/create',
        controller: 'SACountrySettingsController as vm',
        templateUrl: 'create.tpl.html',
        data: {
            pageTitle: 'Countries'
        }
    })
    .state('sys-admin.countries.edit', {
        url: '/{id}',
        controller: 'SACountrySettingsController as vm',
        templateUrl: 'edit.tpl.html',
        data: {
            pageTitle: 'Countries',
            settings: {
            }
        }
    })
    ;

})
.controller('SACountrySettingsController', SACountrySettingsController)
;
SACountrySettingsController.prototype.getEntities = function () {
    var self = this;
    var entityManager = this.$injector.get('entityManagerFactory').adminEntityManager();
    if (self.isNew) {
        self.entity.settings = entityManager.createEntity("CountrySettings");
    }

    angular.forEach(self.entity.settings.properties, function (v, k) {
        var properties = self.entity.settings.propertyCollection.filter(function (p) { return p.name == k; });
        if (properties.length) {
            properties[0].value = v;
        } else {
            var newItem = entityManager.createEntity("CountrySettingsProperty", {
                settingsId: self.entity.settings.Id,
                name: k,
                value: v
            });

            self.entity.settings.propertyCollection.push(newItem);
        }
    });

    var entities = [self.entity, self.entity.settings].concat(self.entity.settings.propertyCollection);

    return entities;
};

function SACountrySettingsController($injector, $scope) {
    EditController.call(this, $injector, $scope);
}