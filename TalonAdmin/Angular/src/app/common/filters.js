angular.module('talon.common')
  .filter('dateField', function () {
      return function (date) {
          var datePart = moment(inputValue).tz('utc').toISOString().split('T')[0];

          return moment(datePart).tz('utc').toDate();
      };
  })
  .filter('localeDate', function () {
      return function (date) {
          var datePart = moment(date).tz('utc').toISOString().split('T')[0];

          return moment.tz(datePart, 'utc').format("LL");
      };
  })
  .filter('localeDateTime', function () {
      return function (date) {
          if (date) {
              return moment(date).format("LL LTS");
          }
          else {
              return "";
          }
      };
  })
  .filter('unixTime', function () {
      return function (date) {
          if (date) {
              return moment(date).format("x");
          }
          else {
              return "";
          }
      };
  })
    .filter('gettext', ["gettextCatalog", function (gettextCatalog) {
        function filter(input, context) {
            return gettextCatalog.getString(input, null, context);
        }
        filter.$stateful = true;
        return filter;
    }])
;