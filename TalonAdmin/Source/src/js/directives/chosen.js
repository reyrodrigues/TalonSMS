'use strict';
var NG_OPTIONS_REGEXP = /^\s*(.*?)(?:\s+as\s+(.*?))?(?:\s+group\s+by\s+(.*))?\s+for\s+(?:([\$\w][\$\w]*)|(?:\(\s*([\$\w][\$\w]*)\s*,\s*([\$\w][\$\w]*)\s*\)))\s+in\s+(.*?)(?:\s+track\s+by\s+(.*?))?$/;

angular.module('app')
    .directive('chosen', ['$timeout', function ($timeout) {
        return {
            restrict: 'A',
            require: '?ngModel',
            link: function (scope, element, attrs, ngModel) {
                var select = $(element);

                if (!select.is("[multiple]")) {
                    select.append($("<option />"));
                }

                select.chosen({ allow_single_deselect: true });
                select.addClass('isChosen');

                if (attrs.ngOptions && ngModel) {
                    match = attrs.ngOptions.match(NG_OPTIONS_REGEXP);
                    valuesExpr = match[7];
                    scope.$watchCollection(valuesExpr, function (newVal, oldVal) {
                        var timer;
                        return timer = $timeout(function () {
                            select.trigger('chosen:updated');
                        });
                    });
                    scope.$on('$destroy', function (event) {
                        if (typeof timer !== "undefined" && timer !== null) {
                            return $timeout.cancel(timer);
                        }
                    });
                }

                select.on('chosen:updated', function () {
                    if (select.prop('readonly')) {
                        var wasDisabled = select.is(':disabled');

                        select.attr('disabled', 'disabled');
                        select.data('chosen').search_field_disabled();

                        if (wasDisabled) {
                            select.attr('disabled', 'disabled');
                        } else {
                            select.removeAttr('disabled');
                        }
                    }
                });


                if (ngModel) {
                    if (attrs.multiple) {
                        viewWatch = function () {
                            $timeout(function () {
                                select.trigger('chosen:updated');
                            }, 500);

                            return ngModel.$viewValue;
                        };
                        scope.$watch(viewWatch, ngModel.$render, true);
                    }
                }

                $timeout(function () {
                    select.trigger('chosen:updated');
                }, 500);
            }
        }
    }]);

