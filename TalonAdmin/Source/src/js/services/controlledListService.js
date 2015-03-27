'use strict';
app.factory('controlledListService', ['breeze', 'backendService', 'adminBackendService', '$q', function (breeze, backendService, adminBackendService, $q) {
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
        getOrganizations: function () {
            var deferred = $q.defer();
            new breeze.EntityQuery("Organizations")
           .using(adminBackendService)
           .noTracking()
           .execute()
           .then(function (res) {
               deferred.resolve(res.results);
           });

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
        }

    };
}]);
