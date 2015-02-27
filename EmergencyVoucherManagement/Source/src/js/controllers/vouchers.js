'use strict';

app.controller('VoucherDistributionCreateCtrl', ['breeze', 'backendService', '$scope', '$state', '$q', 'locations', function (breeze, backendService, $scope, $state, $q, locations) {
    $scope.save = function () {
        backendService.saveChanges([$scope.entity]).then(function (ne) {
            $state.go('distributions.edit', { id: ne.entities[0].Id });
        }).catch(function () {

        });
    };

    $scope.locations = locations;
    $scope.isEditing = true;
    $scope.isNew = true;
    $scope.entity = backendService.createEntity("Distribution");
}]);

app.controller('VoucherDistributionEditCtrl', ['breeze', 'backendService', '$scope', '$state', '$q', 'locations', 'dialogs', 'voucherTypes',
    function (breeze, backendService, $scope, $state, $q, locations, dialogs, voucherTypes) {
        $scope.save = function (andContinue) {
            $scope.isEditing = false;

            backendService.saveChanges().then(function () {
                if (!andContinue)
                    $state.go('distributions.list');
            }).catch(function () {

            });
        };
        $scope.delete = function () {
            var dlg = dialogs.confirm("Confirm", "Are you sure you would like to delete this record? This operation cannot be reversed.");
            dlg.result.then(function () {
                $scope.entity.entityAspect.setDeleted();
                $scope.isEditing = false;

                backendService.saveChanges([$scope.entity]).then(function () {
                    $state.go('distributions.list');
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
        $scope.loadGridData = function () {
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
                var pageSize = parseInt($scope.pagingOptions.pageSize);
                var currentPage = parseInt($scope.pagingOptions.currentPage);
                var entityQuery = new breeze.EntityQuery("Vouchers")
                    .expand("Type")
                    .skip(pageSize * (currentPage - 1))
                    .take(pageSize)
                    .inlineCount(true)
                    .using(backendService);

                if (order) {
                    entityQuery = entityQuery.orderBy(order);
                }

                entityQuery = entityQuery.where({
                    "DistributionId": { '==': $scope.entity.Id }
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
                    }).catch(function () { console.log(arguments);});
            }, 100);
        };
        $scope.removeCategory = function (category) {
            category.entityAspect.setDeleted();

            $scope.categories = $scope.categories.filter(function (c) {
                return c.Id != category.Id;
            });
        };

        $scope.addCategory = function () {
            var category = backendService.createEntity("DistributionVoucherCategory", {DistributionId: $scope.entity.Id});
            $scope.categories.push(category);
        };

        var watchFunction = function () {
            $scope.loadGridData();
        };

        $scope.locations = locations;
        $scope.voucherTypes = voucherTypes;
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
                fields: ['Id'],
                directions: ['asc']
            },
            pagingOptions: $scope.pagingOptions,
            filterOptions: $scope.filterOptions,
            enableRowSelection: false,
            useExternalSorting: true,
            columnDefs: [
                { field: "Type.Name", displayName: "Type" },
                { field: "VoucherCode", displayName: "Voucher Code" },
                { field: "Value", displayName: "Value" }
            ]
        };

        var query = new breeze.EntityQuery('Distributions')
            .expand('Beneficiaries')
            .expand('Categories')
            .using(backendService);
        $q.when(query.where("Id", "==", $state.params.id).take(1).execute()).then(function (res) {
            if (res.results) {
                var entity = res.results.pop();

                $scope.entity = entity;
                $scope.categories = entity.Categories;
                $scope.loadGridData();
            }
        });



        $scope.$watch('pagingOptions', watchFunction, true);
        $scope.$watch('gridOptions.sortInfo', watchFunction, true);

    }]);

app.controller('VoucherDistributionGridCtrl', ['breeze', 'backendService', '$scope', '$http', '$localStorage',
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

                var entityQuery = new breeze.EntityQuery("Distributions")
                    .expand("Location")
                    .orderBy(order)
                    .skip($localStorage.distributionGridSettings.pageSize * ($localStorage.distributionGridSettings.currentPage - 1))
                    .take($localStorage.distributionGridSettings.pageSize)
                    .inlineCount(true)
                    .using(backendService);

                if ($scope.filter) {
                    entityQuery = entityQuery.where($scope.filter);
                }
                entityQuery
                    .execute().then(function (res) {
                        $scope.totalServerItems = res.inlineCount;
                        $scope.distributions = res.results.map(function (r) {
                            return r;
                        });

                        if (!$scope.$$phase) {
                            $scope.$apply();
                        }
                    });
            }, 100);
        };

        var watchFunction = function () {
            $localStorage.distributionGridSettings.pageSize = parseInt($scope.pagingOptions.pageSize);
            $localStorage.distributionGridSettings.currentPage = parseInt($scope.pagingOptions.currentPage);
            $localStorage.distributionGridSettings.sortInfo = $scope.gridOptions.sortInfo;
            $scope.loadGridData();
        };

        $scope.showDisabled = function () {
            $scope.showingDisabled = true;
            $localStorage.distributionGridSettings.showingDisabled = true;

            $localStorage.distributionGridSettings.filter['Disabled'] = { '==': true };
            $scope.loadGridData();
        };
        $scope.hideDisabled = function () {
            $scope.showingDisabled = false;

            $localStorage.distributionGridSettings.showingDisabled = false;
            $localStorage.distributionGridSettings.filter['Disabled'] = { '!=': true };
            $scope.loadGridData();
        };

        if (!angular.isDefined($localStorage.distributionGridSettings)) {
            $localStorage.distributionGridSettings = {
                pageSize: 250,
                currentPage: 1,
                showingDisabled: false,
                sortInfo: {
                    fields: ['Title'],
                    directions: ['asc']
                }
                //, filter: { 'Disabled': { '!=': true } }
            };
        }

        $scope.filterOptions = {
            filterText: "",
            useExternalFilter: true
        };
        $scope.totalServerItems = 0;
        $scope.showingDisabled = $localStorage.distributionGridSettings.showingDisabled;
        $scope.filter = $localStorage.distributionGridSettings.filter;
        $scope.pagingOptions = {
            pageSizes: [250, 500, 1000],
            pageSize: $localStorage.distributionGridSettings.pageSize,
            currentPage: $localStorage.distributionGridSettings.currentPage
        };
        $scope.gridOptions = {
            data: 'distributions',
            enablePaging: true,
            showFooter: true,
            rowHeight: 36,
            headerRowHeight: 36,
            totalServerItems: 'totalServerItems',
            sortInfo: $localStorage.distributionGridSettings.sortInfo,
            pagingOptions: $scope.pagingOptions,
            filterOptions: $scope.filterOptions,
            enableRowSelection: false,
            useExternalSorting: true,
            columnDefs: [
                { field: "Title", cellTemplate: '<div class="ngCellText" ng-class="col.colIndex()"><span ng-cell-text><a href ui-sref="distributions.edit({ id: row.getProperty(\'Id\') })">{{COL_FIELD}}</a></span></div>' },
                { field: "Location.Name", displayName: "Location" }
            ]
        };

        $scope.$watch('pagingOptions', watchFunction, true);
        $scope.$watch('gridOptions.sortInfo', watchFunction, true);

        $scope.loadGridData();
    }]);
