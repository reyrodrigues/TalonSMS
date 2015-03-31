'use strict';

app.controller('CountryUserListCtrl', ['$scope', 'settings', '$http', 'listController', 'gettext', 'dialogs', 'toaster', 'adminBackendService', 'injectorHelper',
function ($scope, settings, $http, listController, gettext, dialogs, toaster, adminBackendService, injectorHelper) {
    $scope.genericSettings = settings;
    settings.backendService = adminBackendService;
    settings.columns = [
        ["FullName", gettext("Full Name"), '<div class="ngCellText" ng-class="col.colIndex()"><span ng-cell-text><a href ui-sref="' +
                                          settings.editState + '({ id: row.getProperty(\'Id\') })">{{COL_FIELD}}</a></span></div>'],
        ["Email", gettext("Email")],
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

app.controller('CurrentUserProfileCtrl', ['$rootScope', '$scope', 'settings', '$http', '$state', 'serviceBase', '$q', 'toaster', 'gettext', 'organizations', 'countries',
function ($rootScope, $scope, settings, $http, $state, serviceBase, $q, toaster, gettext, organizations, countries) {
    $scope.genericSettings = settings;
    $scope.organizations = organizations;
    $scope.countries = countries;
    $scope.user = $rootScope.currentUser;
    $scope.password = {};
    $scope.userCountries = $scope.user.Countries.map(function (c) {
        return c.Country.Id;
    });

    $scope.save = function () {
        var queue = [];
        if ($scope.password.OldPassword && $scope.password.NewPassword) {
            queue.push($http.post(serviceBase + 'api/Account/ChangePassword', $scope.password).then(function () {
                toaster.pop("success", gettext("Success"), gettext("Password changed successfully."));
                console.log(arguments);
            }).catch(function (error) {
                console.log(arguments);
                toaster.pop("error", gettext("Error"), error);
            }));
        }

        queue.push($http.post(serviceBase + 'api/Account/UpdateProfile', $scope.user).then(function () {
            toaster.pop("success", gettext("Success"), gettext("Account updated successfully."));
        }).catch(function (error) {
            console.log(arguments);
            toaster.pop("error", gettext("Error"), error);
        }));

        $q.all(queue).then(function () {
            $state.go("app.dashboard");
        });
    };
}]);
