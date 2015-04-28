'use strict';

app.controller('DistributionsCreateCtrl', ['$scope', '$scope', 'createController', 'settings', '$q', 'controlledLists',
    function ($rootScope, $scope, createController, settings, $q, controlledLists) {
        $q.all([controlledLists.getLocations(), controlledLists.getVoucherTypes(), controlledLists.getVendorTypes(), controlledLists.getPrograms()]).then(function (promises) {
            $scope.locations = promises[0];
            $scope.voucherTypes = promises[1];
            $scope.vendorTypes = promises[2];
            $scope.programs = promises[3];
        });

        createController($scope, angular.extend({
            defaults: { VoucherCodeLength: 6, Date: moment().toDate() }
        }, settings));

        $scope.entity.CreatedBy = $rootScope.currentUser.Id;
        $scope.entity.CreatedOn = moment().toDate();

        $scope.entity.ModifiedBy = $rootScope.currentUser.Id;
        $scope.entity.ModifiedOn = moment().toDate();
    }]);


app.controller('DistributionsEditCtrl', ['breeze', 'backendService', '$rootScope', '$scope', '$state', '$q', '$http', 'controlledLists',
    'dialogs', 'serviceBase', 'toaster', 'gettext', 'subGrid', 'injectorHelper',
    function (breeze, backendService, $rootScope, $scope, $state, $q, $http, controlledLists,
        dialogs, serviceBase, toaster, gettext, subGrid, injectorHelper) {
        injectorHelper.injectPromises($scope, ['programs', 'locations', 'voucherTypes', 'vendorTypes']);

        $scope.save = function (andContinue) {
            $scope.isEditing = false;

            $scope.entity.ModifiedBy = $rootScope.currentUser.Id;
            $scope.entity.ModifiedOn = moment().utc().toDate();

            var saveList = [];
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
            $scope.isAssigning = true;
            if (!$scope.categories || !$scope.categories.length) {
                toaster.pop('error', gettext('Error'), gettext('Please add one voucher type to the distribution before assigning it to a group.'));
            } else {
                var dlg = dialogs.create('tpl/dialogs/assignToGroup.html', 'AssignToGroupDialogCtrl', $scope.data);
                dlg.result.then(function (group) {
                    if (group) {
                        var payload = { DistributionId: $scope.entity.Id, GroupId: group.Id };

                        $http.post(serviceBase + 'Api/VoucherWorkflow/AssignToGroup', payload)
                        .then(function () {
                            toaster.pop('success', 'Success!', 'Vouchers created successfully!');
                            loadData().then(function () {
                                $scope.UsedVouchersLoadGrid();
                                $scope.UnusedVouchersLoadGrid();
                                $scope.VendorsLoadGrid();
                                $scope.DistributionLogLoadGrid();
                            });
                            $scope.isAssigning = false;
                        }).catch(function (res) {
                            toaster.pop('error', 'Error', res.data.Message);
                            $scope.isAssigning = false;
                        });
                    } else {
                        $scope.isAssigning = false;
                    }
                });
            }
        }; // end launch

        var watchFunction = function () {
            $scope.loadGridData();
        };
        var loadData = function () {
            var entityQuery = query.where("Id", "==", $state.params.id).take(1).execute();
            return $q.all([entityQuery]).then(function (responses) {
                var entity = responses[0].results.pop();

                $scope.entity = entity;
                $scope.categories = entity.Categories;
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
            var dlg = dialogs.confirm(gettext("Confirm"), gettext("Are you sure you would like to resend this voucher?"));
            dlg.result.then(function (r) {
                var payload = { VoucherId: voucherId, BeneficiaryId: beneficiaryId };

                $http.post(serviceBase + 'Api/VoucherWorkflow/ResendSMS', payload)
                    .then(function () {
                        toaster.pop('success', 'Success!', 'Voucher resent successfully!');
                        loadData();
                    }).catch(function (res) {
                        toaster.pop('error', 'Error', res.data.Message);
                    });
            });
        };
        $scope.cancelVoucher = function (voucherId) {
            var dlg = dialogs.confirm(gettext("Confirm"), gettext("Are you sure you would like to cancel this voucher?"));
            dlg.result.then(function (r) {
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
            });
        };

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

        $scope.isEditing = false;
        $scope.isAssigning = false;

        var query = new breeze.EntityQuery('Distributions')
            .expand('Beneficiaries')
            .expand('Categories')
            .using(backendService);

        var logQuery = breeze.EntityQuery.from('DistributionLogs')
        .where({
            'and': [
                { 'DistributionId': { '==': $state.params.id } },
                { 'EndedOn': { '==': null } }
            ]
        })
        .take(0)
        .using(backendService)
        .inlineCount(true)
        .execute(function (results) {
            $scope.isAssigning = results.inlineCount > 0;

            loadData().then(function () {
                $scope.UsedVouchersLoadGrid();
                $scope.UnusedVouchersLoadGrid();
                $scope.VendorsLoadGrid();
                $scope.DistributionLogLoadGrid();
            });
        }).catch(function () {
            console.log(arguments);
        });

        delete window.lockAssignment;
        delete window.unlockAssignment;

        window.lockAssignment = function (distributionId) {
            if ($state.params.id == distributionId)
                $scope.isAssigning = true;
        };
        window.unlockAssignment = function (distributionId) {
            if ($state.params.id == distributionId)
                $scope.isAssigning = false;
        };


        $scope.UnusedVouchersFilter = { 'Status': { '==': 0 } };
        $scope.UsedVouchersFilter = { 'Status': { '==': 2 } };

        $scope.search = {}

        $scope.searchUnused = function () {
            if ($scope.search.UnusedVouchers) {
                $scope.UnusedVouchersFilter = {
                    'and': [
                        { 'Status': { '==': 0 } },
                        {
                            'or': [
                              { 'Beneficiary.FirstName': { 'contains': $scope.search.UnusedVouchers } },
                              { 'Beneficiary.LastName': { 'contains': $scope.search.UnusedVouchers } },
                              { 'Beneficiary.MobileNumber': { 'contains': $scope.search.UnusedVouchers } },
                              { 'Voucher.VoucherCode': { 'contains': $scope.search.UnusedVouchers } }
                            ]
                        }
                    ]
                };
            } else {
                $scope.UnusedVouchersFilter = { 'Status': { '==': 0 } };
            }
            $scope.UnusedVouchersLoadGrid();
        };
        $scope.searchUsed = function () {
            if ($scope.search.UsedVouchers) {
                $scope.UsedVouchersFilter = {
                    'and': [
                        { 'Status': { '==': 2 } },
                        {
                            'or': [
                              { 'Beneficiary.FirstName': { 'contains': $scope.search.UsedVouchers } },
                              { 'Beneficiary.LastName': { 'contains': $scope.search.UsedVouchers } },
                              { 'Vendor.FirstName': { 'contains': $scope.search.UsedVouchers } },
                              { 'Vendor.LastName': { 'contains': $scope.search.UsedVouchers } },
                              { 'Voucher.VoucherCode': { 'contains': $scope.search.UsedVouchers } },
                              { 'ConfirmationCode': { 'contains': $scope.search.UsedVouchers } }
                            ]
                        }
                    ]
                };
            } else {
                $scope.UsedVouchersFilter = { 'Status': { '==': 2 } };
            }
            $scope.UsedVouchersLoadGrid();
        };
        $scope.searchVendor = function () {

            if ($scope.search.Vendors) {
                $scope.VendorsFilter = {
                    'or': [
                      { 'FirstName': { 'contains': $scope.search.Vendors } },
                      { 'LastName': { 'contains': $scope.search.Vendors } }
                    ]
                };
            } else {
                $scope.VendorsFilter = { };
            }
            $scope.VendorsLoadGrid();
        };

        subGrid($scope, {
            collectionType: 'VoucherTransactionRecords',
            name: 'UsedVouchers',
            key: 'Voucher.DistributionId',
            expand: ['Voucher', 'Voucher.Category', 'Voucher.Category.Type', 'Vendor', 'Beneficiary'],
            columns: [
                ["Beneficiary.FirstName", gettext("Beneficiary"), '{{row.getProperty(\'Beneficiary.Name\')}}'],
                ["Vendor.Name", gettext("Vendor")],
                ["FinalizedOn", gettext("Redemption Date"), '{{COL_FIELD|localeDateTime}}'],
                ["Voucher.VoucherCode", gettext("Voucher Code")],
                ["ConfirmationCode", gettext("Confirmation Code")],
                ["Voucher.Category.Value", gettext("Value"), '{{ COL_FIELD|currency:(country.CurrencyIsoCode + " "||"$") }}']
            ]
        });

        subGrid($scope, {
            collectionType: 'VoucherTransactionRecords',
            name: 'UnusedVouchers',
            key: 'Voucher.DistributionId',
            expand: ['Voucher', 'Voucher.Category', 'Voucher.Category.Type', 'Beneficiary'],
            columns: [
                ["Beneficiary.FirstName", gettext("Beneficiary"), '{{row.getProperty(\'Beneficiary.Name\')}}'],
                ["Beneficiary.MobileNumber", gettext("Mobile Number")],
                ["CreatedOn", gettext("Sent On"), '{{COL_FIELD|localeDateTime}}'],
                ["Voucher.VoucherCode", gettext("Voucher Code")],
                ["Voucher.Category.Value", gettext("Value"), '{{ COL_FIELD|currency:(country.CurrencyIsoCode + " "||"$") }}'],
                ["Id", gettext("Actions"), '<div ng-if="row.getProperty(\'Status\') < 2"><a href ng-click="cancelVoucher(row.getProperty(\'Voucher.Id\'))">Cancel Voucher</a>&nbsp;|&nbsp;' +
                        '<a href ng-click="resendVoucher(row.getProperty(\'Voucher.Id\'), row.getProperty(\'BeneficiaryId\'))">Resend Voucher</a>']
            ]
        });

        subGrid($scope, {
            collectionType: 'DistributionVendors',
            name: 'Vendors',
            entityType: 'Vendor',
            parameters: { 'distributionId': $state.params.id },
            expand: ['Location', 'Type'],
            columns: [
                ["FirstName", gettext("Name"), '{{row.getProperty("Name")}}'],
                ["MobileNumber", gettext("Mobile Number")],
                ["Location.Name", gettext("Location")],
                ["Type.Name", gettext("Type")],
            ]
        });

        subGrid($scope, {
            collectionType: 'DistributionLogs',
            name: 'DistributionLog',
            entityType: 'DistributionLog',
            key: 'DistributionId',
            columns: [
                ["StartedOn", gettext("Started On"), '{{COL_FIELD|localeDateTime}}'],
                ["EndedOn", gettext("Ended On"), '{{COL_FIELD|localeDateTime}}'],
                ["AffectedBeneficiaries", gettext("Beneficiaries")]
            ]
        });

    }]);

app.controller('DistributionsListCtrl', ['breeze', 'backendService', '$scope', '$http', '$localStorage',
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
                    .expand(["Location", "Program"])
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
                { field: "Location.Name", displayName: "Location" },
                { field: "Program.Name", displayName: "Program" }
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
                .catch(function () {
                    console.log(arguments);
                });
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

app.controller('AssignToGroupDialogCtrl', ['breeze', 'backendService', '$scope', '$q', '$modalInstance',
    function (breeze, backendService, $scope, $q, $modalInstance) {
        $scope.group = null;
        $scope.isAssigning = false;
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


        $scope.close = function () {
            $modalInstance.close();
        };

        $scope.done = function () {
            $scope.isAssigning = true;
            $modalInstance.close($scope.group);
        };
    }]);
