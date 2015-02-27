'use strict';

app.controller('VendorRegisterCtrl', ['breeze', 'backendService', '$scope', '$state', '$q', 'locations', function (breeze, backendService, $scope, $state, $q, locations) {
    $scope.save = function () {
        backendService.saveChanges([$scope.entity]).then(function (ne) {
            $state.go('vendors.edit', { id: ne.entities[0].Id });
        }).catch(function () {

        });
    };

    $scope.locations = locations;
    $scope.isEditing = true;
    $scope.entity = backendService.createEntity("Vendor");
}]);

app.controller('VendorEditCtrl', ['breeze', 'backendService', '$scope', '$state', '$q', 'locations', 'dialogs',
    function (breeze, backendService, $scope, $state, $q, locations, dialogs) {
        $scope.save = function (andContinue) {
            $scope.isEditing = false;

            backendService.saveChanges([$scope.entity]).then(function () {
                if (!andContinue)
                    $state.go('vendors.list');
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
                    $state.go('vendors.list');

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

        $scope.locations = locations;
        $scope.isEditing = false;

        var query = new breeze.EntityQuery('Vendors')
            .using(backendService);


        query.where("Id", "==", $state.params.id).take(1).execute().then(function (res) {
            if (res.results) {
                var entity = res.results[0];

                $scope.entity = entity;
            }
        }).catch(function() {
            console.log(arguments);
        });
    }]);

app.controller('VendorGridCtrl', ['breeze', 'backendService', '$scope', '$http', '$localStorage',
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

                var entityQuery = new breeze.EntityQuery("Vendors")
                    .orderBy(order)
                    .skip($localStorage.vendorGridSettings.pageSize * ($localStorage.vendorGridSettings.currentPage - 1))
                    .take($localStorage.vendorGridSettings.pageSize)
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
                    }).catch(function () { console.log(arguments); });
            }, 100);
        };

        var watchFunction = function () {
            $localStorage.vendorGridSettings.pageSize = parseInt($scope.pagingOptions.pageSize);
            $localStorage.vendorGridSettings.currentPage = parseInt($scope.pagingOptions.currentPage);
            $localStorage.vendorGridSettings.sortInfo = $scope.gridOptions.sortInfo;
            $scope.loadGridData();
        };

        if (!angular.isDefined($localStorage.vendorGridSettings)) {
            $localStorage.vendorGridSettings = {
                pageSize: 250,
                currentPage: 1,
                showingDisabled: false,
                sortInfo: {
                    fields: ['Name'],
                    directions: ['asc']
                }
            };
        }

        $scope.filterOptions = {
            filterText: "",
            useExternalFilter: true
        };
        $scope.totalServerItems = 0;
        $scope.showingDisabled = $localStorage.vendorGridSettings.showingDisabled;
        $scope.filter = $localStorage.vendorGridSettings.filter;
        $scope.pagingOptions = {
            pageSizes: [250, 500, 1000],
            pageSize: $localStorage.vendorGridSettings.pageSize,
            currentPage: $localStorage.vendorGridSettings.currentPage
        };
        $scope.gridOptions = {
            data: 'myData',
            enablePaging: true,
            showFooter: true,
            rowHeight: 36,
            headerRowHeight: 36,
            totalServerItems: 'totalServerItems',
            sortInfo: $localStorage.vendorGridSettings.sortInfo,
            pagingOptions: $scope.pagingOptions,
            filterOptions: $scope.filterOptions,
            enableRowSelection: false,
            useExternalSorting: true,
            columnDefs: [
                { field: "Name", cellTemplate: '<div class="ngCellText" ng-class="col.colIndex()"><span ng-cell-text><a href ui-sref="vendors.edit({ id: row.getProperty(\'Id\') })">{{COL_FIELD}}</a></span></div>' },
                { field: "MobileNumber", displayName: "Mobile Number" },
                { field: "Location.Name", displayName: "Location" }
            ]
        };

        $scope.$watch('pagingOptions', watchFunction, true);
        $scope.$watch('gridOptions.sortInfo', watchFunction, true);

        $scope.loadGridData();
    }]);
