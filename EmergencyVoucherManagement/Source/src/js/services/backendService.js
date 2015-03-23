'use strict';
app.factory('backendService', ['breeze', 'serviceBase', '$localStorage', function (breeze, serviceBase, $localStorage) {
    var entityManager = new breeze.EntityManager(serviceBase + 'Breeze/EVM');
    entityManager.metadataStore.setProperties({
        serializerFn: function (dataProperty, value) {
            if (dataProperty.dataType.name == 'DateTime') {
                return moment(value).tz('utc').toDate();
            }
            if (dataProperty.name == 'CountryId') {
                // Globaly setting Country based on users local storage
                return  $localStorage.selectedCountry.Id;
            }
            if (dataProperty.name == 'OrganizationId') {
                // Globaly setting Organization based on users local storage
                return $localStorage.organization.Id;
            }

            return value;
        }
    });
    entityManager.saveOptions = new breeze.SaveOptions({ allowConcurrentSaves: true });

    return entityManager;
}]);
