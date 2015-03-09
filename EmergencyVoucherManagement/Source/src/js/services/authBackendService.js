'use strict';
app.factory('authBackendService', ['breeze', 'serviceBase', function (breeze, serviceBase) {
    var entityManager = new breeze.EntityManager(serviceBase + 'Breeze/Auth');

    return entityManager;
}]);
