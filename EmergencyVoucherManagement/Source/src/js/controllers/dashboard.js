'use strict';


app.controller('DashboardCtrl', ['breeze', '$scope', '$http', 'serviceBase', function (breeze, $scope, $http, serviceBase) {
    $http.get(serviceBase + 'api/Dashboard/DashboardSummary')
        .then(function (res) {
            $scope.distributions = res.data;
        });
}]);