'use strict';

app.controller('VendorReceiptReconciliationCtrl', ['$scope', '$rootScope', 'gettext', 'settings', '$q', 'toaster', 'backendService', 'controlledLists',
    function ($scope, $rootScope, gettext, settings, $q, toaster, backendService, controlledLists) {
        $q.all([controlledLists.getVendors(), controlledLists.getDistributions(), controlledLists.getPrograms()]).then(function (promises) {
            $scope.vendors = promises[0].filter(function (v) { return !v.ParentRecordId;});
            $scope.distributions = promises[1];
            $scope.programs = promises[2];
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
                .expand(["Voucher", "Beneficiary", "Voucher.Category", "Vendor", "Vendor.ParentRecord"])
                .where({
                    'and': [ 
                        { 
                            "or": [
                                { "VendorId": { "==": $scope.reconciliation.Vendor.Id } },
                                { "Vendor.ParentRecordId": { "==": $scope.reconciliation.Vendor.Id } },
                            ]
                        },
                        {"Voucher.Distribution.Program.Id": {"==": $scope.reconciliation.Program.Id }},
                        { "Voucher.IsFinalized": {"==": null } }
                    ]
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
        $q.all([controlledLists.getVendors(), controlledLists.getDistributions(), controlledLists.getPrograms()]).then(function (promises) {
            $scope.vendors  = promises[0]; 
            $scope.distributions = promises[1];
            $scope.programs = promises[2];
        });

        $scope.report = {
        };



        $scope.downloadReport = function (report) {
            window.open('data:application/pdf;base64,' + report.OriginalReport);
        };



        $scope.listReports = function () {
            var query = new breeze
                .EntityQuery("ProgramVendorReconciliations")
                .expand(['Vendor'])
                .using(backendService)
                .where({
                    "ProgramId": { "==": $scope.reconciliation.Program.Id }
                })
                .execute()
                .then(function (response) {
                    $scope.reconciliation.Reports = response.results;
                })
                .catch(function () { console.log(arguments) });
        };
    }]);