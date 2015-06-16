'use strict';
angular.module('app')
    .directive('toggleEditing', ['$q', function ($q) {
        return {
            restrict: 'A',
            scope: {
                isEditing: "="
            },
            replace: true,
            link: function (scope, element, attrs) {
                var toggleEditing = function () {
                    var isEditing = scope.isEditing || false;

                    $('input:not(.keep-disabled), select:not(.keep-disabled), textarea:not(.keep-disabled)', element).prop('readonly', !isEditing);
                    if (!isEditing) {
                        $('[type=checkbox]:not(.keep-disabled)').attr('disabled', 'disabled');
                        $('select:not(.keep-disabled)', element).attr('disabled', 'disabled');

                    } else {
                        $('[type=checkbox]:not(.keep-disabled)').removeAttr('disabled');
                        $('select:not(.keep-disabled)', element).removeAttr('disabled');
                    }
                    $('.isChosen').trigger('chosen:updated');
                };

                toggleEditing();

                scope.$watch('isEditing', function () {
                    toggleEditing();
                });
            }
        }
    }]);
