'use strict';

app.controller('BeneficiariesCreateCtrl', ['$scope', 'settings', 'ControllerFactory', 'injectorHelper',
    function ($scope, settings, ControllerFactory, injectorHelper) {
        injectorHelper.injectPromises($scope, ['locations', 'groups']);

        ControllerFactory.Create($scope, settings);
    }]);

app.controller('BeneficiariesEditCtrl', ['$scope', 'ControllerFactory', 'gettext', 'injectorHelper', 'backendService',
    function ($scope, ControllerFactory, gettext, injectorHelper, backendService) {
        injectorHelper.injectPromises($scope, ['locations', 'groups']);

        $scope.deactivate = function () {
            $scope.entity.Disabled = true;
            backendService.saveChanges([$scope.entity]);
        };

        $scope.reactivate = function () {
            $scope.entity.Disabled = false;
            backendService.saveChanges([$scope.entity]);
        };

        $scope.UnusedVouchersFilter = { 'Status': { '==': 0 } };
        $scope.UsedVouchersFilter = { 'Status': { '==': 2 } };

        ControllerFactory.Edit($scope, {
            entityType: 'Beneficiary',
            collectionType: 'Beneficiaries',
            listState: 'beneficiaries.list',
            expand: ['Distributions']
        });

        ControllerFactory.ChildGrid($scope, {
            collectionType: 'VoucherTransactionRecords',
            name: 'UsedVouchers',
            key: 'BeneficiaryId',
            expand: ['Voucher', 'Voucher.Category', 'Vendor'],
            columns: [
                ["Status", gettext("Status"), '{{COL_FIELD|voucherStatus}}'],
                ["FinalizedOn", gettext("Redemption Date"), '{{COL_FIELD|localeDateTime}}'],
                ["Vendor.Name", gettext("Vendor"), false, false],
                ["Voucher.VoucherCode", gettext("Voucher Code")],
                ["Voucher.Category.Value", gettext("Value")]
            ]
        });

        ControllerFactory.ChildGrid($scope, {
            collectionType: 'VoucherTransactionRecords',
            name: 'UnusedVouchers',
            key: 'BeneficiaryId',
            expand: ['Voucher', 'Voucher.Category', 'Vendor'],
            columns: [
                ["Status", gettext("Status"), '{{COL_FIELD|voucherStatus}}'],
                ["FinalizedOn", gettext("Redemption Date"), '{{COL_FIELD|localeDateTime}}'],
                ["Vendor.Name", gettext("Vendor"), false, false],
                ["Voucher.VoucherCode", gettext("Voucher Code")],
                ["Voucher.Category.Value", gettext("Value")]
            ]
        });

        $scope.loadData()
            .then(function () {
                $scope.UsedVouchersLoadGrid();
                $scope.UnusedVouchersLoadGrid();
            });
    }]);

app.controller('BeneficiariesListCtrl', ['$scope', '$state', '$localStorage', 'ControllerFactory', 'gettext', 'dialogs', 'toaster', 'serviceBase', '$location', 'settings', '$injector',
    function ($scope, $state, $localStorage, ControllerFactory, gettext, dialogs, toaster, serviceBase, $location, settings, $injector) {
        var localStorageService = $injector.get('localStorageService');
        var authData = localStorageService.get('authorizationData');
        $scope.showingDisabled = false;
        $scope.token = authData.token;
        $scope.exportUrl = serviceBase + 'api/Excel/ExportBeneficiaries?countryId=' + $localStorage.country.Id + '&organizationId=' + $localStorage.organization.Id;

        $scope.filter = { Disabled: { '!=': true } };

        $scope.showDisabled = function () {
            $scope.showingDisabled = true;
            $scope.filter['Disabled'] = { '==': true };

            $scope.loadGridData();
        };

        $scope.hideDisabled = function () {
            $scope.showingDisabled = false;
            $scope.filter['Disabled'] = { '!=': true };

            $scope.loadGridData();
        };

        $scope.exportBeneficiaries = function () {
            var url = serviceBase + 'api/Excel/ExportBeneficiaries?countryId=' + $localStorage.country.Id + '&organizationId=' + $localStorage.organization.Id;

            document.location = url;
        }

        $scope.importBeneficiaries = function () {
            var dlg = dialogs.create('tpl/dialogs/importBeneficiaries.html', 'ImportBeneficiariesCtrl');
            dlg.result.then(function (result) {
                if (result) {
                    if (!result.Errors.length) {
                        toaster.pop('success', gettext('Success'), gettext('Beneficiaries successfuly imported.'));
                    } else {
                        toaster.pop('warning', gettext('Notice'), gettext('Some beneficiaries were not imported correctly.'));

                        result.Errors.forEach(function (e) {
                            toaster.pop('error', gettext('Error'), gettext('Error importing beneficiary. Message from server:\n') + e.ErrorText + "\nLine: " + e.Line);
                        });
                    }

                    $scope.loadGridData();
                }
            }).catch(function (res) {
                toaster.pop('error', gettext('Error'), res.data);
            });
        };

        ControllerFactory.List($scope, angular.extend({
            expand: ['Location', "Group"],
            columns: [
                ["FirstName", gettext("Name"), '<a href ui-sref="beneficiaries.edit({ id: row.getProperty(\'Id\') })">{{ row.getProperty(\'Name\')}}</a>'],
                ["BirthYear", gettext("Birth Year"), '<a href ui-sref="beneficiaries.edit({ id: row.getProperty(\'Id\') })">{{COL_FIELD}}</a>'],
                ["NationalId", gettext("National Id Number")],
                ["MobileNumber", gettext("Mobile Number")],
                ["Location.Name", gettext("Location")],
                ["Group.Name", gettext("Group")]
            ]
        }, settings));

        $scope.loadGridData();
    }]);

app.controller('ImportBeneficiariesCtrl', ['breeze', 'serviceBase', '$scope', '$q', '$modalInstance', '$upload', '$localStorage',
    function (breeze, serviceBase, $scope, $q, $modalInstance, $upload, $localStorage) {
        $scope.files = [];

        $scope.upload = function () {
            $scope.uploading = $upload.upload({
                url: serviceBase + 'api/Excel/ImportBeneficiaries?countryId=' + $localStorage.country.Id + '&organizationId=' + $localStorage.organization.Id,
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
