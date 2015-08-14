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
    var gettext = this.$injector.get('gettext');
    var toaster = this.$injector.get('toaster');
    var dialogs = this.$injector.get('dialogs');
    var self = this;

    this.actions = [
       {
           label: gettext("Import Vendor Data"),
           css: "btn-default",
           condition: function () { return $rootScope.canI('Import Vendor Data'); },
           action: function action() {
               var dlg = dialogs.create('org-admin/country-settings/upload-vendor-data.tpl.html', UploadVendorDataController);
               dlg.result.then(function (result) {
                   if (result) {
                       toaster.pop('success', gettext('Success'), gettext('Data successfuly imported.'));
                   }
               }).catch(function (res) {
                   toaster.pop('error', gettext('Error'), res.data);
               });

           }
       }];
    this.forms = [
        {
            label: gettext("Export Card Loads"),
            condition: function () { return $rootScope.canI('Export CardLoads'); },
            css: "btn-info",
            countryId: function () { return self.entity.country ? self.entity.country.id : null; },
            url: function () {
                return serviceRoot + 'api/App/MobileClient/DownloadOfflinePayload';
            }
        }
    ];

    function UploadVendorDataController($scope, $modalInstance, Upload) {
        $scope.files = [];
        $scope.fileNames = "";

        $scope.$watchCollection('files', function () {
            var files = $scope.files;
            $scope.fileNames = (files && files.length) ? files.map(function (f) { return f.name; }).join(',') : "No file selected";
        });

        $scope.upload = function () {
            $scope.uploading = Upload.upload({
                url: serviceRoot + 'api/App/MobileClient/UploadVendorPayload',
                file: $scope.files
            }).then(function (result) {
                $modalInstance.close(result.data);
            }).catch(function () {
            });
        };

        $scope.close = function () {
            $modalInstance.close(false);
        };
    }
};

function CountrySettingsController($injector, $scope) {
    EditController.call(this, $injector, $scope);
}