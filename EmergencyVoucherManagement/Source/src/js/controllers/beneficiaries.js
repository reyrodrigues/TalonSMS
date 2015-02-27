'use strict';

app.controller('BeneficiaryRegisterCtrl', ['breeze', 'backendService', '$scope', '$state', '$q', 'locations', function (breeze, backendService, $scope, $state, $q, locations) {
    $scope.save = function () {
        backendService.saveChanges([$scope.entity]).then(function (ne) {
            $state.go('beneficiaries.edit', { id: ne.entities[0].Id });
        }).catch(function () {

        });
    };

    $scope.locations = locations;
    $scope.isEditing = true;
    $scope.entity = backendService.createEntity("Beneficiary");
}]);

app.controller('BeneficiaryEditCtrl', ['breeze', 'backendService', '$scope', '$state', '$q', 'locations', 'dialogs',
    function (breeze, backendService, $scope, $state, $q, locations, dialogs) {
        $scope.save = function (andContinue) {
            $scope.isEditing = false;

            backendService.saveChanges([$scope.entity]).then(function () {
                if (!andContinue)
                    $state.go('beneficiaries.list');
            }).catch(function () {

            });
        };
        $scope.deactivate = function () {
            $scope.entity.Disabled = true;

            backendService.saveChanges([$scope.entity]).then(function () {

            }).catch(function () {

            });
        };
        $scope.reactivate = function () {
            $scope.entity.Disabled = false;
            backendService.saveChanges([$scope.entity]).then(function () {

            }).catch(function () {

            });
        };
        $scope.delete = function () {
            var dlg = dialogs.confirm("Confirm", "Are you sure you would like to delete this record? This operation cannot be reversed.");
            dlg.result.then(function () {
                console.log(arguments);
                $scope.entity.entityAspect.setDeleted();
                $scope.isEditing = false;

                backendService.saveChanges([$scope.entity]).then(function () {
                    $state.go('beneficiaries.list');

                }).catch(function () {

                });

            });
        };
        $scope.startEditing = function () {
            $scope.isEditing = true;
        };
        $scope.endEditing = function () {
            $scope.isEditing = false;
        };
        $scope.loadGridData = function (pageSize, page) {
            if (!$scope.entity || !$scope.entity.Id) {
                return;
            }

            setTimeout(function () {
                var fields = [];
                for (var i = 0; i < $scope.gridOptions.sortInfo.fields.length; i++) {
                    var ordering = $scope.gridOptions.sortInfo.fields[i] + ($scope.gridOptions.sortInfo.directions[i] == "desc" ? " desc" : "");

                    fields.push(ordering);
                }

                var order = fields.join(',');

                var entityQuery = new breeze.EntityQuery("VoucherVerificationItem")
                    .expand("Voucher")
                    .orderBy(order)
                    .skip($scope.pagingOptions.pageSize * ($scope.pagingOptions.currentPage - 1))
                    .take($scope.pagingOptions.pageSize)
                    .inlineCount(true)
                    .using(backendService);

                entityQuery = entityQuery.where({
                    "BeneficiaryId": { '==': $scope.entity.Id }
                });

                entityQuery
                    .execute().then(function (res) {
                        $scope.totalServerItems = res.inlineCount;
                        $scope.vouchers = res.results.map(function (r) {
                            return r;
                        });

                        if (!$scope.$$phase) {
                            $scope.$apply();
                        }
                    });
            }, 100);
        };
        var watchFunction = function () {
            $scope.loadGridData();
        };

        $scope.locations = locations;
        $scope.isEditing = false;
        $scope.vouchers = [];
        $scope.pagingOptions = {
            pageSizes: [10, 20, 1000],
            pageSize: 10,
            currentPage: 1
        };
        $scope.gridOptions = {
            data: 'vouchers',
            enablePaging: true,
            showFooter: true,
            rowHeight: 36,
            headerRowHeight: 36,
            totalServerItems: 'totalServerItems',
            sortInfo: {
                fields: ['id'],
                directions: ['asc']
            },
            pagingOptions: $scope.pagingOptions,
            filterOptions: $scope.filterOptions,
            enableRowSelection: false,
            useExternalSorting: true,
            columnDefs: [
                { field: "Voucher.VoucherCode", displayName: "Voucher Code" },
                { field: "Voucher.Value", displayName: "Value" }
            ]
        };

        var query = new breeze.EntityQuery('Beneficiaries')
            .expand('Distributions')
            .using(backendService);
        $q.when(query.where("Id", "==", $state.params.id).take(1).execute()).then(function (res) {
            if (res.results) {
                var entity = res.results[0];

                $scope.entity = entity;
                $scope.loadGridData();
            }
        });



        $scope.$watch('pagingOptions', watchFunction, true);
        $scope.$watch('gridOptions.sortInfo', watchFunction, true);

    }]);

app.controller('BeneficiaryGridCtrl', ['breeze', 'backendService', '$scope', '$http', '$localStorage',
    function (breeze, backendService, $scope, $http, $localStorage) {
        $scope.loadGridData = function (pageSize, page) {
            setTimeout(function () {
                var data;

                var fields = [];
                for (var i = 0; i < $scope.gridOptions.sortInfo.fields.length; i++) {
                    var ordering = $scope.gridOptions.sortInfo.fields[i] + ($scope.gridOptions.sortInfo.directions[i] == "desc" ? " desc" : "");

                    fields.push(ordering);
                }

                var order = fields.join(',');

                var entityQuery = new breeze.EntityQuery("Beneficiaries")
                    .expand("Location")
                    .orderBy(order)
                    .skip($localStorage.beneficiaryGridSettings.pageSize * ($localStorage.beneficiaryGridSettings.currentPage - 1))
                    .take($localStorage.beneficiaryGridSettings.pageSize)
                    .inlineCount(true)
                    .using(backendService);

                if ($scope.filter) {
                    entityQuery = entityQuery.where($scope.filter);
                }
                entityQuery
                    .execute().then(function (res) {
                        $scope.totalServerItems = res.inlineCount;
                        $scope.myData = res.results.map(function (r) {
                            r.ParsedDate = moment(r.DateOfBirth).format('L');

                            return r;
                        });

                        if (!$scope.$$phase) {
                            $scope.$apply();
                        }
                    });
            }, 100);
        };

        var watchFunction = function () {
            $localStorage.beneficiaryGridSettings.pageSize = parseInt($scope.pagingOptions.pageSize);
            $localStorage.beneficiaryGridSettings.currentPage = parseInt($scope.pagingOptions.currentPage);
            $localStorage.beneficiaryGridSettings.sortInfo = $scope.gridOptions.sortInfo;
            $scope.loadGridData();
        };

        $scope.showDisabled = function () {
            $scope.showingDisabled = true;
            $localStorage.beneficiaryGridSettings.showingDisabled = true;

            $localStorage.beneficiaryGridSettings.filter['Disabled'] = { '==': true };
            $scope.loadGridData();
        };
        $scope.hideDisabled = function () {
            $scope.showingDisabled = false;

            $localStorage.beneficiaryGridSettings.showingDisabled = false;
            $localStorage.beneficiaryGridSettings.filter['Disabled'] = { '!=': true };
            $scope.loadGridData();
        };

        if (!angular.isDefined($localStorage.beneficiaryGridSettings)) {
            $localStorage.beneficiaryGridSettings = {
                pageSize: 250,
                currentPage: 1,
                showingDisabled: false,
                sortInfo: {
                    fields: ['Name'],
                    directions: ['asc']
                },
                filter: { 'Disabled': { '!=': true } }
            };
        }

        $scope.filterOptions = {
            filterText: "",
            useExternalFilter: true
        };
        $scope.totalServerItems = 0;
        $scope.showingDisabled = $localStorage.beneficiaryGridSettings.showingDisabled;
        $scope.filter = $localStorage.beneficiaryGridSettings.filter;
        $scope.pagingOptions = {
            pageSizes: [250, 500, 1000],
            pageSize: $localStorage.beneficiaryGridSettings.pageSize,
            currentPage: $localStorage.beneficiaryGridSettings.currentPage
        };
        $scope.gridOptions = {
            data: 'myData',
            enablePaging: true,
            showFooter: true,
            rowHeight: 36,
            headerRowHeight: 36,
            totalServerItems: 'totalServerItems',
            sortInfo: $localStorage.beneficiaryGridSettings.sortInfo,
            pagingOptions: $scope.pagingOptions,
            filterOptions: $scope.filterOptions,
            enableRowSelection: false,
            useExternalSorting: true,
            columnDefs: [
                { field: "Name", cellTemplate: '<div class="ngCellText" ng-class="col.colIndex()"><span ng-cell-text><a href ui-sref="beneficiaries.edit({ id: row.getProperty(\'Id\') })">{{COL_FIELD}}</a></span></div>' },
                { field: "ParsedDate", displayName: "Date of Birth", cellTemplate: '<div class="ngCellText" ng-class="col.colIndex()"><span ng-cell-text><a href ui-sref="beneficiaries.edit({ id: row.getProperty(\'Id\') })">{{COL_FIELD}}</a></span></div>' },
                { field: "NationalId", displayName: "National Id Number" },
                { field: "MobileNumber", displayName: "Mobile Number" },
                { field: "Location.Name", displayName: "Location" }
            ]
        };

        $scope.$watch('pagingOptions', watchFunction, true);
        $scope.$watch('gridOptions.sortInfo', watchFunction, true);

        $scope.loadGridData();
    }]);
