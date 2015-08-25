angular.module('talon.vendor', [
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
    .state('vendors', {
        url: '/vendor',
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
                            { 'name': { 'contains': value } },
                            { 'mobileNumber': { 'contains': value } },
                            { 'parentRecord.name': { 'contains': value } },
                            { 'parentRecord.mobileNumber': { 'contains': value } }
                        ]
                    };
                },
                collectionType: "Vendors",
                entityType: 'Vendor',
                controlledLists: ['locations', 'vendorTypes', 'sexes'],
                form: 'vendor/form.tpl.html'
            }
        }
    })

    .state('vendors.list', {
        url: '/index',
        controller: 'VendorListController as vm',
        templateUrl: 'index.tpl.html',
        data: {
            pageTitle: 'Vendors',
            settings: {
                columns: [
                    ['id', '#'],
                    ['name', 'Name']
                ]
            }
        }
    })

    .state('vendors.edit', {
        url: '/{id:int}',
        controller: 'VendorEditController as vm',
        templateUrl: 'edit.tpl.html',
        data: {
            pageTitle: 'Vendors'
        }
    })

    .state('vendors.create', {
        url: '/create',
        controller: 'VendorEditController as vm',
        templateUrl: 'create.tpl.html',
        data: {
            pageTitle: 'Vendors'
        }
    });
})
.controller('VendorListController', VendorListController)
.controller('ImportVendorsController', ImportVendorsController)
.controller('VendorEditController', VendorEditController)
;

VendorListController.prototype.configure = function configure() {
    var $localStorage = this.$injector.get('$localStorage');
    var dialogs = this.$injector.get('dialogs');
    var toaster = this.$injector.get('toaster');
    var self = this;

    this.actions = [
        {
            label: "Import Excel",
            css: "btn-default",
            action: function action() {
                var dlg = dialogs.create('dialogs/importVendors.tpl.html', 'ImportVendorsController');
                dlg.result.then(function (result) {
                    if (result) {
                        if (!result.Errors.length) {
                            toaster.pop('success', 'Success', 'Vendors successfuly imported.');
                        } else {
                            toaster.pop('warning', 'Notice', 'Some vendors were not imported correctly.');

                            result.Errors.forEach(function (e) {
                                toaster.pop('error', 'Error', 'Error importing vendor. Message from server:\n' + e.ErrorText + "\nLine: " + e.Line);
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
                return serviceRoot + 'api/Excel/ExportVendors?countryId=' + $localStorage.country.Id;
            }
        }
    ];
};

VendorEditController.prototype.postSave = function postSave() {
    var self = this;
    var $state = this.$injector.get('$state');
    var $http = this.$injector.get('$http');
    var $q = this.$injector.get('$q');
    var entityManagerFactory = this.$injector.get('entityManagerFactory');
    var entityManager = this.entityManager;
    var $scope = this.$scope;

    var url = serviceRoot + 'Api/App/VendorProfile/UpdatePassword';

    var def = $q.defer();
    if (self.password.NewPassword && (self.password.NewPassword == self.password.ConfirmPassword)) {
        $http.post(url, {
            VendorId: self.entity.id,
            Password: self.password.NewPassword
        }).then(function () {
            def.resolve();
        });
    } else {
        def.resolve();
    }

    var logItem = entityManager.createEntity('AuditLogItem', {
        objectType: 'Vendor',
        objectId: this.entity.id,
        modifiedOn: moment().utc().toDate(),
        modifiedBy: $scope.currentUser.UserName
    });

    return def.promise.then(entityManager.saveChanges([logItem]));

};

VendorEditController.prototype.configure = function configure() {
    var gettext = this.$injector.get('gettext');
    var $state = this.$injector.get('$state');
    var dialogs = this.$injector.get('dialogs');
    var toaster = this.$injector.get('toaster');
    var $rootScope = this.$injector.get('$rootScope');
    var entityManagerFactory = this.$injector.get('entityManagerFactory');
    var entityManager = this.entityManager;
    var self = this;
    this.password = {};


    this.$scope.editChild = editChild;
    this.$scope.deleteChild = deleteChild;
    this.actions = [
        {
            label: "Add Sales Person",
            css: "btn-info",
            condition: function () {
                return $rootScope.canI('Add Sales Person');
            },
            action: createChild
        }
    ];


    function deleteChild(child, grid) {
        var dlg = dialogs.confirm(gettext("Confirm"), gettext("Are you sure you would like to delete this record? This operation cannot be reversed."));
        dlg.result.then(function (result) {
            if (result) {
                entityManagerFactory.entityQuery('VendorSalesPersons')
                .where('id', '==', child.id)
                .using(entityManager)
                .execute()
                .then(function (r) {
                    var entity = r.results[0];
                    entity.entityAspect.setDeleted();

                    entityManager.saveChanges([entity]).then(function () {
                        toaster.pop('success', gettext('Success!'), gettext('Record successfully deleted.'));

                        grid.api.custom.reloadData();
                    }).catch(function (error) {
                    });
                });
            }
        });
    }
    function editChild(child, grid) {
        var dlg = dialogs.create('vendor/edit-child.tpl.html', EditSalesPersonController, child);
        dlg.result.then(function (result) {
            if (result) {
                toaster.pop('success', gettext('Success!'), gettext('Item updated successfully.'));
                grid.api.custom.reloadData();
            }
        });
    }


    function createChild() {
        var dlg = dialogs.create('vendor/edit-child.tpl.html', EditSalesPersonController);
        dlg.result.then(function (result) {
            if (result) {
                toaster.pop('success', gettext('Success!'), gettext('Sales person created successfully.'));
            }
        });
    }

    function EditSalesPersonController($scope, $modalInstance, data) {
        if (data) {
            entityManagerFactory.entityQuery('VendorSalesPersons')
                .where('id', '==', data.id)
                .using(entityManager)
                .execute()
                .then(function (r) {
                    $scope.entity = r.results[0];
                });
        } else {
            $scope.entity = entityManager.createEntity('VendorSalesPerson', {
                vendorId: self.entity.id,
                countryId: self.entity.countryId
            });
        }

        $scope.save = function () {
            if ($scope.popupForm.$invalid) {
                angular.forEach($scope.popupForm.$error.required, function (field) {
                    field.$setDirty();
                    field.$setTouched();
                });

                return;
            }

            entityManager.saveChanges([$scope.entity]).then(function () {
                $modalInstance.close(true);
            });
        };

        $scope.close = function () {
            $modalInstance.close(false);
        };
    }

};


function VendorEditController($injector, $scope) {
    EditController.call(this, $injector, $scope);
}

function VendorListController($injector, $scope) {
    ListController.call(this, $injector, $scope);
}

function ImportVendorsController($scope, $q, $modalInstance, Upload, $localStorage) {
    $scope.files = [];

    $scope.upload = function () {
        console.log($scope);
        $scope.uploading = Upload.upload({
            url: serviceRoot + 'api/Excel/ImportVendors?countryId=' + $localStorage.country.Id,
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