angular.module('talon.distribution', [
  'ui.router',
  'datatables',
  'datatables.bootstrap',
  'talon.dataAccess',
  'talon.common',
  'dialogs.main',
  'ngFileUpload'
])

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
                expand: ['vouchers', 'categories'],
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

    var dialogs = this.$injector.get('dialogs');
    var $http = this.$injector.get('$http');
    var toaster = this.$injector.get('toaster');
    var entityManagerFactory = this.$injector.get('entityManagerFactory');
    var entityManager = this.entityManager;
    var self = this;

    function cancelVoucher(entity, grid) {
        var dlg = dialogs.confirm("Confirm", "Are you sure you would like to cancel this voucher?");
        dlg.result.then(function (r) {
            var query = entityManagerFactory.entityQuery('VoucherTransactionRecords')
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