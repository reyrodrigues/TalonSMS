'use strict';

/* Filters */
// need load the moment.js to use this filter. 
angular.module('app')
  .filter('totalVouchers', function () {
      return function (distribution) {
          return distribution.Categories.map(function (c) { return c.NumberOfVouchers; }).reduce(function (a, b) { return a + b });
      }
  })
  .filter('voucherStatus', function () {
      return function (status) {
          if (typeof (status) == 'undefined')
              return "Not Assigned";

          status = parseInt(status);
          if (status == 0) {
              return "Issued"
          } else if (status == 2) {
              return "Used";
          } else if (status == 3) {
              return "Cancelled";
          }
      }
  })

;
