angular.module('talon.program', [
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
                controlledLists: ['programs', 'locations', 'voucherTypes', 'vendorTypes', 'beneficiaryGroups', 'distributionMechanisms'],
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
        controller: 'GenericEditCtrl as vm',
        templateUrl: 'create.tpl.html',
        data: {
            pageTitle: 'Programs'
        }
    });
})
.controller('ProgramEditController', ProgramEditController)
;

ProgramEditController.prototype.configure = function configure() {
    this.$scope.addCategory = AddCategory;
    this.$scope.removeCategory = RemoveCategory;

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
                var dlg = dialogs.create('program/distribute-vouchers.tpl.html', DistributeVouchersController);
                dlg.result.then(function (result) {
                });
            }
        }
    ];

    function AddCategory() {
        self.entity.categories.push(entityManager.createEntity('ProgramVoucherCategory', {
            programId: self.entity.id
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
            console.log($scope.distributeForm);
            if ($scope.distributeForm.$valid) {
                $http.post(serviceRoot + 'Api/VoucherWorkflow/DistributeVouchers', $scope.entity)
                    .then(function () {
                        toaster.pop('success', 'Success!', 'Distribution created successfully!');
                    }).catch(function (res) {
                        toaster.pop('error', 'Error', res.data.Message);
                    });
                $modalInstance.close(true);
            }
        };

        $scope.close = function () {
            $modalInstance.close(false);
        };
    }
};

function ProgramEditController($injector, $scope) {
    this.save = save;
    this.deleted = [];

    EditController.call(this, $injector, $scope);


    function save(continueEditing) {
        var self = this;
        self.isEditing = false;

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