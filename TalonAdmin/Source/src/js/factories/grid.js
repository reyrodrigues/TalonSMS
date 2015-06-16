'use strict';

angular.module('app')
    .factory('GridHelpers', ['gettext', function (gettext) {
        var GridColumnHelper = function () {
            this.name = "";
            this.displayText = "";
            this.cellTemplate = "";
            this.isSortable = "";
        };

        GridColumnHelper.prototype.named = function (name) {
            this.name = name;
            return this;
        };

        GridColumnHelper.prototype.header = function (text) {
            this.displayText = text;
            return this;
        };

        GridColumnHelper.prototype.template = function (template) {
            this.cellTemplate = template;
            return this;
        };

        GridColumnHelper.prototype.sortable = function (sortable) {
            this.isSortable = sortable;
            return this;
        };

        GridColumnHelper.prototype.asArray = function () {
            var result = [this.name, gettext(this.displayText || this.name)];

            if (this.cellTemplate !== "") {
                result.push(this.cellTemplate);
            }

            if (this.isSortable !== "") {
                result.push(this.isSortable);
            }

            return result;
        };
    }]);