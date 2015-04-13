'use strict';

app.controller('VendorFinancialReportingCtrl', ['$scope', '$rootScope', 'gettext', 'settings', '$q', 'toaster', 'serviceBase', 'controlledLists', 'authService',
    function ($scope, $rootScope, gettext, settings, $q, toaster, serviceBase, controlledLists, authService) {
        $q.all([controlledLists.getVendors(), controlledLists.getDistributions()]).then(function (promises) {
            $scope.vendors = promises[0];
            $scope.distributions = promises[1];
        });
        $scope.url = serviceBase + 'api/Reports/VendorFinancialReport'

        $scope.report = {
        };

        authService.loadUserData().then(function () {
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

app.controller('DistributionReportingCtrl', ['$scope', '$rootScope', 'gettext', 'settings', '$q', 'toaster', 'serviceBase', 'controlledLists',
    function ($scope, $rootScope, gettext, settings, $q, toaster, serviceBase, controlledLists) {
        $q.all([controlledLists.getVendors(), controlledLists.getDistributions()]).then(function (promises) {
            $scope.vendors  = promises[0]; 
            $scope.distributions  = promises[1]; 
        });

        $scope.url = serviceBase + 'api/Reports/DistributionReport'

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