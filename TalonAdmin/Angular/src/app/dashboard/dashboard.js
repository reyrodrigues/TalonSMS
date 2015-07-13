angular.module( 'talon.dashboard', [
  'ui.router',
  'ui.utils',
  'talon.dataAccess'
])

.config(function config( $stateProvider ) {
    $stateProvider.state('dashboard', {
        url: '/dashboard',
        views: {
            "app": {
                controller: 'DashCtrl',
                templateUrl: 'dashboard/dashboard.tpl.html'
            }
        },
    data:{ pageTitle: 'Home' }
  });
})

.controller('DashCtrl', function DashController($rootScope, $scope, $http, entityManagerFactory) {
    $scope.reports = {
        UsedVsIssued: {
            Data: function (program) {
                var data = [
                  {
                      data: program.UsedVsIssued.map(function (m) { return [moment.tz(m[0], 'utc').toDate().getTime(), m[1]]; }),
                      label: 'Distributed Vouchers',
                      points: { show: true, radius: 6 },
                      splines: { show: true, tension: 0.45, lineWidth: 5, fill: 0 }
                  },
                  {
                      data: program.UsedVsIssued.map(function (m) { return [moment.tz(m[0], 'utc').toDate().getTime(), m[3]]; }),
                      label: 'Used Vouchers',
                      points: { show: true, radius: 6 },
                      splines: { show: true, tension: 0.45, lineWidth: 5, fill: 0 }
                  }
                ];

                return data;
            },
            Options: function (program) {
                var first = program.UsedVsIssued[0];
                var last = program.UsedVsIssued[program.UsedVsIssued.length - 1];

                var min = moment(first[0]).add(-1, 'd').toDate();
                var max = moment(last[0]).add(+1, 'd').toDate();

                var options = {
                    colors: [$scope.app.color.info, $scope.app.color.success],
                    series: { shadowSize: 2 },
                    xaxis: { mode: "time", min: min.getTime(), max: max.getTime(), font: { color: '#333' } },
                    yaxis: {
                        font: { color: '#ccc' }
                    },
                    grid: { hoverable: true, clickable: true, borderWidth: 0, color: '#ccc' },
                    tooltip: true,
                    tooltipOpts: { content: '%y', defaultTheme: false, shifts: { x: 0, y: 20 } }
                };

                return options;
            }
        },
        Vendors: {
            Data: function (program) {
                var data = program.Vendor.map(function (v) {
                    return {
                        label: v[0],
                        data: v[1]
                    };
                });

                return data;
            },
            Options: function (program) {
                var options = {
                    series: {
                        pie: {
                            show: true,
                            radius: 1,
                            label: {
                                show: true,
                                radius: 3 / 4,
                                background: {
                                    opacity: 0.5,
                                    color: '#000'
                                },
                                threshold: 0.1
                            }
                        }
                    },
                    grid: { hoverable: true, clickable: true, borderWidth: 0, color: '#333' },
                    tooltip: true,
                    tooltipOpts: { content: function (series, x, y) { return series + '<br/>' + y + ' Vouchers'; }, defaultTheme: false, shifts: { x: 0, y: 20 } }
                };

                return options;
            }
        }
    };

    window.loadDashboard = loadDashboard;
    $rootScope.$watch('currentUser', function () {
        loadDashboard();
    });

    function loadDashboard() {
        $http.get(serviceRoot + 'api/Dashboard/DashboardSummary')
            .then(function (res) {
                $scope.programs = res.data;
            });
    }
})

;

