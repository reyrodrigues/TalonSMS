'use strict';

app.controller('VendorReceiptReconciliationCtrl', ['$scope', '$rootScope', 'gettext', 'settings', '$q', 'toaster', 'backendService', 'controlledLists',
    function ($scope, $rootScope, gettext, settings, $q, toaster, backendService, controlledLists) {
        $q.all([controlledLists.getVendors(), controlledLists.getDistributions()]).then(function (promises) {
            $scope.vendors = promises[0];
            $scope.distributions = promises[1];
        });


        $scope.reconciliation = {
        };
        $scope.$watch('reconciliation.Filter', function (filter) {
            if (filter) {
                $scope.reconciliation.FilteredVouchers = $scope.reconciliation.Vouchers.filter(function (v) {
                    return v.ConfirmationCode.indexOf(filter) > -1 || v.Voucher.VoucherCode.indexOf(filter) > -1;
                });
            } else {
                $scope.reconciliation.FilteredVouchers = $scope.reconciliation.Vouchers;
            }
        });


        $scope.reconcileVoucher = function (transactionRecord) {
            transactionRecord.Voucher.ReconciledOn = moment().utc().toDate();
            transactionRecord.Voucher.ReconciledBy = $rootScope.currentUser.UserName;

            backendService.saveChanges([transactionRecord.Voucher])
                .then(function () { })
                .catch(function (response) { toaster.pop('error', gettext('Error'), res.data); });
        };


        $scope.loadVendor = function () {
            var query = new breeze
                .EntityQuery("VoucherTransactionRecords")
                .using(backendService)
                .expand(["Voucher", "Beneficiary", "Voucher.Category"])
                .where({
                    "VendorId": {"==": $scope.reconciliation.Vendor.Id },
                    "Voucher.DistributionId": {"==": $scope.reconciliation.Distribution.Id },
                    "Voucher.IsFinalized": {"==": null }
                })
                .execute()
                .then(function (response) {
                    $scope.reconciliation.Vouchers = response.results;
                    $scope.reconciliation.FilteredVouchers = $scope.reconciliation.Vouchers;
                })
                .catch(function () { console.log(arguments) });
        };
    }]);

app.controller('ReportHistoryReconciliationCtrl', ['$scope', '$rootScope', 'gettext', 'settings', '$q', 'toaster', 'backendService', 'controlledLists',
    function ($scope, $rootScope, gettext, settings, $q, toaster, backendService, controlledLists) {
        $q.all([controlledLists.getVendors(), controlledLists.getDistributions()]).then(function (promises) {
            $scope.vendors  = promises[0]; 
            $scope.distributions  = promises[1]; 
        });

        $scope.report = {
        };



        $scope.downloadReport = function (report) {
            window.open('data:application/pdf;base64,' + report.OriginalReport);
        };



        $scope.listReports = function () {
            var query = new breeze
                .EntityQuery("DistributionVendorReconciliations")
                .using(backendService)
                .where({
                    "VendorId": { "==": $scope.reconciliation.Vendor.Id },
                    "DistributionId": { "==": $scope.reconciliation.Distribution.Id }
                })
                .execute()
                .then(function (response) {
                    $scope.reconciliation.Reports = response.results;
                })
                .catch(function () { console.log(arguments) });
        };
    }]);