angular.module('talon.org-admin')
.config(function config($stateProvider) {
    $stateProvider
    .state('org-admin.country-settings', {
        url: '/country-settings',
        template: "<div ui-view></div>",
        abstract: true,
        data: {
            settings: {
                entityManager: 'adminEntityManager',
                collectionType: "OrganizationCountries",
                entityType: 'OrganizationCountry',
                form: 'org-admin/country-settings/form.tpl.html',
                controlledLists: ['organizations', 'smsBackends'],
                expand: ['settings', 'settings.propertyCollection', 'country'],
                defaults: {
                }
            }
        }
    })

    .state('org-admin.country-settings.list', {
        url: '/index',
        controller: 'GenericListCtrl as vm',
        templateUrl: 'index.tpl.html',
        data: {
            pageTitle: 'Countries',
            settings: {
                columns: [
                    ['id', '#'],
                    ['country.name', 'Name']
                ]
            }
        }
    })

    .state('org-admin.country-settings.create', {
        url: '/create',
        controller: 'GenericEditCtrl as vm',
        templateUrl: 'create.tpl.html',
        data: {
            pageTitle: 'Countries'
        }
    })
    .state('org-admin.country-settings.edit', {
        url: '/{id}',
        controller: 'CountrySettingsController as vm',
        templateUrl: 'edit.tpl.html',
        data: {
            pageTitle: 'Countries',
            settings: {
            }
        }
    })
    ;

})
.controller('CountrySettingsController', CountrySettingsController)
;
CountrySettingsController.prototype.getEntities = function () {
    var self = this;
    var entityManager = this.$injector.get('entityManagerFactory').adminEntityManager();

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

CountrySettingsController.prototype.configure = function () {
    var $rootScope = this.$injector.get('$rootScope');
    var $localStorage = this.$injector.get('$localStorage');
    var self = this;

    this.forms = [
        {
            label: "Export Card Loads",
            condition: function () { return $rootScope.canI('Export CardLoads'); },
            css: "btn-info",
            countryId: function () { return self.entity.country ? self.entity.country.id : null; },
            url: function () {
                return serviceRoot + 'api/App/MobileClient/DownloadOfflinePayload';
            }
        }
    ];
};

function CountrySettingsController($injector, $scope) {
    EditController.call(this, $injector, $scope);
}