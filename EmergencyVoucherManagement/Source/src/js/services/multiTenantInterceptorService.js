'use strict';
app.factory('multiTenantInterceptorService', ['$q', '$injector', '$location', '$localStorage', function ($q, $injector, $location, $localStorage) {

    var multiTenantInterceptorServiceFactory = {};

    var _request = function (config) {

        config.headers = config.headers || {};
       
        var selectedCountry = $localStorage.selectedCountry;
        var organization = $localStorage.organization;
        if (selectedCountry && organization) {
            config.headers['X-Tenant-Country'] = selectedCountry.Id;
            config.headers['X-Tenant-Organization'] = organization.Id;
        }

        return config;
    }

    multiTenantInterceptorServiceFactory.request = _request;

    return multiTenantInterceptorServiceFactory;
}]);