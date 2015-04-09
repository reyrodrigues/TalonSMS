'use strict';

app    .directive('input', ['dateFilter', function (dateFilter) {
        return {
            restrict: 'E',
            require: '?ngModel',
            link: function (scope, element, attrs, ngModel) {
                if (
                       'undefined' !== typeof attrs.type
                    && 'date' === attrs.type
                    && ngModel
                ) {
                    ngModel.$formatters.push(function (modelValue) {
                        return moment(moment(modelValue).tz('utc').format("YYYY-MM-DD")).toDate();
                    });

                    ngModel.$parsers.push(function (viewValue) {
                        return viewValue;
                    });
                }
            }
        }
    }])
;
