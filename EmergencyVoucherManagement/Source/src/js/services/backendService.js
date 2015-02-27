'use strict';
app.factory('backendService', ['breeze', 'serviceBase', function (breeze, serviceBase) {
    var entityManager = new breeze.EntityManager(serviceBase + 'Breeze/EVM');

    return entityManager;
}]);
