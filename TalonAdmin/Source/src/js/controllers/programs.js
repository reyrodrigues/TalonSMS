"use strict";

app.controller('ProgramsCreateCtrl', ['$scope', '$scope', 'ControllerFactory', 'settings', '$q', 'injectorHelper',
    function ($rootScope, $scope, ControllerFactory, settings, $q, injectorHelper) {
        injectorHelper.injectPromises($scope, ['locations', 'voucherTypes', 'vendorTypes']);

        ControllerFactory.Create($scope, angular.extend({
            defaults: { VoucherCodeLength: 6, Date: moment().toDate() }
        }, settings));

        $rootScope.$watch("currentUser", function () {
            if ($rootScope.currentUser) {
                $scope.entity.CreatedBy = $rootScope.currentUser.Id;
                $scope.entity.CreatedOn = moment().toDate();

                $scope.entity.ModifiedBy = $rootScope.currentUser.Id;
                $scope.entity.ModifiedOn = moment().toDate();
            }
        })
    }]);


app.controller('ProgramsEditCtrl', ['breeze', 'backendService', '$rootScope', '$scope', '$state', '$q', '$http', 'ControllerFactory',
    'dialogs', 'serviceBase', 'toaster', 'gettext', 'settings', 'injectorHelper',
    function (breeze, backendService, $rootScope, $scope, $state, $q, $http, ControllerFactory,
        dialogs, serviceBase, toaster, gettext, settings, injectorHelper) {
        injectorHelper.injectPromises($scope, ['locations', 'voucherTypes', 'vendorTypes']);
        $scope.categories = [];

        settings = angular.extend({
            expand: ['Distributions', 'Distributions.Vouchers', 'Distributions.Vouchers.TransactionRecords'],
            preSave: function () {
                $scope.entity.ModifiedBy = $rootScope.currentUser.Id;
                $scope.entity.ModifiedOn = moment().utc().toDate();
            },
            postSave: function () {
                var saveList = [];
                $scope.categories.forEach(function (d) {
                    saveList.push(d);
                });
                return backendService.saveChanges(saveList);
            }
        } ,settings);

        ControllerFactory.Edit($scope, settings);

        $scope.removeCategory = function (category) {
            category.entityAspect.setDeleted();

            $scope.categories = $scope.categories.filter(function (c) {
                return c.Id != category.Id;
            });
        };

        $scope.addCategory = function () {
            var category = backendService.createEntity("ProgramVoucherCategory", { ProgramId: $scope.entity.Id });
            $scope.categories.push(category);
        };

        $scope.assignToGroup = function () {
           /* $scope.isAssigning = true;
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
            }*/
        }; // end launch

        var watchFunction = function () {
            $scope.loadGridData();
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

        $scope.isAssigning = false;


        var logQuery = breeze.EntityQuery.from('DistributionLogs')
        .where({
            'and': [
                { 'Distribution.ProgramId': { '==': $state.params.id } },
                { 'EndedOn': { '==': null } }
            ]
        })
        .take(0)
        .using(backendService)
        .inlineCount(true)
        .execute(function (results) {
            $scope.isAssigning = results.inlineCount > 0;

            $scope.loadData().then(function () {
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
                $scope.VendorsFilter = {};
            }
            $scope.VendorsLoadGrid();
        };

        ControllerFactory.ChildGrid($scope, {
            collectionType: 'VoucherTransactionRecords',
            name: 'UsedVouchers',
            key: 'Voucher.Distribution.ProgramId',
            expand: ['Voucher', 'Voucher.Category', 'Voucher.Category.Type', 'Vendor', 'Beneficiary'],
            columns: [
                ["Voucher.Distribution.Title", gettext("Distribution"), '{{COL_FIELD}}'],
                ["Voucher.Distribution.Id", gettext("Code"), '{{COL_FIELD}}'],
                ["Beneficiary.FirstName", gettext("Beneficiary"), '{{row.getProperty(\'Beneficiary.Name\')}}'],
                ["Vendor.Name", gettext("Vendor")],
                ["FinalizedOn", gettext("Redemption Date"), '{{COL_FIELD|localeDateTime}}'],
                ["Voucher.VoucherCode", gettext("Voucher Code")],
                ["ConfirmationCode", gettext("Confirmation Code")],
                ["Voucher.Category.Value", gettext("Value"), '{{ COL_FIELD|currency:(country.CurrencyIsoCode + " "||"$") }}']
            ]
        });

        ControllerFactory.ChildGrid($scope, {
            collectionType: 'VoucherTransactionRecords',
            name: 'UnusedVouchers',
            key: 'Voucher.Distribution.ProgramId',
            expand: ['Voucher', 'Voucher.Category', 'Voucher.Category.Type', 'Beneficiary'],
            columns: [
                ["Voucher.Distribution.Title", gettext("Distribution"), '{{COL_FIELD}}'],
                ["Voucher.Distribution.Id", gettext("Code"), '{{COL_FIELD}}'],
                ["Beneficiary.FirstName", gettext("Beneficiary"), '{{row.getProperty(\'Beneficiary.Name\')}}'],
                ["Beneficiary.MobileNumber", gettext("Mobile Number")],
                ["CreatedOn", gettext("Sent On"), '{{COL_FIELD|localeDateTime}}'],
                ["Voucher.VoucherCode", gettext("Voucher Code")],
                ["Voucher.Category.Value", gettext("Value"), '{{ COL_FIELD|currency:(country.CurrencyIsoCode + " "||"$") }}'],
                ["Id", gettext("Actions"), '<div ng-if="row.getProperty(\'Status\') < 2"><a href ng-click="cancelVoucher(row.getProperty(\'Voucher.Id\'))">Cancel Voucher</a>&nbsp;|&nbsp;' +
                        '<a href ng-click="resendVoucher(row.getProperty(\'Voucher.Id\'), row.getProperty(\'BeneficiaryId\'))">Resend Voucher</a>']
            ]
        });

        ControllerFactory.ChildGrid($scope, {
            collectionType: 'ProgramVendors',
            name: 'Vendors',
            entityType: 'Vendor',
            parameters: { 'programId': $state.params.id },
            expand: ['Location', 'Type'],
            columns: [
                ["FirstName", gettext("Name"), '{{row.getProperty("Name")}}'],
                ["MobileNumber", gettext("Mobile Number")],
                ["Location.Name", gettext("Location")],
                ["Type.Name", gettext("Type")],
            ]
        });

        ControllerFactory.ChildGrid($scope, {
            collectionType: 'DistributionLogs',
            name: 'DistributionLog',
            entityType: 'DistributionLog',
            key: 'Distribution.ProgramId',
            columns: [
                ["Distribution.Title", gettext("Distribution"), '{{COL_FIELD}}'],
                ["StartedOn", gettext("Date"), '{{COL_FIELD|localeDateTime}}'],
                ["AffectedBeneficiaries", gettext("Beneficiaries")]
            ]
        });

    }]);
