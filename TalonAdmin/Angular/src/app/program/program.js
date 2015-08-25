angular.module('talon.program', [
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
    .state('programs', {
        url: '/program',
        views: {
            "app": {
                template: "<div ui-view></div>"
            }
        },
        abstract: true,
        data: {
            settings: {
                collectionType: "Programs",
                entityType: 'Program',
                controlledLists: ['programs', 'locations', 'voucherTypes', 'vendorTypes', 'beneficiaryGroups', 'distributionMechanisms', 'validAfterOffsetTypes'],
                expand: ['distributions', 'categories'],
                form: 'program/form.tpl.html'
            }
        }
    })

    .state('programs.list', {
        url: '/index',
        controller: 'GenericListCtrl as vm',
        templateUrl: 'index.tpl.html',
        data: {
            pageTitle: 'Programs',
            settings: {
                columns: [
                    ['id', '#'],
                    ['name', 'Name']
                ]
            }
        }
    })

    .state('programs.edit', {
        url: '/{id:int}',
        controller: 'ProgramEditController as vm',
        templateUrl: 'edit.tpl.html',
        data: {
            pageTitle: 'Programs'
        }
    })

    .state('programs.create', {
        url: '/create',
        controller: 'ProgramEditController as vm',
        templateUrl: 'create.tpl.html',
        data: {
            pageTitle: 'Programs'
        }
    });
})
.controller('ProgramEditController', ProgramEditController)
;

ProgramEditController.prototype.configure = function configure() {
    var $scope = this.$scope;
    this.$scope.addCategory = AddCategory;
    this.$scope.removeCategory = RemoveCategory;
    this.$scope.copyCategory = CopyCategory;

    var dialogs = this.$injector.get('dialogs');
    var $http = this.$injector.get('$http');
    var $rootScope = this.$injector.get('$rootScope');
    var toaster = this.$injector.get('toaster');
    var entityManagerFactory = this.$injector.get('entityManagerFactory');
    var entityManager = this.entityManager;
    var self = this;

    this.actions = [
        {
            label: "Distribute Vouchers",
            css: "btn-info",
            condition: function (entity) {
                return $rootScope.canI('Distribute Vouchers');
            },
            action: function action() {
                if (self.entity.categories.length === 0) {
                    alert('To distribute vouchers, please add at least one category. To add a category, edit this program and click on the "add" button.');
                    return;
                }

                var dlg = dialogs.create('program/distribute-vouchers.tpl.html', DistributeVouchersController);
                dlg.result.then(function (result) {
                    console.log('Modal Closed, result:', result, 'and self is', self);
                    if (result) {
                        self.reloadDistributions();
                    }
                });
            }
        }
    ];

    function CopyCategory(category) {
        var dlg = dialogs.create('program/copy-categories.tpl.html', function ($scope, $modalInstance) {
            $scope.copies = {
                numberOfCopies: 1,
                dateOffset: 0
            };
            $scope.dateOffsetOptions = [
                { id: 1, name: 'No Offset' },
                { id: 2, name: 'Every Day' },
                { id: 3, name: 'Every Week' }
            ];

            $scope.save = function () {
                $modalInstance.close($scope.copies);
            };

            $scope.close = function () {
                $modalInstance.close(false);
            };
        });

        dlg.result.then(function (result) {
            if (result) {
                for (var i = 1; i <= result.numberOfCopies; i++) {
                    self.entity.categories.push(entityManager.createEntity('ProgramVoucherCategory', {
                        programId: self.entity.id,
                        organizationId: self.entity.organizationId,
                        countryId: self.entity.countryId,
                        typeId: category.typeId,
                        value: category.value,
                        vendorTypeId: category.vendorTypeId,
                        validAfterOffsetType: result.dateOffset,
                        validAfterOffset: i
                    }));
                }
            }
        });
    }

    function AddCategory() {
        self.entity.categories.push(entityManager.createEntity('ProgramVoucherCategory', {
            programId: self.entity.id,
            organizationId: self.entity.organizationId,
            countryId: self.entity.countryId,
            validAfterOffsetType: 1
        }));
    }

    function RemoveCategory(category) {
        category.entityAspect.setDeleted();
        self.deleted.push(category);
    }

    function DistributeVouchersController($scope, $modalInstance, data) {
        $scope.groups = self.lists.beneficiaryGroups;
        $scope.locations = self.lists.locations;
        $scope.entity = {
            programId: self.entity.id
        };


        $scope.save = function () {
            if ($scope.popupForm.$invalid) {
                angular.forEach($scope.popupForm.$error.required, function (field) {
                    field.$setDirty();
                    field.$setTouched();
                });

                return;
            }

            $http.post(serviceRoot + 'Api/VoucherWorkflow/DistributeVouchers', $scope.entity)
                .then(function () {
                    $modalInstance.close(true);

                    toaster.pop('success', 'Success!', 'Distribution created successfully!');
                }).catch(function (res) {
                    $modalInstance.close(true);

                    toaster.pop('error', 'Error', res.data.Message);
                });
        };

        $scope.close = function () {
            $modalInstance.close(false);
        };
    }
};

function ProgramEditController($injector, $scope) {
    this.save = save;
    this.deleted = [];
    this.defaults = function () {
        return {
            organizationId: $scope.organization.Id,
            countryId: $scope.country.Id,
            createdOn: moment().utc().toJSON(),
            createdBy: $scope.currentUser.Id,
            modifiedOn: moment().utc().toJSON(),
            modifiedBy: $scope.currentUser.Id,
            voucherCodeLength: 6,
            showInDashboard: true
        };
    };

    var $state = $injector.get('$state');

    EditController.call(this, $injector, $scope);


    // Custom Save Marker
    function save(continueEditing) {
        var self = this;
        var $scope = this.$scope;
        if ($scope.dataForm.$invalid) {
            angular.forEach($scope.dataForm.$error.required, function (field) {
                field.$setDirty();
                field.$setTouched();
            });

            return;
        }

        self.isEditing = false;

        $scope.dataForm.$setPristine();
        $scope.dataForm.$setUntouched();

        self.entity.modifiedOn = moment().utc().toJSON();
        self.entity.modifiedBy = $scope.currentUser.Id;

        self.entityManager.saveChanges([self.entity].concat(self.entity.categories).concat(self.deleted)).then(function (ne) {
            self.success('Record successfully saved.');
            if (self.isNew) {
                $state.go('^.edit', { id: self.entity.id });
            }

            self.isEditing = continueEditing;
        }).catch(function (error) {
            self.failure(error);
        });
    }
}