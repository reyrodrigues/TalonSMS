'use strict';
angular.module('app')
    .directive('dateField', ['JQ_CONFIG', function (JQ_CONFIG, uiLoad, $timeout) {
        return {
            restrict: 'A',
            require: '?ngModel',
            link: function (scope, element, attrs, ngModel) {
                ngModel.$formatters.push(function (inputValue) {
                    // Removing the Time piece of the equation because it confuses the system
                    var datePart = moment(inputValue).tz('utc').toISOString().split('T')[0];

                    return moment(datePart).tz('utc').toDate();
                });

                ngModel.$parsers.push(function (inputValue) {
                    return moment(inputValue).tz('utc').toISOString();
                });
            }
        }
    }]);

