'use strict';
app.factory('backendService', ['breeze', 'serviceBase', '$localStorage', function (breeze, serviceBase, $localStorage) {
    // define the Breeze `DataService` for this app
    var dataService = new breeze.DataService({
        serviceName: serviceBase + 'Breeze/EVM',
        hasServerMetadata: false  // don't ask the server for metadata
    });

    // create the metadataStore 
    var metadataStore = new breeze.MetadataStore({
    });

    // initialize it from the application's metadata variable
    metadataStore.importMetadata(window.MainMetadata);

    metadataStore.setProperties({
        serializerFn: function (dataProperty, value) {
            if (dataProperty.dataType.name == 'DateTime') {
                return moment(value).tz('utc').toDate();
            }
            if (dataProperty.name == 'CountryId') {
                // Globaly setting Country based on users local storage
                return $localStorage.country.Id;
            }
            if (dataProperty.name == 'OrganizationId') {
                // Globaly setting Organization based on users local storage
                return $localStorage.organization.Id;
            }

            return value;
        }
    });

    var Vendor = function () {
        this.Name = "";
    };
    var Beneficiary = function () {
        this.Name = "";
    };

    // register your custom constructor
    metadataStore.registerEntityTypeCtor("Vendor", Vendor);
    metadataStore.registerEntityTypeCtor("Beneficiary", Beneficiary);

    // create a new EntityManager that uses this metadataStore
    var entityManager = new breeze.EntityManager({
        dataService: dataService,
        metadataStore: metadataStore
    });

    entityManager.saveOptions = new breeze.SaveOptions({ allowConcurrentSaves: true });

    return entityManager;
}]);
