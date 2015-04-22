'use strict';

app.controller('BeneficiariesCreateCtrl', ['$scope', 'createController', 'locations', 'groups',
    function ($scope, createController, locations, groups) {
        $scope.locations = locations;
        $scope.groups = groups;

        createController($scope, {
            entityType: 'Beneficiary',
            editState: 'beneficiaries.edit'
        });
    }]);

app.controller('BeneficiariesEditCtrl', ['$scope', 'editController', 'gettext', 'subGrid', 'locations', 'groups', 'backendService',
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
            expand: ['Voucher', 'Voucher.Category', 'Vendor'],
            columns: [
                ["Status", gettext("Status"), '{{statusToString(COL_FIELD)}}'],
                ["FinalizedOn", gettext("Redemption Date"), '{{COL_FIELD|localeDateTime}}'],
                ["Vendor.Name", gettext("Vendor"), false, false],
                ["Voucher.VoucherCode", gettext("Voucher Code")],
                ["Voucher.Category.Value", gettext("Value")]
            ]
        });

        $scope.loadData()
            .then(function () {
                $scope.VoucherTransactionRecordsLoadGrid();
            });
    }]);

app.controller('BeneficiariesListCtrl', ['$scope', '$state', '$localStorage', 'listController', 'gettext', 'dialogs', 'toaster', 'serviceBase', '$location', 'settings', '$injector',
    function ($scope, $state, $localStorage, listController, gettext, dialogs, toaster, serviceBase, $location, settings, $injector) {
        var storageSetting = $state.current.name + 'GridSettings';
        $scope.showingDisabled = false;
        $scope.genericSettings = settings;

        var localStorageService = $injector.get('localStorageService');
        var authData = localStorageService.get('authorizationData');
        $scope.token = authData.token;
        $scope.exportUrl = serviceBase + 'api/Excel/ExportBeneficiaries?countryId=' + $localStorage.country.Id + '&organizationId=' + $localStorage.organization.Id;

        listController($scope, angular.extend({
            expand: ['Location', "Group"],
            columns: [
                ["FirstName", gettext("Name"), '<a href ui-sref="beneficiaries.edit({ id: row.getProperty(\'Id\') })">{{ row.getProperty(\'Name\')}}</a>'],
                ["BirthYear", gettext("Birth Year"), '<a href ui-sref="beneficiaries.edit({ id: row.getProperty(\'Id\') })">{{COL_FIELD}}</a>'],
                ["NationalId", gettext("National Id Number")],
                ["MobileNumber", gettext("Mobile Number")],
                ["Location.Name", gettext("Location")],
                ["Group.Name", gettext("Group")]
            ]
        }, settings));

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

        $scope.exportBeneficiaries = function () {
            var url = serviceBase + 'api/Excel/ExportBeneficiaries?countryId=' + $localStorage.country.Id + '&organizationId=' + $localStorage.organization.Id;

            document.location = url;
        }

        $scope.importBeneficiaries = function () {
            var dlg = dialogs.create('tpl/dialogs/importBeneficiaries.html', 'ImportBeneficiariesCtrl');
            dlg.result.then(function (result) {
                if (result) {
                    if (!result.Errors.length) {
                        toaster.pop('success', gettext('Success'), gettext('Beneficiaries successfuly imported.'));
                    } else {
                        toaster.pop('warning', gettext('Notice'), gettext('Some beneficiaries were not imported correctly.'));

                        result.Errors.forEach(function (e) {
                            toaster.pop('error', gettext('Error'), gettext('Error importing beneficiary. Message from server:\n') + e.ErrorText + "\nLine: " + e.Line);
                        });
                    }

                    $scope.loadGridData();
                }
            }).catch(function (res) {
                toaster.pop('error', gettext('Error'), res.data);
            });
        };

        $scope.loadGridData();
    }]);

app.controller('ImportBeneficiariesCtrl', ['breeze', 'serviceBase', '$scope', '$q', '$modalInstance', '$upload', '$localStorage',
    function (breeze, serviceBase, $scope, $q, $modalInstance, $upload, $localStorage) {
        $scope.files = [];

        $scope.upload = function () {
            $scope.uploading = $upload.upload({
                url: serviceBase + 'api/Excel/ImportBeneficiaries?countryId=' + $localStorage.country.Id + '&organizationId=' + $localStorage.organization.Id,
                file: $scope.files.pop()
            }).then(function (result) {
                $modalInstance.close(result.data);
            }).catch(function () {
                console.log(arguments);
            });
        };

        $scope.close = function () {
            $modalInstance.close(false);
        };
    }]);

app.controller('BeneficiaryBulkEditCtrl', ['$scope', '$state', 'dialogs', 'listController', 'gettext', 'locations', 'backendService', 'toaster',
    function ($scope, $state, dialogs, listController, gettext, locations, backendService, toaster) {
        var storageSetting = $state.current.name + 'GridSettings';
        $scope.locations = locations;
        $scope.bulkFilters = {
            BirthDateFrom: null,
            BirthDateTo: null
        };

        listController($scope, {
            collectionType: 'Beneficiaries',
            expand: ['Location', "Group"],
            columns: [
                ["FirstName", gettext("Name"), '<a href ui-sref="beneficiaries.edit({ id: row.getProperty(\'Id\') })">{{ row.getProperty(\'Name\')}}</a>'],
                ["BirthDate", gettext("Date of Birth"), '<a href ui-sref="beneficiaries.edit({ id: row.getProperty(\'Id\') })">{{COL_FIELD}}</a>'],
                ["NationalId", gettext("National Id Number")],
                ["MobileNumber", gettext("Mobile Number")],
                ["Location.Name", gettext("Location")],
                ["Group.Name", gettext("Group")]
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
            if (bulkFilters.BirthDateFrom) {
                filter['and'].push({ 'BirthDate': { '>=': moment(bulkFilters.BirthDateFrom).toJSON() } });
            }
            if (bulkFilters.BirthDateTo) {
                filter['and'].push({ 'BirthDate': { '<=': moment(bulkFilters.BirthDateTo).toJSON() } });
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
