angular.module('talon.sys-admin')
.config(function config($stateProvider) {
    $stateProvider
    .state('sys-admin.menu-items', {
        url: '/menu-item',
        template: "<div ui-view></div>",
        abstract: true,
        data: {
            settings: {
                entityManager: 'adminEntityManager',
                collectionType: "MenuItems",
                entityType: 'MenuItem',
                expand: ['category'],
                controlledLists: ['menuCategories'],
                form: 'sys-admin/menu-item/form.tpl.html'
            }
        }
    })

    .state('sys-admin.menu-items.list', {
        url: '/index',
        controller: 'GenericListCtrl as vm',
        templateUrl: 'index.tpl.html',
        data: {
            pageTitle: 'Menu Items',
            settings: {
                filter: {
                    'parentId': { '==': null }
                },
                columns: [
                    ['id', '#'],
                    ['title', 'Title'],
                    ['category.name', 'Category'],
                    ['cssClass', 'CSS']
                ]
            }
        }
    })
    .state('sys-admin.menu-items.create', {
        url: '/create',
        controller: 'GenericEditCtrl as vm',
        templateUrl: 'create.tpl.html',
        data: {
            pageTitle: 'Menu Items',
            settings: {
            }
        }
    })
    .state('sys-admin.menu-items.edit', {
        url: '/{id:int}',
        controller: 'MenuItemEditController as vm',
        templateUrl: 'edit.tpl.html',
        data: {
            pageTitle: 'Menu Items',
            settings: {
            }
        }
    })
    ;

})
.controller('MenuItemEditController', MenuItemEditController)
;

MenuItemEditController.prototype.configure = function configure() {
    var toaster = this.$injector.get('toaster');
    var dialogs = this.$injector.get('dialogs');
    var entityManagerFactory = this.$injector.get('entityManagerFactory');
    var entityManager = entityManagerFactory.adminEntityManager();
    var self = this;

    this.$scope.editChild = editChild;
    this.$scope.deleteChild = deleteChild;
    this.actions = [
        {
            label: "Create Child",
            css: "btn-info",
            action: createChild
        }
    ];

    function deleteChild(child, grid) {
        var dlg = dialogs.confirm("Confirm", "Are you sure you would like to delete this record? This operation cannot be reversed.");
        dlg.result.then(function () {
            entityManagerFactory.entityQuery('MenuItems')
                .where('id', '==', child.id)
                .using(entityManager)
                .execute()
                .then(function (r) {
                    var entity = r.results[0];
                    entity.entityAspect.setDeleted();

                    entityManager.saveChanges([entity]).then(function () {
                        toaster.pop('success', 'Success!', 'Record successfully deleted.');

                        grid.api.custom.reloadData();
                    }).catch(function (error) {
                    });
                });
        });
    }
    function editChild(child) {
        var dlg = dialogs.create('sys-admin/menu-item/edit-child.tpl.html', EditChildMenuItemController, child);
        dlg.result.then(function (result) {
            toaster.pop('success', 'Success!', 'Menu item updated successfully.');
            grid.api.custom.reloadData();
        });
    }

    function createChild() {
        var dlg = dialogs.create('sys-admin/menu-item/edit-child.tpl.html', EditChildMenuItemController);
        dlg.result.then(function (result) {
            toaster.pop('success', 'Success!', 'Menu item created successfully.');

            if (self.reloadMenuItems) {
                self.reloadMenuItems();
            }
        });
    }

    function EditChildMenuItemController($scope, $modalInstance, data) {
        if (data) {
            entityManagerFactory.entityQuery('MenuItems')
                .where('id', '==', data.id)
                .using(entityManager)
                .execute()
                .then(function (r) {
                    $scope.entity = r.results[0];
                });
        } else {
            $scope.entity = entityManager.createEntity('MenuItem', {
                parentId: self.entity.id
            });
        }

        $scope.save = function () {
            entityManager.saveChanges([$scope.entity]).then(function () {
                $modalInstance.close(true);
            });
        };

        $scope.close = function () {
            $modalInstance.close(false);
        };
    }
};

function MenuItemEditController($injector, $scope) {
    EditController.call(this, $injector, $scope);
}