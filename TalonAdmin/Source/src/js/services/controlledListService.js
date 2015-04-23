'use strict';
app.factory('controlledLists', ['breeze', 'backendService', 'adminBackendService', '$q', '$sessionStorage', '$localStorage',
    function (breeze, backendService, adminBackendService, $q, $sessionStorage, $localStorage) {
        return {
            getLocations: function () {
                var deferred = $q.defer();
                new breeze.EntityQuery("Locations")
                   .using(backendService)
                   .noTracking()
                   .execute()
                   .then(function (res) {
                       deferred.resolve(res.results);
                   });

                return deferred.promise;
            },
            getVoucherTypes: function () {
                var deferred = $q.defer();
                new breeze.EntityQuery("VoucherTypes")
                   .using(backendService)
                   .noTracking()
                   .execute()
                   .then(function (res) {
                       deferred.resolve(res.results);
                   });

                return deferred.promise;
            },
            getBeneficiaryGroups: function () {
                var deferred = $q.defer();
                new breeze.EntityQuery("BeneficiaryGroups")
               .using(backendService)
               .noTracking()
               .execute()
               .then(function (res) {
                   deferred.resolve(res.results);
               });

                return deferred.promise;
            },
            getVendorTypes: function () {
                var deferred = $q.defer();
                new breeze.EntityQuery("VendorTypes")
               .using(backendService)
               .noTracking()
               .execute()
               .then(function (res) {
                   deferred.resolve(res.results);
               });

                return deferred.promise;
            },
            getVendors: function () {
                var deferred = $q.defer();
                new breeze.EntityQuery("Vendors")
               .using(backendService)
               .noTracking()
               .execute()
               .then(function (res) {
                   deferred.resolve(res.results);
               });

                return deferred.promise;
            },
            getDistributions: function () {
                var deferred = $q.defer();
                new breeze.EntityQuery("Distributions")
               .using(backendService)
               .noTracking()
               .execute()
               .then(function (res) {
                   deferred.resolve(res.results);
               });

                return deferred.promise;
            },
            getPrograms: function () {
                var deferred = $q.defer();
                new breeze.EntityQuery("Programs")
               .using(backendService)
               .noTracking()
               .execute()
               .then(function (res) {
                   deferred.resolve(res.results);
               });

                return deferred.promise;
            },
            getOrganizations: function () {
                var deferred = $q.defer();
                if ($sessionStorage.organizations) {
                    deferred.resolve($sessionStorage.organizations);
                } else {
                    new breeze.EntityQuery("Organizations")
                   .using(adminBackendService)
                   .noTracking()
                   .execute()
                   .then(function (res) {
                       $sessionStorage.organizations = res.results;
                       deferred.resolve(res.results);
                   });
                }

                return deferred.promise;
            },
            getCountries: function () {
                var deferred = $q.defer();

                new breeze.EntityQuery("Countries")
                .using(adminBackendService)
                .noTracking()
                .execute()
                   .then(function (res) {
                       deferred.resolve(res.results);
                   });

                return deferred.promise;
            },
            getRoles: function () {
                var deferred = $q.defer();
                new breeze.EntityQuery("Roles")
                   .using(adminBackendService)
                   .noTracking()
                   .execute()
                   .then(function (res) {
                       deferred.resolve(res.results);
                   });
                return deferred.promise;
            }

        };
    }]);

app.factory('organizations', ['controlledLists', function (controlledLists) {
    return controlledLists.getOrganizations();
}])
app.factory('countries', ['controlledLists', function (controlledLists) {
    return controlledLists.getCountries();
}])
app.factory('roles', ['controlledLists', function (controlledLists) {
    return controlledLists.getRoles();
}])
app.factory('locations', ['controlledLists', function (controlledLists) {
    return controlledLists.getLocations();
}])
app.factory('vendorTypes', ['controlledLists', function (controlledLists) {
    return controlledLists.getVendorTypes();
}])
app.factory('distributions', ['controlledLists', function (controlledLists) {
    return controlledLists.getDistributions();
}])
app.factory('vendors', ['controlledLists', function (controlledLists) {
    return controlledLists.getVendors();
}])
app.factory('programs', ['controlledLists', function (controlledLists) {
    return controlledLists.getPrograms();
}])
app.factory('voucherTypes', ['controlledLists', function (controlledLists) {
    return controlledLists.getVoucherTypes();
}])