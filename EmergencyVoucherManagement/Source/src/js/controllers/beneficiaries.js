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

app.controller('BeneficiaryEditCtrl', ['$scope', 'editController', 'gettext', 'subGrid', 'locations', 'groups', 'backendService',
    function ($scope, editController, gettext, subGrid, locations, groups, backendService) {
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

app.controller('BeneficiaryGridCtrl', ['$scope', '$state', '$localStorage', 'listController', 'gettext',
    function ($scope, $state, $localStorage, listController, gettext) {
        var storageSetting = $state.current.name + 'GridSettings';
        $scope.showingDisabled = false;

        listController($scope, {
            collectionType: 'Beneficiaries',
            expand: ['Location', "Group"],
            columnDefs: [
                { field: "Name", displayName: gettext("Name"), cellTemplate: '<div class="ngCellText" ng-class="col.colIndex()"><span ng-cell-text><a href ui-sref="beneficiaries.edit({ id: row.getProperty(\'Id\') })">{{COL_FIELD}}</a></span></div>' },
                { field: "DateOfBirth", displayName: gettext("Date of Birth"), cellTemplate: '<div class="ngCellText" ng-class="col.colIndex()"><span ng-cell-text><a href ui-sref="beneficiaries.edit({ id: row.getProperty(\'Id\') })">{{COL_FIELD|localeDate}}</a></span></div>' },
                { field: "NationalId", displayName: gettext("National Id Number") },
                { field: "MobileNumber", displayName: gettext("Mobile Number") },
                { field: "Location.Name", displayName: gettext("Location") }
            ]
        });

        $scope.filter = { Disabled: { '!=': true } };
        $scope.showDisabled = function () {
            $scope.showingDisabled = true;
            $scope.filter['Disabled'] = { '==': true };

            $scope.loadGridData();
        };
        $scope.hideDisabled = function () {
            $scope.showingDisabled = false;
            $scope.filter['Disabled'] = { '!=': true };

            $scope.loadGridData();
        };

        $scope.loadGridData();
    }]);


app.controller('BeneficiaryBulkEditCtrl', ['$scope', '$state', 'dialogs', 'listController', 'gettext', 'locations', 'backendService', 'toaster',
    function ($scope, $state, dialogs, listController, gettext, locations, backendService, toaster) {
        var storageSetting = $state.current.name + 'GridSettings';
        $scope.locations = locations;
        $scope.bulkFilters = {
            DateOfBirthFrom: null,
            DateOfBirthTo: null
        };

        listController($scope, {
            collectionType: 'Beneficiaries',
            expand: ['Location', "Group"],
            columnDefs: [
                { field: "Name", displayName: gettext("Name"), cellTemplate: '<div class="ngCellText" ng-class="col.colIndex()"><span ng-cell-text><a href ui-sref="beneficiaries.edit({ id: row.getProperty(\'Id\') })">{{COL_FIELD}}</a></span></div>' },
                { field: "DateOfBirth", displayName: gettext("Date of Birth"), cellTemplate: '<div class="ngCellText" ng-class="col.colIndex()"><span ng-cell-text><a href ui-sref="beneficiaries.edit({ id: row.getProperty(\'Id\') })">{{COL_FIELD|localeDate}}</a></span></div>' },
                { field: "NationalId", displayName: gettext("National Id Number") },
                { field: "MobileNumber", displayName: gettext("Mobile Number") },
                { field: "Location.Name", displayName: gettext("Location") },
                { field: "Group.Name", displayName: "Group" }
            ]
        });

        $scope.assignToGroup = function () {
            var dlg = dialogs.create('tpl/dialogs/assignToGroup.html', 'AssignToGroupDialogCtrl');
            dlg.result.then(function (group) {
                $scope.list.forEach(function (e) {
                    e.GroupId = group.Id;
                });

                backendService.saveChanges($scope.list)
                .then(function () {
                    toaster.pop('success', gettext('Success'), gettext('Beneficiaries added to group.'));
                    $scope.loadGridData();
                }).catch(function (res) {
                    toaster.pop('error', gettext('Error'), res.data);
                });
            });
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
                $scope.loadGridData();
            } else {
                $scope.filter = false;
                $scope.loadGridData();

            }
        };

        $scope.filterLocations = function (name) {
            return $scope.locations.filter(function (l) { return l.Name.toLowerCase().indexOf(name.toLowerCase()) > -1; });
        }
        $scope.$watch('bulkFilters', $scope.runFilters, true);

        $scope.loadGridData();
    }]);
