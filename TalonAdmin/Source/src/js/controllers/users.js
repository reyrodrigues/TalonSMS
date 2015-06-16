'use strict';

app.controller('UserCreateCtrl', ['breeze', 'authBackendService', '$scope', '$state', '$q', 'settings',
    function (breeze, backendService, $scope, $state, $q, settings) {
        

    $scope.save = function () {
        backendService.saveChanges([$scope.entity]).then(function (ne) {
            $state.go(settings.editState, { id: ne.entities[0].Id });
        }).catch(function () {

        });
    };

    $scope.isEditing = true;
    $scope.isNew = true;
    $scope.entity = backendService.createEntity(settings.entityType, settings.defaults);
}]);

app.controller('UserEditCtrl', ['breeze', 'authBackendService', '$scope', '$state', '$q', '$http', 'dialogs', 'settings', 'serviceBase', 'toaster',
    function (breeze, backendService, $scope, $state, $q, $http, dialogs, settings, serviceBase, toaster) {
        

        $scope.save = function (andContinue) {
            $scope.isEditing = false;

            backendService.saveChanges().then(function () {
                if (!andContinue)
                    $state.go(settings.listState);
            }).catch(function () {

            });
        };
        $scope.delete = function () {
            var dlg = dialogs.confirm("Confirm", "Are you sure you would like to delete this record? This operation cannot be reversed.");
            dlg.result.then(function () {
                $scope.entity.entityAspect.setDeleted();
                $scope.isEditing = false;

                backendService.saveChanges([$scope.entity]).then(function () {
                    $state.go(settings.listState);
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
        var loadData = function () {
            $q.when(query.where("Id", "==", $state.params.id).take(1).execute()).then(function (res) {
                if (res.results) {
                    var entity = res.results.pop();

                    $scope.entity = entity;
                }
            });
        };

        $scope.isEditing = false;
        var query = new breeze.EntityQuery(settings.collectionType)
            .using(backendService);

        if (settings.expand) {
            query = query.expand(settings.expand);
        }

        loadData();
    }]);

app.controller('UserGridCtrl', ['breeze', 'authBackendService', '$scope', '$http', '$localStorage', '$state', 'settings',
    function (breeze, backendService, $scope, $http, $localStorage, $state, settings) {
        var storageSetting = $state.current.name + 'GridSettings';
        

        $scope.loadGridData = function (pageSize, page) {
            setTimeout(function () {
                var data;

                var fields = [];
                for (var i = 0; i < $scope.gridOptions.sortInfo.fields.length; i++) {
                    var ordering = $scope.gridOptions.sortInfo.fields[i] + ($scope.gridOptions.sortInfo.directions[i] == "desc" ? " desc" : "");

                    fields.push(ordering);
                }

                var order = fields.join(',');

                var entityQuery = new breeze.EntityQuery(settings.collectionType);

                if (settings.expand) {
                    entityQuery = entityQuery.expand(settings.expand);
                }

                if (order) {
                    entityQuery = entityQuery
                        .orderBy(order);
                }

                entityQuery = entityQuery
                    .skip($localStorage[storageSetting].pageSize * ($localStorage[storageSetting].currentPage - 1))
                    .take($localStorage[storageSetting].pageSize)
                    .inlineCount(true)
                    .using(backendService);

                if ($scope.filter) {
                    entityQuery = entityQuery.where($scope.filter);
                }

                entityQuery
                    .execute()
                    .then(function (res) {
                        $scope.totalServerItems = res.inlineCount;
                        $scope.list = res.results.map(function (r) {
                            return r;
                        });

                        if (!$scope.$$phase) {
                            $scope.$apply();
                        }
                    })
                .catch(function () { console.log(arguments); });
            }, 100);
        };

        var watchFunction = function () {
            $localStorage[storageSetting].pageSize = parseInt($scope.pagingOptions.pageSize);
            $localStorage[storageSetting].currentPage = parseInt($scope.pagingOptions.currentPage);
            $localStorage[storageSetting].sortInfo = $scope.gridOptions.sortInfo;
            $scope.loadGridData();
        };

        if (!angular.isDefined($localStorage[storageSetting])) {
            $localStorage[storageSetting] = {
                pageSize: 250,
                currentPage: 1,
                showingDisabled: false,
                sortInfo: {
                    fields: ['Id'],
                    directions: ['asc']
                }
            };
        }

        $scope.filterOptions = {
            filterText: "",
            useExternalFilter: true
        };
        $scope.totalServerItems = 0;
        $scope.showingDisabled = $localStorage[storageSetting].showingDisabled;
        $scope.filter = $localStorage[storageSetting].filter;
        $scope.pagingOptions = {
            pageSizes: [250, 500, 1000],
            pageSize: $localStorage[storageSetting].pageSize,
            currentPage: $localStorage[storageSetting].currentPage
        };
        $scope.gridOptions = {
            data: 'list',
            enablePaging: true,
            showFooter: true,
            rowHeight: 36,
            headerRowHeight: 36,
            totalServerItems: 'totalServerItems',
            sortInfo: $localStorage[storageSetting].sortInfo,
            pagingOptions: $scope.pagingOptions,
            filterOptions: $scope.filterOptions,
            enableRowSelection: false,
            useExternalSorting: true,
            columnDefs: [
                {
                    field: "Name",
                    cellTemplate: '<a href ui-sref="' +
                    settings.editState +
                    '({ id: row.getProperty(\'Id\') })">{{COL_FIELD}}</a>'
                },
                {
                    field: "Email",
                    cellTemplate: '<span ng-cell-text><a href ui-sref="' +
                    settings.editState +
                    '({ id: row.getProperty(\'Id\') })">{{COL_FIELD}}</a>'
                }
            ]
        };

        $scope.$watch('pagingOptions', watchFunction, true);
        $scope.$watch('gridOptions.sortInfo', watchFunction, true);

        $scope.loadGridData();
    }]);
