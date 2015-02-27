'use strict';
app.factory('controlledListService', ['breeze', 'backendService', '$q', function (breeze, backendService, $q) {
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
        }

    };
}]);
