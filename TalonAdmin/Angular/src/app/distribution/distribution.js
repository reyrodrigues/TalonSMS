angular.module('talon.distribution', [
  'ui.router',
  'datatables',
  'datatables.bootstrap',
  'talon.dataAccess',
  'talon.common',
  'dialogs.main',
  'ngFileUpload'
].concat(ALL_IMPORTS))

.config(function config($stateProvider) {
    $stateProvider
    .state('distributions', {
        url: '/distribution',
        views: {
            "app": {
                template: "<div ui-view></div>"
            }
        },
        abstract: true,
        data: {
            settings: {
                collectionType: "Distributions",
                entityType: 'Distribution',
                expand: ['vouchers', 'categories', 'program', 'group'],
                form: 'distribution/form.tpl.html',
                controlledLists: ['programs', 'locations', 'voucherTypes', 'vendorTypes', 'beneficiaryGroups']
            }
        }
    })

    .state('distributions.list', {
        url: '/index',
        controller: 'GenericListCtrl as vm',
        templateUrl: 'index.tpl.html',
        data: {
            pageTitle: 'Distributions',
            settings: {
                columns: [
                    ['id', '#'],
                    ['program.name', 'Program'],
                    ['group.name', 'Cycle'],
                    ['title', 'Title']
                ]
            }
        }
    })

    .state('distributions.edit', {
        url: '/{id:int}',
        controller: 'DistributionEditController as vm',
        templateUrl: 'edit.tpl.html',
        data: {
            pageTitle: 'Distributions'
        }
    })

    .state('distributions.create', {
        url: '/create',
        controller: 'GenericEditCtrl as vm',
        templateUrl: 'create.tpl.html',
        data: {
            pageTitle: 'Distributions'
        }
    });
})
.controller('DistributionEditController', DistributionEditController)
;

DistributionEditController.prototype.configure = function () {
    this.$scope.cancelVoucher = cancelVoucher;
    this.$scope.resendVoucher = resendVoucher;
    this.$scope.assignVoucherBook = AssignVoucherBook;

    var dialogs = this.$injector.get('dialogs');
    var $http = this.$injector.get('$http');
    var $rootScope = this.$injector.get('$rootScope');
    var toaster = this.$injector.get('toaster');
    var entityManagerFactory = this.$injector.get('entityManagerFactory');
    var entityManager = this.entityManager;
    var self = this;

    this.forms = [
        {
            label: "Download Test Sheet",
            css: "btn-info",
            condition: function (entity) {
                return $rootScope.canI('Generate Test Sheet');
            },
            url: function action() {
                return serviceRoot + 'Api/Reports/GenerateTestSheet?DistributionId=' + self.entity.id;
            }
        }
    ];

    function AssignVoucherBook(beneficiary, grid) {
        var dlg = dialogs.create('distribution/assign-voucher-book.tpl.html', function ($scope, $modalInstance) {
            $scope.serialNumber = "";

            $scope.save = function () {
                $modalInstance.close($scope.serialNumber);
            };

            $scope.close = function () {
                $modalInstance.close(false);
            };
        });

        dlg.result.then(function (result) {
            if (result) {
                var payload = {
                    BeneficiaryId: beneficiary.id,
                    DistributionId: self.entity.id,
                    SerialNumber: result
                };

                $http.post(serviceRoot + 'Api/VoucherWorkflow/AssignVoucherBook', payload)
                .then(function () {
                    toaster.pop('success', 'Success!', 'Voucher assigned successfully!');
                    grid.api.custom.reloadData();
                }).catch(function (res) {
                    toaster.pop('error', 'Error', res);
                });
            }
        });
    }

    function cancelVoucher(entity, grid) {
        var dlg = dialogs.confirm("Confirm", "Are you sure you would like to cancel this voucher?");
        dlg.result.then(function (r) {
            var query = entityManagerFactory.entityQuery('IssuedVoucherTransactionRecords')
                .where("voucher.id", "==", entity.voucherId)
                .using(entityManager)
                .execute()
            .then(function (res) {
                var voucher = res.results.pop();
                voucher.status = 3;
                entityManager.saveChanges([voucher]).then(function () {
                    $http.post(serviceRoot + 'Api/VoucherWorkflow/CancelVoucher', { VoucherId: entity.voucherId })
                    .then(function () {
                        grid.api.custom.reloadData();
                    });
                });
            });
        });
    }

    function resendVoucher(entity, grid) {
        console.log(grid, entity);

        var dlg = dialogs.confirm("Confirm", "Are you sure you would like to resend this voucher?");
        dlg.result.then(function (r) {
            var payload = { VoucherId: entity.voucherId, BeneficiaryId: entity.beneficiaryId };

            $http.post(serviceRoot + 'Api/VoucherWorkflow/ResendSMS', payload)
                .then(function () {
                    toaster.pop('success', 'Success!', 'Voucher resent successfully!');
                    grid.api.custom.reloadData();
                }).catch(function (res) {
                    toaster.pop('error', 'Error', res.data.Message);
                });
        });
    }
};

function DistributionEditController($injector, $scope) {
    EditController.call(this, $injector, $scope);
}