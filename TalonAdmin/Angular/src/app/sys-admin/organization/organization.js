angular.module('talon.sys-admin')
.config(function config($stateProvider) {
    $stateProvider
    .state('sys-admin.organizations', {
        url: '/organizations',
        template: "<div ui-view></div>",
        abstract: true,
        data: {
            settings: {
                entityManager: 'adminEntityManager',
                collectionType: "Organizations",
                entityType: 'Organization',
                form: 'sys-admin/organization/form.tpl.html',
                controlledLists: ['countries'],
                defaults: {
                }
            }
        }
    })

    .state('sys-admin.organizations.list', {
        url: '/index',
        controller: 'GenericListCtrl as vm',
        templateUrl: 'index.tpl.html',
        data: {
            pageTitle: 'Organizations',
            settings: {
                columns: [
                    ['id', '#'],
                    ['name', 'Name'],
                    ['abbreviation', 'Abbrev']
                ]
            }
        }
    })

    .state('sys-admin.organizations.create', {
        url: '/create',
        controller: 'OrganizationEditController as vm',
        templateUrl: 'create.tpl.html',
        data: {
            pageTitle: 'Organizations',
            settings: {
            }
        }
    })
    .state('sys-admin.organizations.edit', {
        url: '/{id}',
        controller: 'OrganizationEditController as vm',
        templateUrl: 'edit.tpl.html',
        data: {
            pageTitle: 'Organizations',
            settings: {
            }
        }
    })
    ;

})
.controller('OrganizationEditController', OrganizationEditController)
;

OrganizationEditController.prototype.configure = function configure() {
    var $http = this.$injector.get('$http');
    var $q = this.$injector.get('$q');
    var entityManagerFactory = this.$injector.get('entityManagerFactory');
    var entityManager = entityManagerFactory.adminEntityManager();
    var self = this;

    this.mergeCountries = MergeCountries;

    this.$scope.removeCountry = RemoveCountry;
    this.$scope.addCountry = AddCountry;

    function RemoveCountry(country, grid) {
        entityManagerFactory.entityQuery('UnfilteredOrganizationCountries')
            .where({
                and: [
                    { countryId: { '==': country.id } },
                    { organizationId: { '==': self.entity.id } },
                ]
            })
            .using(entityManager)
            .execute()
            .then(function (r) {
                var entity = r.results[0];
                entity.entityAspect.setDeleted();

                entityManager.saveChanges([entity]).then(function () {
                    grid.api.custom.reloadData();
                }).catch(function (error) {
                    console.log(error);
                });
            });
    }

    function AddCountry(country, grid) {
        var entity = entityManager.createEntity('OrganizationCountry', {
            countryId: country.id,
            organizationId: self.entity.id,
            settings: entityManager.createEntity('CountrySettings')
        });

        entityManager.saveChanges([entity.settings, entity]).then(function () {
            grid.api.custom.reloadData();
        }).catch(function (error) {
            console.log(error);

        });
    }

    function MergeCountries(q) {
        var def = $q.defer();

        entityManagerFactory.entityQuery('UnfilteredOrganizationCountries')
            .where({
                and: [
                    { organizationId: { '==': self.entity.id } },
                ]
            })
            .using(entityManager)
            .execute()
            .then(function (response) {
                var ids = response.results.map(function (r) { return r.countryId; });
                var orgCountryIds = response.results.map(function (r) { return [r.id, r.organizationId]; });
                var results = q.map(function (r) {
                    r.isLinked = ids.indexOf(r.id) > -1;
                    return r;
                });

                def.resolve(results);
            });

        return def.promise;
    }
};

function OrganizationEditController($injector, $scope) {
    this.password = {};

    EditController.call(this, $injector, $scope);
}