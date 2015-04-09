'use strict';
angular.module('app')
.directive('phoneNumber', ['$rootScope', function ($rootScope) {
    var slice = Array.prototype.slice;
    return {
        restrict: 'A',
        require: '?ngModel',
        link: function(scope, element, attrs, ngModel) {
            if (!ngModel) return;

            var countryCode = 1;
            ngModel.$parsers.push(function (modelValue) {
                return '+' + countryCode + modelValue;
            });

            ngModel.$formatters.push(function (viewValue) {
                var reg = new RegExp("^\\+" + countryCode + "|" + countryCode);

                return (viewValue || "").replace(reg, "");
            });

            $rootScope.$watch(function () { return $rootScope.country; }, function () {
                if ($rootScope.country) {
                    countryCode = $rootScope.country.CountryCallingCode;
                };
            });
        } //link
    }; //return
}]);