angular.module('talon.dataAccess')
.factory('entityManagerFactory', ['breeze', 'modelOverrides', emFactory]);

function emFactory(breeze, modelOverrides) {
    // Convert properties between server-side PascalCase and client-side camelCase
    breeze.NamingConvention.camelCase.setAsDefault();

    // Breeze 7 on the server reads Breeze's own JSON query format. This client defaults to the
    // 'odata' uriBuilder, which emits $top/$orderby/$expand/$inlinecount; the server ignores those
    // outright, so list screens would fetch every row unpaged, with no expands and no inlineCount.
    breeze.config.initializeAdapterInstance('uriBuilder', 'json', true);

    // where/orderBy clauses are resolved against the metadata, so breeze already translates those
    // to the server names. expand paths are not: the JSON uriBuilder forwards them verbatim, so
    // the camelCase paths this app is written with ('group', 'settings.propertyCollection') reach
    // EF Core, which only knows the PascalCase navigation names and answers 500.
    //
    // The rewrite has to happen while the URI is built, not on the query: EntityQuery.expand()
    // validates each path against the metadata under its *client* name, so translating earlier
    // fails with "unable to locate property: Group on entityType: Beneficiary".
    patchJsonUriBuilderExpand(breeze);

    // Identify the endpoint for the remote data service
    var serviceRoot = window.location.protocol + '//' + window.location.host + '/';
    var serviceName = serviceRoot + 'Breeze/EVM'; // breeze Web API controller

    var dataService = new breeze.DataService({
        serviceName: serviceName,
        hasServerMetadata: false,
        uriBuilderName: 'json'
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
        hasServerMetadata: false,
        uriBuilderName: 'json'
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

    function patchJsonUriBuilderExpand(breeze) {
        var builder = breeze.config.getAdapterInstance('uriBuilder', 'json');
        if (!builder || builder.talonExpandPatched) {
            return;
        }

        var baseBuildUri = builder.buildUri;

        builder.buildUri = function (query, metadataStore) {
            var uri = baseBuildUri.call(this, query, metadataStore);
            var separator = uri.indexOf('?');
            if (separator < 0) {
                return uri;
            }

            var prefix = uri.substring(0, separator + 1);
            var payload = uri.substring(separator + 1);

            var parsed;
            try {
                parsed = JSON.parse(decodeURIComponent(payload));
            } catch (e) {
                return uri;
            }

            if (!parsed || !parsed.expand) {
                return uri;
            }

            parsed.expand = toServerExpandPaths(parsed.expand);

            return prefix + encodeURIComponent(JSON.stringify(parsed));
        };

        builder.talonExpandPatched = true;

        function toServerExpandPaths(propertyPaths) {
            var isArray = Object.prototype.toString.call(propertyPaths) === '[object Array]';
            var paths = isArray ? propertyPaths : String(propertyPaths).split(',');

            var mapped = paths.map(function (path) {
                return String(path).trim().split('.').map(function (segment) {
                    return segment ? segment.charAt(0).toUpperCase() + segment.substring(1) : segment;
                }).join('.');
            });

            return isArray ? mapped : mapped.join(',');
        }
    }

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
