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
    $scope.entity = backendService.createEntity("Distribution", { VoucherCodeLength: 6 });
}]);

app.controller('AssignToGroupDialogCtrl', ['breeze', 'backendService', '$scope', '$q', '$modalInstance',
    function (breeze, backendService, $scope, $q, $modalInstance) {
        $scope.group = null;
        $scope.getGroup = function (name) {
            var query = new breeze.EntityQuery('BeneficiaryGroups')
                .using(backendService)
                .where('Name', 'contains', name);

            return query.execute().then(function (res) {
                if (res.results) {
                    var groups = res.results;
                    return groups;
                }
            });
        };

        $scope.done = function () {
            $modalInstance.close($scope.group);
        };
    }]);

app.controller('VoucherDistributionEditCtrl', ['breeze', 'backendService', '$scope', '$state', '$q', '$http', 'locations', 'dialogs', 'voucherTypes', 'serviceBase', 'toaster',
    function (breeze, backendService, $scope, $state, $q, $http, locations, dialogs, voucherTypes, serviceBase, toaster) {
        $scope.save = function (andContinue) {
            $scope.isEditing = false;

            $scope.distributionVendors.forEach(function (d) {
                if ($scope.assignedVendors.indexOf(d.VendorId) == -1) {
                    $scope.entity.entityAspect.setDeleted();
                }
            });

            $scope.assignedVendors.forEach(function (d) {
                var vendorIds = $scope.distributionVendors.map(function (dv) { return dv.VendorId; });

                if (vendorIds.indexOf(d) == -1) {
                    var newDV = backendService.createEntity("DistributionVendor", { DistributionId: $scope.entity.Id, VendorId: d });
                    $scope.distributionVendors.push(newDV);
                }
            });

            var saveList = [];
            $scope.distributionVendors.forEach(function (d) {
                saveList.push(d);
            });
            $scope.categories.forEach(function (d) {
                saveList.push(d);
            });
            saveList.push($scope.entity);

            backendService.saveChanges(saveList).then(function () {
                if (!andContinue)
                    $state.go('distributions.list');
            }).catch(function () {
                console.log(arguments);
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
                    .expand(["Type", "TransactionRecord", "TransactionRecord.Beneficiary"])
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
                    }).catch(function () { console.log(arguments); });
            }, 100);
        };
        $scope.removeCategory = function (category) {
            category.entityAspect.setDeleted();

            $scope.categories = $scope.categories.filter(function (c) {
                return c.Id != category.Id;
            });
        };
        $scope.addCategory = function () {
            var category = backendService.createEntity("DistributionVoucherCategory", { DistributionId: $scope.entity.Id });
            $scope.categories.push(category);
        };
        $scope.generateVouchers = function () {
            toaster.pop('success', 'Processing', 'Vouchers are being generated.');

            $http.post(serviceBase + 'Api/VoucherWorkflow/GenerateVouchers', { DistributionId: $scope.entity.Id })
                .then(function (res) {
                    toaster.pop('success', 'Success!', 'Vouchers created successfully!');
                    loadData();
                })
                .catch(function (res) {
                    toaster.pop('error', 'Error', res.data.Message);
                });
        };
        $scope.assignToGroup = function () {
            var dlg = dialogs.create('tpl/dialogs/assignToGroup.html', 'AssignToGroupDialogCtrl', $scope.data);
            dlg.result.then(function (group) {
                var payload = { DistributionId: $scope.entity.Id, GroupId: group.Id };

                $http.post(serviceBase + 'Api/VoucherWorkflow/AssignToGroup', payload)
                .then(function () {
                    toaster.pop('success', 'Success!', 'Vouchers created successfully!');
                    loadData();
                }).catch(function (res) {
                    toaster.pop('error', 'Error', res.data.Message);
                });
            });
        }; // end launch

        var watchFunction = function () {
            $scope.loadGridData();
        };
        var loadData = function () {


            var vendorQuery = new breeze.EntityQuery("Vendors")
            .using(backendService)
            .execute();


            var assignedVendorQuery = new breeze.EntityQuery("DistributionVendors")
            .where("DistributionId", "==", $state.params.id)
            .using(backendService)
            .execute();


            var entityQuery = query.where("Id", "==", $state.params.id).take(1).execute();
            $q.all([entityQuery, vendorQuery, assignedVendorQuery]).then(function (responses) {
                var entity = responses[0].results.pop();
                var vendors = responses[1].results;
                var assignedVendors = responses[2].results;

                $scope.entity = entity;
                $scope.categories = entity.Categories;
                $scope.vendors = vendors;
                $scope.distributionVendors = assignedVendors;
                $scope.assignedVendors = $scope.distributionVendors.map(function (dv) { return dv.VendorId; });

                $scope.loadGridData();
            });
        };
        $scope.toggleVendor = function (vendor) {
            if ($scope.assignedVendors.indexOf(vendor.Id) > -1) {
                $scope.assignedVendors = $scope.assignedVendors.filter(function (v) { return v != vendor.Id; });
            } else {
                $scope.assignedVendors.push(vendor.Id);
            }
        };
        $scope.resendVoucher = function (voucherId, beneficiaryId) {

            var payload = { VoucherId: voucherId, BeneficiaryId: beneficiaryId };

            $http.post(serviceBase + 'Api/VoucherWorkflow/ResendSMS', payload)
                .then(function () {
                    toaster.pop('success', 'Success!', 'Voucher resent successfully!');
                    loadData();
                }).catch(function (res) {
                    toaster.pop('error', 'Error', res.data.Message);
                });
        };
        $scope.cancelVoucher = function (voucherId) {
            console.log(arguments);
            var query = new breeze.EntityQuery('VoucherTransactionRecords')
                .where("Voucher.Id", "==", voucherId)
                .using(backendService)
                .execute()
            .then(function (res) {
                var voucher = res.results.pop();
                voucher.Status = 3;
                backendService.saveChanges([voucher]).then(function () {
                    $http.post(serviceBase + 'Api/VoucherWorkflow/CancelVoucher', { VoucherId: voucherId })
                    .then(function () {
                        $scope.loadGridData();
                    });
                });
            });
        };

        $scope.statusToString = function (status) {
            if (typeof(status) == 'undefined')
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
                { field: "Value", displayName: "Value" },
                { field: "TransactionRecord.Beneficiary.Name", displayName: "Beneficiary" },
                {
                    field: "TransactionRecord.Status", displayName: "Status",
                    cellTemplate: '<div class="ngCellText" ng-class="col.colIndex()"><span ng-cell-text>{{statusToString(COL_FIELD)}}</span></div>'
                },
                {
                    field: "Id",
                    displayName: "Actions",
                    cellTemplate: '<div class="ngCellText" ng-class="col.colIndex()"><div ng-if="row.getProperty(\'TransactionRecord.Status\') < 2"><a href ng-click="cancelVoucher(row.getProperty(\'Id\'))">Cancel Voucher</a>&nbsp;|&nbsp;' +
                        '<a href ng-click="resendVoucher(row.getProperty(\'Id\'), row.getProperty(\'TransactionRecord.BeneficiaryId\'))">Resend Voucher</a></div>'
                }
            ]
        };

        var query = new breeze.EntityQuery('Distributions')
            .expand('Beneficiaries')
            .expand('Categories')
            .using(backendService);

        loadData();

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

app.controller('VoucherGridCtrl', ['breeze', 'backendService', '$scope', '$http', '$localStorage',
    function (breeze, backendService, $scope, $http, $localStorage) {

        var storageSetting = $state.current.name + 'GridSettings';
        $scope.genericSettings = settings;

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
                    field: "VoucherCode",
                    displayName: 'Voucher Code'
                }
            ]
        };

        $scope.$watch('pagingOptions', watchFunction, true);
        $scope.$watch('gridOptions.sortInfo', watchFunction, true);

        $scope.loadGridData();

    }]);
