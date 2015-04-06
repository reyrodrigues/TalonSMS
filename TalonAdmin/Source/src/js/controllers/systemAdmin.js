'use strict';

app.controller('SystemAdminUsersListCtrl', ['$scope', 'settings', '$http', 'listController', 'gettext', 'dialogs', 'toaster', 'adminBackendService', 'injectorHelper',
function ($scope, settings, $http, listController, gettext, dialogs, toaster, adminBackendService, injectorHelper) {
    $scope.genericSettings = settings;
    settings.backendService = adminBackendService;
    settings.columns = [
        ["FullName", gettext("Full Name"), '<div class="ngCellText" ng-class="col.colIndex()"><span ng-cell-text><a href ui-sref="' +
                                          settings.editState + '({ id: row.getProperty(\'Id\') })">{{COL_FIELD}}</a></span></div>'],
        ["Email", gettext("Email")],
        ["Organization.Name", gettext("Organization")],
        ["_Countries", gettext("Countries"), null, false],
        ["_Role", gettext("Role"), null, false]
    ];
    settings.expand = ['Organization', 'Countries', 'Countries.Country', 'Roles'];

    listController($scope, settings);
    injectorHelper.injectPromises($scope, ['organizations', 'countries', 'roles']).then(function () {
        settings.resultMap = function (r) {
            r._Countries = r.Countries.map(function (c) { return c.Country.Name; }).join(', ');
            r._Role = r.Roles.map(function (c) {
                var roles = $scope.roles.filter(function (r) { return r.Id == c.RoleId; });

                return roles.length ? roles.pop().Name : "";
            }).join(', ');

            return r;
        };


        $scope.loadGridData();
    });
}]);

app.controller('SystemAdminUsersEditCtrl',
    ['$scope', '$http', '$state', '$q', 'serviceBase', 'settings', 'adminBackendService', 'toaster', 'dialogs', 'gettext', 'injectorHelper',
function ($scope, $http, $state, $q, serviceBase, settings, adminBackendService, toaster, dialogs, gettext, injectorHelper) {
    injectorHelper.injectPromises($scope, ['organizations', 'countries', 'roles']);

    $scope.password = {};
    $scope.isEditing = false;

    var entityType = adminBackendService.metadataStore.getEntityType("ApplicationUser");

    var query = null
    if ($state.params.id) {
        $scope.isNew = false;
        query = new breeze.EntityQuery(settings.collectionType)
            .expand(["Countries", "Countries.Country", "Organization", "Roles"])
            .where("Id", "==", $state.params.id)
            .noTracking()
            .toType(entityType)
            .using(adminBackendService).execute();
    } else {
        $scope.isNew = true;
        var deferred = $q.defer();
        var entity = {
            Roles: [],
            Countries: [],
        };

        deferred.resolve({
            results: [entity]
        });
        query = deferred.promise;
    }


    $q.when(query).then(function (response) {
        $scope.user = response.results.pop();
        var roles = $scope.user.Roles;
        $scope.user.Role = roles.length ? roles.pop().RoleId : "";

        $scope.userCountries = $scope.user.Countries.map(function (c) {
            return c.Country.Id;
        });

        if (!$scope.$$phase) $scope.$apply();

    })
    .catch(function (error) {
        console.log(arguments);
        toaster.pop("error", gettext("Error"), error);
    });


    $scope.delete = function () {
        var dlg = dialogs.confirm(gettext("Confirm"), gettext("Are you sure you would like to delete this record? This operation cannot be reversed."));
        dlg.result.then(function () {
            $scope.isEditing = false;

            new breeze.EntityQuery("Users")
                .where("Id", "==", $state.params.id)
                .toType(entityType)
                .using(adminBackendService)
                .execute()
                .then(function (response) {
                    var entity = response.results.pop();
                    entity.entityAspect.setDeleted();

                    adminBackendService.saveChanges([entity]).then(function () {
                        toaster.pop('success', gettext('Success'), gettext('Record successfully deleted.'));
                        $state.go("system-admin.users.list");
                    }).catch(function (error) {
                        console.log(arguments);

                        toaster.pop('error', gettext('Error'), error);
                    });
                })
        });
    };

    $scope.save = function () {
        var queue = [];
        $scope.user.CountryIds = $scope.userCountries;

        if ($scope.password.OldPassword && $scope.password.NewPassword) {
            queue.push($http.post(serviceBase + 'api/Account/ChangePassword', $scope.password).then(function () {
                toaster.pop("success", gettext("Success"), gettext("Password changed successfully."));
                console.log(arguments);
            }).catch(function (error) {
                console.log(arguments);
                toaster.pop("error", gettext("Error"), error);
            }));
        }
        if ($scope.isNew) {
            $http.post(serviceBase + 'api/Account/RegisterAdministrator', $scope.user).then(function () {
                toaster.pop("success", gettext("Success"), gettext("Account created successfully."));
            }).catch(function (error) {
                console.log(arguments);
                toaster.pop("error", gettext("Error"), error);
            });
        } else {
            queue.push($http.post(serviceBase + 'api/Account/UpdateProfile', $scope.user).then(function () {
                toaster.pop("success", gettext("Success"), gettext("Account updated successfully."));
            }).catch(function (error) {
                console.log(arguments);
                toaster.pop("error", gettext("Error"), error);
            }));
        }

        $q.all(queue).then(function () {
            if ($scope.isNew) {
                $state.go("system-admin.users.list")
            }
            $scope.isEditing = false;
        });
    };

    $scope.startEditing = function () {
        $scope.isEditing = true;
    };

    $scope.endEditing = function () {
        $scope.isEditing = false;
    };
}]);

app.controller('SystemAdminCountriesEditCtrl', ['$scope', '$state', 'editController', 'gettext', 'subGrid', 'injectorHelper', 'settings', 'adminBackendService', 'toaster', 'dialogs',
    function ($scope, $state, editController, gettext, subGrid, injectorHelper, settings, adminBackendService, toaster, dialogs) {
        $scope.genericSettings = settings;
        $scope.isNew = !($state.params.id || false);

        editController($scope, angular.extend({
        }, settings));

        if ($scope.isNew) {
            $scope.entity = adminBackendService.createEntity("Country", { Settings: adminBackendService.createEntity("CountrySettings") });
            $scope.countrySettings = $scope.entity.Settings;
            $scope.countrySettings.SmsBackendType = 1;
            console.log($scope.entity, $scope.countrySettings);
        } else {
            $scope.loadData().then(function () {
                $scope.countrySettings = $scope.entity.Settings;
            });
        }


        $scope.delete = function () {
            var dlg = dialogs.confirm(gettext("Confirm"), gettext("Are you sure you would like to delete this record? This operation cannot be reversed."));
            dlg.result.then(function () {
                var entities = [$scope.entity, $scope.countrySettings];
                angular.forEach(entities, function (v, k) {
                    v.entityAspect.setDeleted();
                });

                $scope.isEditing = false;

                adminBackendService.saveChanges($scope.countrySettings.PropertyCollection.map(function (p) {
                    p.entityAspect.setDeleted();

                    return p;
                }))
                adminBackendService.saveChanges(entities).then(function () {
                    toaster.pop('success', gettext('Success'), gettext('Record successfully deleted.'));
                    $state.go(settings.listState);
                }).catch(function (error) {
                    console.log(arguments);

                    toaster.pop('error', gettext('Error'), error);
                });
            });
        };

        $scope.save = function (andContinue) {
            $scope.isEditing = false;

            angular.forEach($scope.countrySettings.Properties, function (v, k) {
                var properties = $scope.countrySettings.PropertyCollection.filter(function (p) { return p.Name == k });
                if (properties.length) {
                    properties[0].Value = v;
                } else {
                    var newItem = adminBackendService.createEntity("CountrySettingsProperty", { SettingsId: $scope.countrySettings.Id });
                    newItem.Name = k;
                    newItem.Value = v;

                    $scope.countrySettings.PropertyCollection.push(newItem);
                }
            });

            var entities = [$scope.entity, $scope.countrySettings].concat($scope.countrySettings.PropertyCollection);


            adminBackendService.saveChanges(entities).then(function (ne) {
                toaster.pop('success', gettext('Success'), gettext('Record successfully saved.'));
                if ($scope.isNew) {
                    $state.go(settings.editState, { id: $scope.entity.Id });
                } else {
                    if (!andContinue)
                        $state.go(settings.listState);
                    else
                        $scope.loadData();
                }
            }).catch(function (error) {
                console.log(arguments);

                toaster.pop('error', gettext('Error'), error);
            });
        };
    }]);

app.controller('OrganizationCountriesEditCtrl', ['$scope', '$state', 'editController', 'gettext', 'subGrid', 'injectorHelper', 'settings', 'adminBackendService', 'toaster', 'dialogs',
    function ($scope, $state, editController, gettext, subGrid, injectorHelper, settings, adminBackendService, toaster, dialogs) {
        $scope.genericSettings = settings;
        injectorHelper.injectPromises($scope, ['countries']);
        $scope.isNew = !($state.params.id || false);

        editController($scope, angular.extend({
        }, settings));


        if ($scope.isNew) {
            $scope.isEditing = true;
            $scope.entity = adminBackendService.createEntity("OrganizationCountry", {
                OrganizationId: $state.params.organizationId,
                Settings: adminBackendService.createEntity("CountrySettings")
            });
            $scope.countrySettings = $scope.entity.Settings;
            $scope.countrySettings.SmsBackendType = 1;
            console.log($scope.entity, $scope.countrySettings);
        } else {
            $scope.loadData().then(function () {
                $scope.countrySettings = $scope.entity.Settings;
            });
        }


        $scope.delete = function () {
            var dlg = dialogs.confirm(gettext("Confirm"), gettext("Are you sure you would like to delete this record? This operation cannot be reversed."));
            dlg.result.then(function () {
                var entities = [$scope.entity, $scope.countrySettings];
                angular.forEach(entities, function (v, k) {
                    v.entityAspect.setDeleted();
                });

                $scope.isEditing = false;

                adminBackendService.saveChanges($scope.countrySettings.PropertyCollection.map(function (p) {
                    p.entityAspect.setDeleted();

                    return p;
                }))
                adminBackendService.saveChanges(entities).then(function () {
                    toaster.pop('success', gettext('Success'), gettext('Record successfully deleted.'));
                    $state.go(settings.listState);
                }).catch(function (error) {
                    console.log(arguments);

                    toaster.pop('error', gettext('Error'), error);
                });
            });
        };

        $scope.save = function (andContinue) {
            $scope.isEditing = false;

            angular.forEach($scope.countrySettings.Properties, function (v, k) {
                var properties = $scope.countrySettings.PropertyCollection.filter(function (p) { return p.Name == k });
                if (properties.length) {
                    properties[0].Value = v;
                } else {
                    var newItem = adminBackendService.createEntity("CountrySettingsProperty", { SettingsId: $scope.countrySettings.Id });
                    newItem.Name = k;
                    newItem.Value = v;

                    $scope.countrySettings.PropertyCollection.push(newItem);
                }
            });

            var entities = [$scope.entity, $scope.countrySettings].concat($scope.countrySettings.PropertyCollection);


            adminBackendService.saveChanges(entities).then(function (ne) {
                toaster.pop('success', gettext('Success'), gettext('Record successfully saved.'));
                if ($scope.isNew) {
                    $state.go("system-admin.organizations.edit-country", { id: $scope.entity.Id, organizationId: $scope.entity.OrganizationId });
                } else {
                    if (!andContinue)
                        $state.go(settings.listState);
                    else
                        $scope.loadData();
                }
            }).catch(function (error) {
                console.log(arguments);

                toaster.pop('error', gettext('Error'), error);
            });
        };
    }]);

app.controller('OrganizationsEditCtrl', ['$scope', 'editController', 'gettext', 'subGrid', 'injectorHelper', 'settings', 'adminBackendService',
    function ($scope, editController, gettext, subGrid, injectorHelper, settings, adminBackendService) {
        injectorHelper.injectPromises($scope, ['countries']);
        $scope.genericSettings = settings;

        editController($scope, angular.extend({
            backendService: adminBackendService
        }, settings));


        subGrid($scope, {
            entityType: 'OrganizationCountry',
            collectionType: 'OrganizationCountries',
            key: 'OrganizationId',
            expand: ['Country', 'Organization'],
            backendService: adminBackendService,
            columns: [
                ["Country.Name", gettext("Country"), '<div class="ngCellText" ng-class="col.colIndex()"><span ng-cell-text><a href ui-sref="system-admin.organizations.edit-country({ organizationId: entity.Id, id: row.getProperty(\'Id\') })">{{COL_FIELD}}</a></span></div>']
            ]
        });

        $scope.loadData().then(function () {

            $scope.OrganizationCountriesLoadGrid();
        });
    }]);
