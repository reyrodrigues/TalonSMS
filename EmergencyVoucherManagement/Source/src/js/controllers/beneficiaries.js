'use strict';

app.controller('BeneficiaryRegisterCtrl', ['$scope', 'createController', 'locations', 'groups',
    function ($scope, createController, locations, groups) {
        $scope.locations = locations;
        $scope.groups = groups;

        createController($scope, {
            entityType: 'Beneficiary',
            editState: 'beneficiaries.edit'
        });
    }]);

app.controller('BeneficiaryEditCtrl', ['$scope', 'editController', 'gettext', 'subGrid', 'locations', 'groups',
    function ($scope, editController, gettext, subGrid, locations, groups) {
        $scope.locations = locations;
        $scope.groups = groups;
        $scope.statusToString = function (status) {
            if (typeof (status) == 'undefined')
                return "Not Assigned";

            status = parseInt(status);
            if (status == 0) {
                return "SMS Sent"
            } else if (status == 2) {
                return "Used";
            } else if (status == 3) {
                return "Cancelled";
            }
        };

        $scope.deactivate = function () {
            $scope.entity.Disabled = true;

            backendService.saveChanges([$scope.entity])
            .then(function () {

            }).catch(function () {

            });
        };

        $scope.reactivate = function () {
            $scope.entity.Disabled = false;
            backendService.saveChanges([$scope.entity])
            .then(function () {

            }).catch(function () {

            });
        };

        editController($scope, {
            entityType: 'Beneficiary',
            collectionType: 'Beneficiaries',
            listState: 'beneficiaries.list',
            expand: ['Distributions']
        });

        subGrid($scope, {
            collectionType: 'VoucherTransactionRecords',
            key: 'BeneficiaryId',
            expand: ['Voucher', 'Vendor'],
            columnDefs: [
                {
                    field: "Status", displayName: gettext("Status"),
                    cellTemplate: '<div class="ngCellText" ng-class="col.colIndex()"><span ng-cell-text>{{statusToString(COL_FIELD)}}</span></div>'
                },
                { field: "Vendor.Name", displayName: gettext("Vendor") },
                { field: "Voucher.VoucherCode", displayName: gettext("Voucher Code") },
                { field: "Voucher.Value", displayName: gettext("Value") }
            ]
        });

        $scope.loadData()
            .then(function () {
                $scope.VoucherTransactionRecordsLoadGrid();
            });
    }]);

app.controller('BeneficiaryGridCtrl', ['$scope', 'listController',
    function ($scope, listController) {
        listController($scope, {
            collectionType: 'Beneficiaries',
            expand: 'Location',
            columnsDefs:  [
                { field: "Name", displayName: gettext("Name"), cellTemplate: '<div class="ngCellText" ng-class="col.colIndex()"><span ng-cell-text><a href ui-sref="beneficiaries.edit({ id: row.getProperty(\'Id\') })">{{COL_FIELD}}</a></span></div>' },
                { field: "DateOfBirth", displayName: gettext("Date of Birth"), cellTemplate: '<div class="ngCellText" ng-class="col.colIndex()"><span ng-cell-text><a href ui-sref="beneficiaries.edit({ id: row.getProperty(\'Id\') })">{{COL_FIELD|localeDate}}</a></span></div>' },
                { field: "NationalId", displayName: gettext("National Id Number") },
                { field: "MobileNumber", displayName: gettext("Mobile Number") },
                { field: "Location.Name", displayName: gettext("Location") }
            ]
        });

        $scope.loadGridData();
    }]);

app.controller('BeneficiaryBulkEditCtrl', ['breeze', 'backendService', '$scope', '$http', '$localStorage', 'locations', 'dialogs', 'toaster',
    function (breeze, backendService, $scope, $http, $localStorage, locations, dialogs, toaster) {
        $scope.loadGridData = function () {
            var currentPage = parseInt($scope.pagingOptions.currentPage);
            var pageSize = parseInt($scope.pagingOptions.pageSize);

            var fields = [];
            for (var i = 0; i < $scope.gridOptions.sortInfo.fields.length; i++) {
                var ordering = $scope.gridOptions.sortInfo.fields[i] + ($scope.gridOptions.sortInfo.directions[i] == "desc" ? " desc" : "");

                fields.push(ordering);
            }

            var order = fields.join(',');

            var entityQuery = new breeze.EntityQuery("Beneficiaries")
                .expand(["Location", "Group"])
                .orderBy(order)
                .skip(pageSize * (currentPage - 1))
                .take(pageSize)
                .inlineCount(true)
                .using(backendService);

            if ($scope.filter) {
                entityQuery = entityQuery.where($scope.filter);
            }
            entityQuery
                .execute().then(function (res) {
                    $scope.totalServerItems = res.inlineCount;
                    $scope.myData = res.results;

                    if (!$scope.$$phase) {
                        $scope.$apply();
                    }
                })
                .catch(function () { console.log(arguments); });

        };

        var watchFunction = function () {
            $scope.loadGridData();
        };

        $scope.locations = locations;
        $scope.bulkFilters = {
            DateOfBirthFrom: null,
            DateOfBirthTo: null
        };
        $scope.filterOptions = {
            filterText: "",
            useExternalFilter: true
        };
        $scope.totalServerItems = 0;
        $scope.pagingOptions = {
            pageSizes: [250, 500, 1000],
            pageSize: 250,
            currentPage: 1
        };

        $scope.gridOptions = {
            data: 'myData',
            enablePaging: true,
            showFooter: true,
            rowHeight: 36,
            headerRowHeight: 36,
            totalServerItems: 'totalServerItems',
            sortInfo: {
                fields: ['Name'],
                directions: ['asc']
            },
            pagingOptions: $scope.pagingOptions,
            filterOptions: $scope.filterOptions,
            enableRowSelection: false,
            useExternalSorting: true,
            columnDefs: [
                { field: "Name" },
                { field: "ParsedDate", displayName: "Date of Birth" },
                { field: "NationalId", displayName: "National Id Number" },
                { field: "MobileNumber", displayName: "Mobile Number" },
                { field: "Location.Name", displayName: "Location" },
            { field: "Group.Name", displayName: "Group" }
            ]
        };
        $scope.runFilters = function () {
            var filter = {
                'and': []
            };
            var bulkFilters = $scope.bulkFilters;

            if (bulkFilters.Sex) {
                filter['and'].push({ 'Sex': { '==': bulkFilters.Sex } });
            }
            if (bulkFilters.Location && bulkFilters.Location.Id) {
                filter['and'].push({ 'LocationId': { '==': bulkFilters.Location.Id } });
            }
            if (bulkFilters.DateOfBirthFrom) {
                filter['and'].push({ 'DateOfBirth': { '>=': moment(bulkFilters.DateOfBirthFrom).toJSON() } });
            }
            if (bulkFilters.DateOfBirthTo) {
                filter['and'].push({ 'DateOfBirth': { '<=': moment(bulkFilters.DateOfBirthTo).toJSON() } });
            }

            if (filter.and.length) {
                $scope.filter = filter;
                watchFunction();
            } else {
                $scope.filter = false;
                watchFunction();
            }
        };


        $scope.assignToGroup = function () {
            var dlg = dialogs.create('tpl/dialogs/assignToGroup.html', 'AssignToGroupDialogCtrl');
            dlg.result.then(function (group) {
                $scope.myData.forEach(function (e) {
                    e.GroupId = group.Id;
                });

                backendService.saveChanges($scope.myData)
                .then(function () {
                    toaster.pop('success', 'Success!', 'Beneficiaries added to group.');
                    $scope.loadGridData();
                }).catch(function (res) {
                    toaster.pop('error', 'Error', res.data);
                });
            });
        }; // end launch

        $scope.$watch('bulkFilters', $scope.runFilters, true);
        $scope.$watch('pagingOptions', watchFunction, true);
        $scope.$watch('gridOptions.sortInfo', watchFunction, true);
        $scope.filterLocations = function (name) {
            return $scope.locations.filter(function (l) { return l.Name.toLowerCase().indexOf(name.toLowerCase()) > -1; });
        }
        $scope.loadGridData();
    }]);