'use strict';


app.controller('VendorsCreateCtrl', ['$scope', 'createController', 'settings', 'injectorHelper',
    function ($scope, createController, settings, injectorHelper) {
        injectorHelper.injectPromises($scope, ['locations', 'vendorTypes']);

        createController($scope, settings);
    }]);

app.controller('VendorsEditCtrl', ['$scope', 'editController', 'gettext', 'subGrid', 'settings', 'injectorHelper', 'backendService',
    function ($scope, editController, gettext, subGrid, settings, injectorHelper, backendService) {
        injectorHelper.injectPromises($scope, ['locations', 'vendorTypes']);
        editController($scope, settings);

        subGrid($scope, {
            collectionType: 'VoucherTransactionRecords',
            key: 'VendorId',
            expand: ['Voucher', 'Beneficiary', 'Vendor'],
            columns: [
                ["Status", gettext("Status"), '<div class="ngCellText" ng-class="col.colIndex()"><span ng-cell-text>{{statusToString(COL_FIELD)}}</span></div>'],
                ["Beneficiary.Name", gettext("Beneficiary")],
                ["Voucher.VoucherCode", gettext("Voucher Code")],
                ["Voucher.Value", gettext("Value")]
            ]
        });

        $scope.loadData()
            .then(function () {
                $scope.VoucherTransactionRecordsLoadGrid();
            });
    }]);

app.controller('VendorsListCtrl', ['$scope', '$state', '$localStorage', 'listController', 'gettext', 'dialogs', 'toaster', 'serviceBase', '$location',
function ($scope, $state, $localStorage, listController, gettext, dialogs, toaster, serviceBase, $location) {
    var storageSetting = $state.current.name + 'GridSettings';
    $scope.showingDisabled = false;

    listController($scope, {
        collectionType: 'Vendors',
        expand: ['Location'],
        columns: [
            ["Name", gettext("Name"), '<div class="ngCellText" ng-class="col.colIndex()"><span ng-cell-text><a href ui-sref="vendors.edit({ id: row.getProperty(\'Id\') })">{{COL_FIELD}}</a></span></div>'],
            ["MobileNumber", gettext("Mobile Number")],
            ["Location.Name", gettext("Location")]
        ]
    });

    $scope.exportVendors = function () {
        var url = serviceBase + 'api/Excel/ExportVendors?countryId=' + $localStorage.country.Id;

        document.location = url;
    }

    $scope.importVendors = function () {
        var dlg = dialogs.create('tpl/dialogs/importVendors.html', 'ImportVendorsCtrl');
        dlg.result.then(function (result) {
            if (result) {
                if (!result.Errors.length) {
                    toaster.pop('success', gettext('Success'), gettext('Vendors successfuly imported.'));
                } else {
                    toaster.pop('warning', gettext('Notice'), gettext('Some vendors were not imported correctly.'));

                    result.Errors.forEach(function (e) {
                        toaster.pop('error', gettext('Error'), gettext('Error importing vendor. Message from server:\n') + e.ErrorText + "\nLine: " + e.Line);
                    });
                }

                $scope.loadGridData();
            }
        }).catch(function (res) {
            toaster.pop('error', gettext('Error'), res.data);
        });
    };

    $scope.loadGridData();
}]);


app.controller('ImportVendorsCtrl', ['breeze', 'serviceBase', '$scope', '$q', '$modalInstance', '$upload', '$localStorage',
    function (breeze, serviceBase, $scope, $q, $modalInstance, $upload, $localStorage) {
        $scope.files = [];

        $scope.upload = function () {
            $scope.uploading = $upload.upload({
                url: serviceBase + 'api/Excel/ImportVendors?countryId=' + $localStorage.country.Id,
                file: $scope.files.pop()
            }).then(function (result) {
                $modalInstance.close(result.data);
            }).catch(function () {
                console.log(arguments);
            });
        };

        $scope.close = function () {
            $modalInstance.close(false);
        };
    }]);
