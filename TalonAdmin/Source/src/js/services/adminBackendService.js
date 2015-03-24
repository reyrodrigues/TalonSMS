'use strict';
app.factory('adminBackendService', ['breeze', 'serviceBase', function (breeze, serviceBase) {
    var entityManager = new breeze.EntityManager(serviceBase + 'Breeze/Admin');
    entityManager.metadataStore.setProperties({
        serializerFn: function (dataProperty, value) {
            if (dataProperty.dataType.name == 'DateTime') {
                return moment(value).tz('utc').toDate();
            }

            return value;
        }
    });
    entityManager.saveOptions = new breeze.SaveOptions({ allowConcurrentSaves: true });

    return entityManager;
}]);
