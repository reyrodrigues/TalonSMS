'use strict';
angular.module('app')
    .directive('chosen', ['JQ_CONFIG', 'uiLoad', '$timeout', function (JQ_CONFIG, uiLoad, $timeout) {
        return {
            restrict: 'A',
            require: '?ngModel',
            link: function (scope, element, attrs, ngModel) {
                uiLoad.load(JQ_CONFIG['chosen']).then(function () {
                    var select = $(element);

                    select.chosen({ allow_single_deselect: true });
                    select.addClass('isChosen');

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

                    select.trigger('chosen:updated');

                    if (ngModel) {
                        if (attrs.multiple) {
                            viewWatch = function () {
                                return ngModel.$viewValue;
                            };
                            scope.$watch(viewWatch, ngModel.$render, true);
                        }
                    }

                    $timeout(function () {
                        select.trigger('chosen:updated');
                    }, 500);
                }).catch(function () {

                });
            }
        }
    }]);

