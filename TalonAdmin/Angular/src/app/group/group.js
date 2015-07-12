angular.module('talon.group', [
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
    .state('groups', {
        url: '/cycles',
        views: {
            "app": {
                template: "<div ui-view></div>"
            }
        },
        abstract: true,
        data: {
            settings: {
                collectionType: "BeneficiaryGroups",
                entityType: 'BeneficiaryGroup',
                expand: ['beneficiaries'],
                form: 'group/form.tpl.html'
            }
        }
    })

    .state('groups.list', {
        url: '/index',
        controller: 'GenericListCtrl as vm',
        templateUrl: 'index.tpl.html',
        data: {
            pageTitle: 'Cycles',
            settings: {
                columns: [
                    ['id', '#'],
                    ['name', 'Name'],
                    ['beneficiaries.length', 'Number of Beneficiaries']
                ]
            }
        }
    })

    .state('groups.edit', {
        url: '/{id:int}',
        controller: 'GroupEditController as vm',
        templateUrl: 'edit.tpl.html',
        data: {
            pageTitle: 'Cycles'
        }
    })

    .state('groups.create', {
        url: '/create',
        controller: 'GenericEditCtrl as vm',
        templateUrl: 'create.tpl.html',
        data: {
            pageTitle: 'Cycles'
        }
    });
})
.controller('GroupEditController', GroupEditController)

;

angular.extend(GroupEditController.prototype, EditController.prototype);

GroupEditController.prototype.configure = function configure() {
    var entityManagerFactory = this.$injector.get('entityManagerFactory');
    var entityManager = this.entityManager;

    this.$scope.removeBeneficiary = removeBeneficiary;

    function removeBeneficiary(entity, grid) {
        var query = entityManagerFactory.entityQuery('Beneficiaries')
            .where("id", "==", entity.id)
            .using(entityManager)
            .execute()
            .then(function (r) {
                var beneficiary = r.results[0];
                beneficiary.groupId = null;
                entityManager.saveChanges([beneficiary]).then(function () {
                    grid.api.custom.reloadData();
                });
            });
    }
};

function GroupEditController($injector, $scope) {
    EditController.call(this, $injector, $scope);
}