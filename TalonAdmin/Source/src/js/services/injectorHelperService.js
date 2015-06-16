'use strict';
app.factory('injectorHelper', ['$injector', '$q', function ($injector, $q) {
    return {
        injectPromises: function ($scope, resources) {
            var deferred = $q.defer();
            angular.forEach(resources, function (v, k) {
                $scope[v] = [];
            });

            resources.push(function(){
                var promises = arguments;
                $q.all(promises).then(function (results) {
                    var resourceArguments = results;
                    angular.forEach(resources, function (v, k) {
                        $scope[v] = resourceArguments[k];
                    });

                    deferred.resolve();
                })
                .catch(function () { deferred.reject(); });;
            });

            $injector.invoke(resources);

            return deferred.promise;
        }
    };
}]);
