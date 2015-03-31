﻿'use strict';

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

    var enitityType = adminBackendService.metadataStore.getEntityType("ApplicationUser");

    var query = null
    if ($state.params.id) {
        $scope.isNew = false;
        query = new breeze.EntityQuery(settings.collectionType)
            .expand(["Countries", "Countries.Country", "Organization", "Roles"])
            .where("Id", "==", $state.params.id)
            .noTracking()
            .toType(enitityType)
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
                .toType(enitityType)
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