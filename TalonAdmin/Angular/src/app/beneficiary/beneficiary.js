angular.module('talon.beneficiary', [
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
                filterFunction: function (value) {
                    return {
                        'or': [
                            { 'firstName': { 'contains': value } },
                            { 'lastName': { 'contains': value } },
                            { 'mobileNumber': { 'contains': value } },
                            { 'group.name': { 'contains': value } },
                            { 'location.name': { 'contains': value } }
                        ]
                    };
                },
                collectionType: "Beneficiaries",
                entityType: 'Beneficiary',
                expand: ['group', 'location', 'additionalData'],
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
                    ['location.name', 'Location'],
                    ['disabled', 'Is Disabled']
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

BeneficiaryListController.prototype.configure = function configure() {
    var gettext = this.$injector.get('gettext');
    var $rootScope = this.$injector.get('$rootScope');
    var $localStorage = this.$injector.get('$localStorage');
    var dialogs = this.$injector.get('dialogs');
    var toaster = this.$injector.get('toaster');
    var self = this;

    this.actions = [
        {
            label: gettext("Import Excel"),
            css: "btn-default",
            condition: function () { return $rootScope.canI('Import Beneficiaries'); },
            action: function action() {
                var dlg = dialogs.create('dialogs/importBeneficiaries.tpl.html', 'ImportBeneficiariesCtrl');
                dlg.result.then(function (result) {
                    if (result) {
                        if (!result.Errors.length) {
                            toaster.pop('success', gettext('Success'), gettext('Beneficiaries successfuly imported.'));
                        } else {
                            toaster.pop('warning', gettext('Notice'), gettext('Some beneficiaries were not imported correctly.'));

                            result.Errors.forEach(function (e) {
                                toaster.pop('error', gettext('Error'), gettext('Error importing beneficiary. Message from server:\n') + e.ErrorText + gettext("\nLine: ") + e.Line);
                            });
                        }

                        self.instance.rerender();
                    }
                }).catch(function (res) {
                });
            }
        }];

    this.forms = [
        {
            label: gettext("Export Excel"),
            condition: function () { return $rootScope.canI('Export Beneficiaries'); },
            css: "btn-info",
            url: function () {
                return serviceRoot + 'api/Excel/ExportBeneficiaries?countryId=' + $localStorage.country.Id + '&organizationId=' + $localStorage.organization.Id;
            }
        }
    ];
};

BeneficiaryEditController.prototype.getEntities = function getEntities() {
    var self = this;

    return [self.entity].concat(self.entity.additionalData);
};


/*// Custom Save Marker
BeneficiaryEditController.prototype.save = auditSave;
*/

BeneficiaryEditController.prototype.postSave = function postSave() {
    var entityManagerFactory = this.$injector.get('entityManagerFactory');
    var entityManager = this.entityManager;
    var $scope = this.$scope;

    var logItem = entityManager.createEntity('AuditLogItem', {
        objectType: 'Beneficiary',
        objectId: this.entity.id,
        modifiedOn: moment().utc().toDate(),
        modifiedBy: $scope.currentUser.UserName
    });

    return entityManager.saveChanges([logItem]);
};

BeneficiaryEditController.prototype.canEdit = function canEdit() {
    return !this.entity.disabled;
};

BeneficiaryEditController.prototype.configure = function configure() {
    this.$scope.cancelVoucher = cancelVoucher.bind(this.$scope);
    this.$scope.resendVoucher = resendVoucher.bind(this.$scope);

    var dialogs = this.$injector.get('dialogs');
    var gettext = this.$injector.get('gettext');
    var $rootScope = this.$injector.get('$rootScope');
    var $http = this.$injector.get('$http');
    var toaster = this.$injector.get('toaster');
    var entityManagerFactory = this.$injector.get('entityManagerFactory');
    var entityManager = this.entityManager;
    var self = this;

    this.actions = [
        {
            label: gettext("Disable Beneficiary"),
            css: "btn-info",
            condition: function (entity) {
                return !entity.disabled && self.$scope.canI('Disable Beneficiaries');
            },
            action: function action() {
                self.entity.disabled = true;
                entityManager.saveChanges([self.entity]);
            }
        },
        {
            label: gettext("Enable Beneficiary"),
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
        var dlg = dialogs.confirm(gettext("Confirm"), gettext("Are you sure you would like to cancel this voucher?"));
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
                        toaster.pop('success', gettext('Success!'), gettext('Voucher cancelled successfully!'));
                        grid.api.custom.reloadData();
                    });
                });
            });
        });
    }

    function resendVoucher(entity, grid) {
        console.log(grid, entity);

        var dlg = dialogs.confirm(gettext("Confirm"), gettext("Are you sure you would like to resend this voucher?"));
        dlg.result.then(function (r) {
            var payload = { VoucherId: entity.voucherId, BeneficiaryId: entity.beneficiaryId };

            $http.post(serviceRoot + 'Api/VoucherWorkflow/ResendSMS', payload)
                .then(function () {
                    toaster.pop('success', gettext('Success!'), gettext('Voucher resent successfully!'));
                    grid.api.custom.reloadData();
                }).catch(function (res) {
                    toaster.pop('error', 'Error', res.data.Message);
                });
        });
    }
};

function auditSave(continueEditing) {
    var self = this;
    self.isEditing = false;

    var $scope = this.$scope;
    if ($scope.dataForm.$invalid) {
        angular.forEach($scope.dataForm.$error.required, function (field) {
            field.$setDirty();
            field.$setTouched();
        });

        return;
    }

    $scope.dataForm.$setPristine();
    $scope.dataForm.$setUntouched();

    var breeze = self.$injector.get('breeze');

    var auditSaveOption = new breeze.SaveOptions({ resourceName: 'AuditSaveChanges' });

    self.entityManager.saveChanges(self.getEntities(), auditSaveOption).then(function (ne) {
        self.success('Record successfully saved.');
        if (self.isNew) {
            $state.go('^.edit', { id: self.entity.id });
        }

        self.isEditing = continueEditing;
    }).catch(function (error) {
        self.failure(error);
    });
}

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