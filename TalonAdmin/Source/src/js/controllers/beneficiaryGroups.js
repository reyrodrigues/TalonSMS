'use strict';

app.controller('BeneficiaryGroupsEditCtrl', ['$scope', 'settings', 'gettext', 'ControllerFactory',
    function ($scope, settings, gettext, ControllerFactory) {
        ControllerFactory.Edit($scope, settings);

        ControllerFactory.ChildGrid($scope, {
            collectionType: 'Beneficiaries',
            key: 'GroupId',
            expand: ['Location', "Group"],
            columns: [
                ["FirstName", gettext("Name"), '<a href ui-sref="beneficiaries.edit({ id: row.getProperty(\'Id\') })">{{ row.getProperty(\'Name\')}}</a>'],
                ["BirthYear", gettext("Birth Year"), '<a href ui-sref="beneficiaries.edit({ id: row.getProperty(\'Id\') })">{{COL_FIELD}}</a>'],
                ["NationalId", gettext("National Id Number")],
                ["MobileNumber", gettext("Mobile Number")],
                ["Location.Name", gettext("Location")]
            ]
        });

        $scope.loadData()
            .then(function () {
                $scope.BeneficiariesLoadGrid();
            });
}]);

