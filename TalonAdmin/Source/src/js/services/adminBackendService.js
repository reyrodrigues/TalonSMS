'use strict';
app.factory('adminBackendService', ['breeze', 'serviceBase', function (breeze, serviceBase) {
    // define the Breeze `DataService` for this app
    var dataService = new breeze.DataService({
        serviceName: serviceBase + 'Breeze/Admin',
        hasServerMetadata: false  // don't ask the server for metadata
    });

    // create the metadataStore 
    var metadataStore = new breeze.MetadataStore({
    });

    // initialize it from the application's metadata variable
    metadataStore.importMetadata(window.AdminMetadata);

    metadataStore.setProperties({
        serializerFn: function (dataProperty, value) {
            if (dataProperty.dataType.name == 'DateTime') {
                return moment(value).tz('utc').toDate();
            }

            return value;
        }
    });

    var CountrySettingsCtor = function () {
        this.Properties = {};
    };

    // register your custom constructor
    metadataStore.registerEntityTypeCtor("CountrySettings", CountrySettingsCtor);

    // create a new EntityManager that uses this metadataStore
    var entityManager = new breeze.EntityManager({
        dataService: dataService,
        metadataStore: metadataStore
    });


    entityManager.saveOptions = new breeze.SaveOptions({ allowConcurrentSaves: true });

    return entityManager;
}]);
