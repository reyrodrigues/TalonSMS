'use strict';

app.controller('CountryUsersListCtrl', ['$scope', 'settings', '$http', 'ControllerFactory', 'gettext', 'dialogs', 'toaster', 'adminBackendService', 'injectorHelper',
function ($scope, settings, $http, ControllerFactory, gettext, dialogs, toaster, adminBackendService, injectorHelper) {
    settings.backendService = adminBackendService;
    settings.columns = [
        ["FullName", gettext("Full Name"), '<a href ui-sref="' +  settings.editState + '({ id: row.getProperty(\'Id\') })">{{COL_FIELD}}</a>'],
        ["Email", gettext("Email")],
        ["_Countries", gettext("Countries"), null, false],
        ["_Role", gettext("Role"), null, false]
    ];
    settings.expand = ['Organization', 'Countries', 'Countries.Country', 'Roles'];

    ControllerFactory.List($scope, settings);

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

app.controller('EditCurrentUserCtrl', ['$rootScope', '$scope', 'settings', '$http', '$state', 'serviceBase', '$q', 'toaster', 'gettext', 'injectorHelper', 'countries',
function ($rootScope, $scope, settings, $http, $state, serviceBase, $q, toaster, gettext, injectorHelper, countries) {
    injectorHelper.injectPromises($scope, ['organizations', 'countries']);
    $scope.password = {};

    $rootScope.$watch('currentUser', function () {
        $scope.user = $rootScope.currentUser;
        if ($scope.user) {
            $scope.userCountries = $scope.user.Countries.map(function (c) {
                return c.Country.Id;
            });
        }
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
