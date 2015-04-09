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
        $scope.statusToString = function (status) {
            if (typeof (status) == 'undefined')
                return "Not Assigned";

            status = parseInt(status);
            if (status == 0) {
                return "SMS Sent"
            } else if (status == 2) {
                return "Used";
            } else if (status == 3) {
                return "Cancelled";
            }
        };

        subGrid($scope, {
            collectionType: 'VoucherTransactionRecords',
            key: 'VendorId',
            expand: ['Voucher', "Voucher.Category", 'Beneficiary', 'Vendor'],
            columns: [
                ["Status", gettext("Status"), '{{statusToString(COL_FIELD)}}'],
                ["FinalizedOn", gettext("Redemption Date"), '{{COL_FIELD|date:"medium"}}'],
                ["Beneficiary.Name", gettext("Beneficiary"), null, false],
                ["Voucher.VoucherCode", gettext("Voucher Code")],
                ["Voucher.Category.Value", gettext("Value")]
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
            ["Name", gettext("Name"), '<a href ui-sref="vendors.edit({ id: row.getProperty(\'Id\') })">{{COL_FIELD}}</a>'],
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
