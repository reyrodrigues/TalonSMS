'use strict';

app.controller('GenericCreateCtrl', ['$scope', 'settings', 'ControllerFactory',
    function ($scope, settings, ControllerFactory) {
        

        ControllerFactory.Create($scope, settings);
    }]);

app.controller('GenericEditCtrl', ['$scope', 'settings', 'ControllerFactory',
    function ($scope, settings, ControllerFactory) {
        

        ControllerFactory.Edit($scope, settings);

        $scope.loadData();
    }]);

app.controller('GenericGridCtrl', ['$scope', 'settings', 'ControllerFactory',
    function ($scope, settings, ControllerFactory) {
        
       
        ControllerFactory.List($scope, settings);

        $scope.loadGridData();
    }]);
