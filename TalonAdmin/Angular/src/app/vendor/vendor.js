angular.module('talon.vendor', [
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

VendorEditController.prototype.configure = function configure() {
    var $state = this.$injector.get('$state');
    var entityManagerFactory = this.$injector.get('entityManagerFactory');
    var entityManager = this.entityManager;
    var self = this;

    var otherVendors = entityManagerFactory.entityQuery('Vendors');

    if($state.params.id) {
        otherVendors = otherVendors.where('id', '!=', $state.params.id);
    }

    otherVendors.using(entityManager)
        .execute()
        .then(function (r) {
            self.lists.otherVendors = r.results;
        });


    var childVendors = entityManagerFactory.entityQuery('Vendors');

    if ($state.params.id) {
        childVendors = childVendors.where('parentRecordId', '==', $state.params.id);
    }

    childVendors.using(entityManager)
        .execute()
        .then(function (r) {
            self.childVendors = r.results;
        });
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