angular.module('talon.dataAccess')
.factory('entityManagerFactory', ['breeze', 'modelOverrides', emFactory]);

function emFactory(breeze, modelOverrides) {
    // Convert properties between server-side PascalCase and client-side camelCase
    breeze.NamingConvention.camelCase.setAsDefault();

    // KNOWN ISSUE: this client is breeze-client 1.5, which builds queries with OData-style
    // parameters ($top/$orderby/$expand/$inlinecount). The server is now Breeze 7, which expects
    // Breeze's own JSON query format and ignores those, so list screens fetch every row unpaged,
    // never populate expands, and report "NaN total entries". Entities themselves materialise
    // correctly. The fix is to upgrade to breeze-client 2.x (npm), which ships the matching JSON
    // uriBuilder and an AngularJS ajax adapter. See MIGRATION.md.

    // Identify the endpoint for the remote data service
    var serviceRoot = window.location.protocol + '//' + window.location.host + '/';
    var serviceName = serviceRoot + 'Breeze/EVM'; // breeze Web API controller

    var dataService = new breeze.DataService({
        serviceName: serviceName,
        hasServerMetadata: false
    });

    var metadataStore = new breeze.MetadataStore({});
    metadataStore.importMetadata(window.ContextMetadata);
    metadataStore.registerEntityTypeCtor("VoucherTransactionRecord", modelOverrides.VoucherTransactionRecord);
    metadataStore.registerEntityTypeCtor("Beneficiary", modelOverrides.Beneficiary);
    metadataStore.registerEntityTypeCtor("Voucher", modelOverrides.Voucher);

    var entityManager = new breeze.EntityManager({
        dataService: dataService,
        metadataStore: metadataStore,
        queryOptions: new breeze.QueryOptions({ mergeStrategy: breeze.MergeStrategy.OverwriteChanges })
    });

    entityManager.setProperties({
        queryOptions: entityManager.queryOptions.using({
            fetchStrategy: breeze.FetchStrategy.FromServer
        })
    });

    var adminDataService = new breeze.DataService({
        serviceName: serviceRoot + 'Breeze/Admin',
        hasServerMetadata: false
    });

    var adminMetadataStore = new breeze.MetadataStore({});
    adminMetadataStore.importMetadata(window.AdminMetadata);
    adminMetadataStore.registerEntityTypeCtor("CountrySettings", modelOverrides.CountrySettings);

    var adminEntityManager = new breeze.EntityManager({
        dataService: adminDataService,
        metadataStore: adminMetadataStore,
        queryOptions: new breeze.QueryOptions({ mergeStrategy: breeze.MergeStrategy.OverwriteChanges })
    });

    adminEntityManager.setProperties({
        queryOptions: adminEntityManager.queryOptions.using({
            fetchStrategy: breeze.FetchStrategy.FromServer
        })
    });



    // the "factory" services exposes two members
    var factory = {
        entityManager: function () {
            return entityManager;
        },
        serviceName: serviceName,
        adminEntityManager: function () {
            return adminEntityManager;
        },
        entityQuery: function (entity) { return new breeze.EntityQuery(entity); }
    };

    return factory;
}