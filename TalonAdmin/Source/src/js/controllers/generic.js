'use strict';

app.controller('GenericCreateCtrl', ['$scope', 'settings', 'createController',
    function ($scope, settings, createController) {
        $scope.genericSettings = settings;

        createController($scope, settings);
    }]);

app.controller('GenericEditCtrl', ['$scope', 'settings', 'editController',
    function ($scope, settings, editController) {
        $scope.genericSettings = settings;

        editController($scope, settings);

        $scope.loadData();
    }]);

app.controller('GenericGridCtrl', ['$scope', 'settings', 'listController',
    function ($scope, settings, listController) {
        $scope.genericSettings = settings;
       
        listController($scope, settings);

        $scope.loadGridData();
    }]);
