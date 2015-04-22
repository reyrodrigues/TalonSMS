'use strict';

/* Filters */
// need load the moment.js to use this filter. 
angular.module('app')
  .filter('dateField', function () {
      return function (date) {
          var datePart = moment(inputValue).tz('utc').toISOString().split('T')[0];

          return moment(datePart).tz('utc').toDate();
      }
  });
angular.module('app')
  .filter('localeDate', function () {
      return function (date) {
          var datePart = moment(date).tz('utc').toISOString().split('T')[0];

          return moment.tz(datePart, 'utc').format("LL");
      }
  });
angular.module('app')
  .filter('localeDateTime', function () {
      return function (date) {
          if (date)
              return moment(date).format("LL LTS");
          else
              return "";
      }
  });