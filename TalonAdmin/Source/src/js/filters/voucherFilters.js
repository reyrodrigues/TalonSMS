'use strict';

/* Filters */
// need load the moment.js to use this filter. 
angular.module('app')
  .filter('totalVouchers', function() {
    return function(distribution) {
        return distribution.Categories.map(function (c) { return c.NumberOfVouchers; }).reduce(function (a, b) { return a + b });
    }
  });