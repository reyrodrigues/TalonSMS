angular.module('talon.beneficiary', [
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
    .state('beneficiaries', {
        url: '/beneficiary',
        views: {
            "app": {
                template: "<div ui-view></div>"
            }
        },
        abstract: true,
        data: {
            settings: {
                collectionType: "Beneficiaries",
                entityType: 'Beneficiary',
                expand: ['group', 'distributions', 'location'],
                controlledLists: ['locations', 'beneficiaryGroups', 'sexes'],
                form: 'beneficiary/form.tpl.html'
            }
        }
    })

    .state('beneficiaries.list', {
        url: '/index',
        controller: 'BeneficiaryListCtrl as vm',
        templateUrl: 'index.tpl.html',
        data: {
            pageTitle: 'Beneficiaries',
            settings: {
                columns: [
                    ['id', '#'],
                    ['firstName', 'First Name'],
                    ['lastName', 'Last Name'],
                    ['mobileNumber', 'Mobile Number'],
                    ['group.name', 'Cycle'],
                    ['location.name', 'Location']
                ]
            }
        }
    })

    .state('beneficiaries.edit', {
        url: '/{id:int}',
        controller: 'BeneficiaryEditController as vm',
        templateUrl: 'edit.tpl.html',
        data: {
            pageTitle: 'Beneficiaries'
        }
    })

    .state('beneficiaries.create', {
        url: '/create',
        controller: 'GenericEditCtrl as vm',
        templateUrl: 'create.tpl.html',
        data: {
            pageTitle: 'Beneficiaries'
        }
    });
})
.controller('BeneficiaryListCtrl', BeneficiaryListController)
.controller('BeneficiaryEditController', BeneficiaryEditController)
.controller('ImportBeneficiariesCtrl', ImportBeneficiariesController)
;

angular.extend(BeneficiaryListController.prototype, ListController.prototype);
angular.extend(BeneficiaryEditController.prototype, EditController.prototype);

BeneficiaryListController.prototype.configure = function configure() {
    var $localStorage = this.$injector.get('$localStorage');
    var dialogs = this.$injector.get('dialogs');
    var toaster = this.$injector.get('toaster');
    var self = this;

    this.actions = [
        {
            label: "Import Excel",
            css: "btn-default",
            action: function action() {
                var dlg = dialogs.create('dialogs/importBeneficiaries.tpl.html', 'ImportBeneficiariesCtrl');
                dlg.result.then(function (result) {
                    if (result) {
                        if (!result.Errors.length) {
                            toaster.pop('success', 'Success', 'Beneficiaries successfuly imported.');
                        } else {
                            toaster.pop('warning', 'Notice', 'Some beneficiaries were not imported correctly.');

                            result.Errors.forEach(function (e) {
                                toaster.pop('error', 'Error', 'Error importing beneficiary. Message from server:\n' + e.ErrorText + "\nLine: " + e.Line);
                            });
                        }


                        self.instance.rerender();
                    }
                }).catch(function (res) {
                    toaster.pop('error', 'Error', res.data);
                });

            }
        }];

    this.forms = [
        {
            label: "Export Excel",
            css: "btn-info",
            url: function () {
                return serviceRoot + 'api/Excel/ExportBeneficiaries?countryId=' + $localStorage.country.Id + '&organizationId=' + $localStorage.organization.Id;
            }
        }
    ];
};

BeneficiaryEditController.prototype.canEdit = function canEdit() {
    return !this.entity.disabled;
};  

BeneficiaryEditController.prototype.configure = function configure() {
    this.$scope.cancelVoucher = cancelVoucher.bind(this.$scope);
    this.$scope.resendVoucher = resendVoucher.bind(this.$scope);

    var dialogs = this.$injector.get('dialogs');
    var $http = this.$injector.get('$http');
    var toaster = this.$injector.get('toaster');
    var entityManagerFactory = this.$injector.get('entityManagerFactory');
    var entityManager = this.entityManager;
    var self = this;

    this.actions = [
        {
            label: "Disable Beneficiary",
            css: "btn-info",
            condition: function (entity) {
                return !entity.disabled;
            },
            action: function action() {
                self.entity.disabled = true;
                entityManager.saveChanges([self.entity]);

            }
        },
        {
            label: "Enable Beneficiary",
            css: "btn-info",
            condition: function (entity) {
                return entity.disabled;
            },
            action: function action() {
                self.entity.disabled = false;
                entityManager.saveChanges([self.entity]);
            }
        }
    ];

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
            var payload = { VoucherId: entity.voucherId, BeneficiaryId: entity.beneficiarId };

            $http.post(serviceBase + 'Api/VoucherWorkflow/ResendSMS', payload)
                .then(function () {
                    toaster.pop('success', 'Success!', 'Voucher resent successfully!');
                    loadData();
                }).catch(function (res) {
                    toaster.pop('error', 'Error', res.data.Message);
                });
        });
    }
};

function BeneficiaryListController($injector, $scope) {
    ListController.call(this, $injector, $scope);
}

function BeneficiaryEditController($injector, $scope) {
    EditController.call(this, $injector, $scope);
}

function ImportBeneficiariesController($scope, $q, $modalInstance, Upload, $localStorage) {
    $scope.files = [];

    $scope.upload = function () {
        console.log($scope);
        $scope.uploading = Upload.upload({
            url: serviceRoot + 'api/Excel/ImportBeneficiaries?countryId=' + $localStorage.country.Id + '&organizationId=' + $localStorage.organization.Id,
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
}