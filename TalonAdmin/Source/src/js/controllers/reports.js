'use strict';

app.controller('VendorFinancialReportingCtrl', ['$scope', '$rootScope', 'gettext', 'settings', '$q', 'toaster', 'serviceBase', 'controlledLists', 'authService', '$injector',
    function ($scope, $rootScope, gettext, settings, $q, toaster, serviceBase, controlledLists, authService, $injector) {
        $q.all([controlledLists.getVendors(), controlledLists.getDistributions(), controlledLists.getPrograms()]).then(function (promises) {
            $scope.vendors = promises[0].filter(function (v) { return !v.ParentRecordId; });
            $scope.distributions = promises[1];
            $scope.programs = promises[2];
        });
        var localStorageService = $injector.get('localStorageService');
        var authData = localStorageService.get('authorizationData');
        $scope.token = authData.token;

        $scope.url = serviceBase + 'api/Reports/VendorProgramFinancialReport'

        $scope.report = {
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
    }]);

app.controller('DistributionReportingCtrl', ['$scope', '$rootScope', 'gettext', 'settings', '$q', 'toaster', 'serviceBase', 'controlledLists', '$injector',
    function ($scope, $rootScope, gettext, settings, $q, toaster, serviceBase, controlledLists, $injector) {
        $q.all([controlledLists.getVendors(), controlledLists.getDistributions()]).then(function (promises) {
            $scope.vendors = promises[0];
            $scope.distributions = promises[1];
        });
        var localStorageService = $injector.get('localStorageService');
        var authData = localStorageService.get('authorizationData');
        $scope.token = authData.token;

        $scope.url = serviceBase + 'api/Reports/DistributionReport'

        $scope.report = {
        };

        $scope.$watch('report.PeriodStart', function () {
            console.log($scope.report);
            $scope.report.PeriodStartJSON = moment($scope.report.PeriodStart).toJSON();
        });
        $scope.$watch('report.PeriodEnd', function () {
            console.log($scope.report);
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
    }]);

app.controller('PaymentScheduleReportCtrl', ['$scope', '$rootScope', 'gettext', 'settings', '$q', 'toaster', 'serviceBase', 'controlledLists', '$injector',
    function ($scope, $rootScope, gettext, settings, $q, toaster, serviceBase, controlledLists, $injector) {
        $q.all([controlledLists.getVendors(), controlledLists.getDistributions(), controlledLists.getPrograms()]).then(function (promises) {
            $scope.vendors = promises[0].filter(function (v) { return !v.ParentRecordId; });
            $scope.distributions = promises[1];
            $scope.programs = promises[2];
        });
        var localStorageService = $injector.get('localStorageService');
        var authData = localStorageService.get('authorizationData');
        $scope.token = authData.token;

        $scope.url = serviceBase + 'api/Reports/PaymentScheduleReport'
        
        var today = moment();
        var lastMonth = moment();
        lastMonth.add(-1,'month');

        $scope.report = {
            PeriodEnd: new Date(today.year(), today.month(), 1),
            PeriodStart: new Date(lastMonth.year(), lastMonth.month(), 1),
        };

        console.log($scope.report);

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
    }]);