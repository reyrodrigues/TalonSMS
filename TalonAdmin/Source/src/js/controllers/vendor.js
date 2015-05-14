'use strict';


app.controller('VendorsCreateCtrl', ['$scope', 'createController', 'settings', 'injectorHelper',
    function ($scope, createController, settings, injectorHelper) {
        injectorHelper.injectPromises($scope, ['locations', 'vendorTypes']);

        createController($scope, settings);
    }]);

app.controller('VendorsEditCtrl', ['$scope', 'editController', 'gettext', 'subGrid', 'settings', 'injectorHelper', 'backendService','$http', 'serviceBase','$q',
    function ($scope, editController, gettext, subGrid, settings, injectorHelper, backendService, $http, serviceBase, $q) {
        injectorHelper.injectPromises($scope, ['locations', 'vendorTypes']);
        $scope.password = {};
        settings.postSave = function () {
            var args = arguments;
            var d = $q.defer();
            if ($scope.password.NewPassword) {
                $http.post(serviceBase + 'api/App/VendorProfile/UpdatePassword', {
                    VendorId: $scope.entity.Id,
                    Password: $scope.password.NewPassword
                }).then(function () {
                    d.resolve.apply(d, args);
                    $scope.password = {};
                }).catch(function () {
                    d.reject();
                });
            } else {
                d.resolve.apply(d, args);
            }
            return d.promise
        };

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

        subGrid($scope, {
            collectionType: 'Vendors',
            name: 'ChildVendors',
            key: 'ParentRecordId',
            columns: [
                ["Name", gettext("Name"), '<a href ui-sref="vendors.edit({ id: row.getProperty(\'Id\') })">{{COL_FIELD}}</a>'],
                ["MobileNumber", gettext("MobileNumber")],
                ["Location.Name", gettext("Location")]
            ]
        });

        $scope.loadData()
            .then(function () {
                var otherQuery = breeze.EntityQuery.from('Vendors')
                    .where('Id', '!=', $scope.entity.Id)
                    .noTracking()
                    .using(backendService)
                    .execute()
                    .then(function (r) {
                        $scope.otherVendors = r.results;
                    });
                $scope.VoucherTransactionRecordsLoadGrid();
                $scope.ChildVendorsLoadGrid();
            });
    }]);

app.controller('VendorsListCtrl', ['$scope', '$state', '$localStorage', 'listController', 'gettext', 'dialogs', 'toaster', 'serviceBase', '$location', '$injector',
function ($scope, $state, $localStorage, listController, gettext, dialogs, toaster, serviceBase, $location, $injector) {
    var storageSetting = $state.current.name + 'GridSettings';
    $scope.showingDisabled = false;

    var localStorageService = $injector.get('localStorageService');
    var authData = localStorageService.get('authorizationData');
    $scope.token = authData.token;
    $scope.exportUrl = serviceBase + 'api/Excel/ExportVendors?countryId=' +$localStorage.country.Id;

    listController($scope, {
        collectionType: 'Vendors',
        expand: ['Location', 'ParentRecord'],
        columns: [
            ["Name", gettext("Entity Name"), '<a href ui-sref="vendors.edit({ id: row.getProperty(\'Id\') })">{{COL_FIELD}}</a>'],
            ["MobileNumber", gettext("Mobile Number")],
            ["ParentRecord.Name", gettext("Main Entity Name")],
            ["Location.Name", gettext("Location")]
        ]
    });


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
