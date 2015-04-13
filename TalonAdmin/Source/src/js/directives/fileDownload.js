'use strict';
angular.module('app')
    .directive('fileDownload', ['$timeout', function ($timeout) {
        return {
            restrict: 'A',
            require: '?ngModel',
            link: function (scope, element, attrs, ngModel) {
                var a = $(element);
                if (ngModel) {
                    viewWatch = function () {
                        return ngModel.$viewValue;
                    };
                    scope.$watch(viewWatch, function () {
                        if (ngModel.$viewValue) {
                            a.attr('href', 'data:' + (attrs['mimeType'] || 'text/plain') + ';base64,' + ngModel.$viewValue);
                        }
                    }, true);
                }
            }
        }
    }]);

