angular.module('talon.reporting', [
  'ui.router',
  'datatables',
  'datatables.bootstrap',
  'talon.dataAccess',
  'talon.common',
  'dialogs.main',
  'ngFileUpload'
])

.config(function config($stateProvider) {
    $stateProvider
    .state('reconciliation', {
        url: '/reconciliation',
        views: {
            "app": {
                template: "<div ui-view></div>"
            }
        },
        abstract: true,
        data: {
        }
    })

    .state('reconciliation.vendor-receipt', {
        url: '/vendor-receipt',
        controller: 'VendorReceiptController as vm',
        templateUrl: 'reporting/vendor-receipt.tpl.html',
        data: {
            pageTitle: 'Vendor Receipt',
        }
    })

    .state('reconciliation.report-history', {
        url: '/report-history',
        controller: 'ReportHistoryController as vm',
        templateUrl: 'reporting/report-history.tpl.html',
        data: {
            pageTitle: 'Report History',
        }
    })


    .state('reporting', {
        url: '/reporting',
        views: {
            "app": {
                template: "<div ui-view></div>"
            }
        },
        abstract: true,
        data: {
        }
    })

    .state('reporting.distribution', {
        url: '/distribution',
        controller: 'DistributionReportController as vm',
        templateUrl: 'reporting/distribution.tpl.html',
        data: {
            pageTitle: 'Distribution Report',
        }
    })

    .state('reporting.vendor-financial', {
        url: '/vendor-financial',
        controller: 'VendorFinancialReportController as vm',
        templateUrl: 'reporting/vendor-financial.tpl.html',
        data: {
            pageTitle: 'Vendor Financial Report',
        }
    })
    .state('reporting.program-closure', {
        url: '/program-closure',
        controller: 'ProgramClosureController as vm',
        templateUrl: 'reporting/program-closure.tpl.html',
        data: {
            pageTitle: 'Vendor Financial Report',
        }
    })
    ;
})
.controller('VendorFinancialReportController', VendorFinancialReportController)
.controller('DistributionReportController', DistributionReportController)
.controller('VendorReceiptController', VendorReceiptController)
.controller('ReportHistoryController', ReportHistoryController)
.controller('ProgramClosureController', ProgramClosureController)

;

function ReportHistoryController($scope, $rootScope, $q, toaster, entityManagerFactory, controlledLists) {
    $q.all([controlledLists.vendors(), controlledLists.distributions(), controlledLists.programs(), controlledLists.reportTypes()]).then(function (promises) {
        $scope.vendors = promises[0];
        $scope.distributions = promises[1];
        $scope.programs = promises[2];
        $scope.reportTypes = promises[3];
    });

    var entityManager = entityManagerFactory.entityManager();

    $scope.report = {
        PaperSize: 'A4'
    };

    $scope.listReports = function () {
        var query = entityManagerFactory.entityQuery("ExportedReports")
            .using(entityManager)
            .where({ 'and' : [
            { "programId": { "==": $scope.reconciliation.program.id } },
            { "reportType": { "==": $scope.reconciliation.reportType.id } },
            ]})
            .execute()
            .then(function (response) {
                $scope.reconciliation.Reports = response.results;
            })
            .catch(function () { console.log(arguments); });
    };
}

function VendorReceiptController($scope, $rootScope, $q, toaster, entityManagerFactory, controlledLists) {
    $q.all([controlledLists.vendors(), controlledLists.distributions(), controlledLists.programs()]).then(function (promises) {
        $scope.vendors = promises[0].filter(function (v) { return !v.parentRecordId; });
        $scope.distributions = promises[1];
        $scope.programs = promises[2];
    });

    var entityManager = entityManagerFactory.entityManager();

    $scope.reconciliation = {
    };

    $scope.$watch('reconciliation.Filter', function (filter) {
        if (filter) {
            $scope.reconciliation.FilteredVouchers = $scope.reconciliation.Vouchers.filter(function (v) {
                return v.confirmationCode.indexOf(filter) > -1 || v.voucher.voucherCode.indexOf(filter) > -1;
            });
        } else {
            $scope.reconciliation.FilteredVouchers = $scope.reconciliation.Vouchers;
        }
    });


    $scope.reconcileVoucher = function (transactionRecord) {
        transactionRecord.reconciledOn = moment().utc().toDate();
        transactionRecord.reconciledBy = $rootScope.currentUser.UserName;

        entityManager.saveChanges([transactionRecord])
            .then(function () { })
            .catch(function (response) { toaster.pop('error', 'Error', res.data); });
    };


    $scope.loadVendor = function () {
        var query = entityManagerFactory
            .entityQuery("VoucherTransactionRecords")
            .using(entityManager)
            .expand(["voucher", "beneficiary", "voucher.category", "vendor", "vendor.parentRecord"])
            .where({
                'and': [
                    {
                        "or": [
                            { "vendorId": { "==": $scope.reconciliation.Vendor.id } },
                            { "vendor.parentRecordId": { "==": $scope.reconciliation.Vendor.id } },
                        ]
                    },
                    { "voucher.distribution.program.id": { "==": $scope.reconciliation.Program.id } },
                    { "isFinalized": { "==": null } },
                    {
                        "or": [
                            { "type": { "==": 2 } },
                            { "type": { "==": 3 } }
                        ]
                    },
                ]
            })
            .execute()
            .then(function (response) {
                $scope.reconciliation.Vouchers = response.results;
                $scope.reconciliation.FilteredVouchers = $scope.reconciliation.Vouchers;
            })
            .catch(function () { console.log(arguments); });
    };
}

function ProgramClosureController($scope, $rootScope, $q, toaster, controlledLists, authService, $injector) {
    $q.all([controlledLists.programs()]).then(function (promises) {
        $scope.programs = promises[0];
    });
    var $localStorage = $injector.get('$localStorage');
    var authData = $localStorage.authorizationData;
    $scope.token = authData.token;

    $scope.url = serviceRoot + 'api/Reports/ProgramClosureReport';

    $scope.report = {
        PaperSize: 'A4'
    };

    $rootScope.$watch('currentUser', function () {
        if ($rootScope.currentUser) {
            $scope.report.OrganizationId = $rootScope.currentUser.OrganizationId;
        }
    });

    $rootScope.$watch('country', function () {
        if ($rootScope.country) {
            $scope.report.CountryId = $rootScope.country.Id;
        }
    });
}


function VendorFinancialReportController($scope, $rootScope, $q, toaster, controlledLists, authService, $injector) {
    $q.all([controlledLists.vendors(), controlledLists.distributions(), controlledLists.programs()]).then(function (promises) {
        $scope.vendors = promises[0].filter(function (v) { return !v.parentRecordId; });
        $scope.distributions = promises[1];
        $scope.programs = promises[2];
    });
    var $localStorage = $injector.get('$localStorage');
    var authData = $localStorage.authorizationData;
    $scope.token = authData.token;

    $scope.url = serviceRoot + 'api/Reports/VendorProgramFinancialReport';

    $scope.report = {
        PaperSize: 'A4'
    };

    $rootScope.$watch('currentUser', function () {
        if ($rootScope.currentUser) {
            $scope.report.OrganizationId = $rootScope.currentUser.OrganizationId;
        }
    });

    $rootScope.$watch('country', function () {
        if ($rootScope.country) {
            $scope.report.CountryId = $rootScope.country.Id;
        }
    });
}

function DistributionReportController($scope, $rootScope, $q, toaster, controlledLists, $injector) {
    $q.all([controlledLists.vendors(), controlledLists.distributions()]).then(function (promises) {
        $scope.vendors = promises[0];
        $scope.distributions = promises[1];
    });
    var $localStorage = $injector.get('$localStorage');
    var authData = $localStorage.authorizationData;
    $scope.token = authData.token;

    $scope.url = serviceRoot + 'api/Reports/DistributionReport';

    $scope.report = {
        PaperSize: 'A4'
    };

    $scope.$watch('report.PeriodStart', function () {
        $scope.report.PeriodStartJSON = moment($scope.report.PeriodStart).toJSON();
    });
    $scope.$watch('report.PeriodEnd', function () {
        $scope.report.PeriodEndJSON = moment($scope.report.PeriodEnd).toJSON();
    });

    $rootScope.$watch('currentUser', function () {
        if ($rootScope.currentUser) {
            $scope.report.OrganizationId = $rootScope.currentUser.OrganizationId;
        }
    });

    $rootScope.$watch('country', function () {
        if ($rootScope.country) {
            $scope.report.CountryId = $rootScope.country.Id;
        }
    });
}