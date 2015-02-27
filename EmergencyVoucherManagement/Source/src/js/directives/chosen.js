'use strict';
angular.module('app')
    .directive('chosen', ['JQ_CONFIG', 'uiLoad', '$timeout', function (JQ_CONFIG, uiLoad, $timeout) {
        return {
            restrict: 'A',
            link: function (scope, element, attrs) {
                uiLoad.load(JQ_CONFIG['chosen']).then(function () {
                    var select = $(element);

                    select.chosen();
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


                    $timeout(function () {
                        select.trigger('chosen:updated');
                    }, 500);
                }).catch(function () {

                });
            }
        }
    }]);

