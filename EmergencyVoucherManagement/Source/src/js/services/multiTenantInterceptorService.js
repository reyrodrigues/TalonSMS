'use strict';
app.factory('multiTenantInterceptorService', ['$q', '$injector', '$location', '$localStorage', function ($q, $injector, $location, $localStorage) {

    var multiTenantInterceptorServiceFactory = {};

    var _request = function (config) {

        config.headers = config.headers || {};
       
        var country = $localStorage.country;
        var organization = $localStorage.organization;
        if (country && organization) {
            config.headers['X-Tenant-Country'] = country.Id;
            config.headers['X-Tenant-Organization'] = organization.Id;
        }

        return config;
    }

    multiTenantInterceptorServiceFactory.request = _request;

    return multiTenantInterceptorServiceFactory;
}]);