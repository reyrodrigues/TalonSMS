'use strict';


app.controller('VendorsCreateCtrl', ['$scope', 'createController', 'locations', 'vendorTypes',
    function ($scope, createController, locations, vendorTypes) {
        $scope.locations = locations;
        $scope.vendorTypes = vendorTypes;

        createController($scope, {
            entityType: 'Vendor',
            editState: 'vendors.edit'
        });
    }]);

app.controller('VendorsEditCtrl', ['$scope', 'editController', 'gettext', 'subGrid', 'locations', 'vendorTypes', 'backendService',
    function ($scope, editController, gettext, subGrid, locations, vendorTypes, backendService) {
        $scope.locations = locations;
        $scope.vendorTypes = vendorTypes;
        editController($scope, {
            entityType: 'Vendor',
            collectionType: 'Vendors',
        });

        subGrid($scope, {
            collectionType: 'VoucherTransactionRecords',
            key: 'VendorId',
            expand: ['Voucher', 'Beneficiary', 'Vendor'],
            columnDefs: [
                {
                    field: "Status", displayName: gettext("Status"),
                    cellTemplate: '<div class="ngCellText" ng-class="col.colIndex()"><span ng-cell-text>{{statusToString(COL_FIELD)}}</span></div>'
                },
                { field: "Beneficiary.Name", displayName: gettext("Beneficiary") },
                { field: "Voucher.VoucherCode", displayName: gettext("Voucher Code") },
                { field: "Voucher.Value", displayName: gettext("Value") }
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
        columnDefs: [
            { field: "Name", displayName:gettext("Name"), cellTemplate: '<div class="ngCellText" ng-class="col.colIndex()"><span ng-cell-text><a href ui-sref="vendors.edit({ id: row.getProperty(\'Id\') })">{{COL_FIELD}}</a></span></div>' },
            { field: "MobileNumber", displayName: gettext("Mobile Number") },
            { field: "Location.Name", displayName: gettext("Location") }
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
