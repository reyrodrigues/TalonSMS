'use strict';


app.controller('AppDashboardCtrl', ['breeze', '$scope', '$rootScope', '$http', '$timeout', 'serviceBase', 'Hub', 'toaster',
    function (breeze, $scope, $rootScope, $http, $timeout, serviceBase, Hub, toaster) {
        var loadDashboard = function () {
            $http.get(serviceBase + 'api/Dashboard/DashboardSummary')
                .then(function (res) {
                    $scope.programs = res.data;
                });
        };

        $scope.reports = {
            UsedVsIssued: {
                Data: function (program) {
                    var data = [
                      {
                          data: program.UsedVsIssued.map(function (m) { return [moment.tz(m[0], 'utc').toDate().getTime(), m[1]] }),
                          label: 'Distributed Vouchers',
                          points: { show: true, radius: 6 },
                          splines: { show: true, tension: 0.45, lineWidth: 5, fill: 0 }
                      },
                      {
                          data: program.UsedVsIssued.map(function (m) { return [moment.tz(m[0], 'utc').toDate().getTime(), m[3]] }),
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

        loadDashboard();

        window.loadDashboard = loadDashboard;

        if (!window.dashboardHub) {
            window.dashboardHub = new Hub('dashboardHub', {
                //client side methods
                listeners: {
                    'message': function (type, title, message) {
                        toaster.pop(type, title, message);
                    },
                    'updateDashboard': function () {
                        window.loadDashboard();
                        $rootScope.$apply();
                    }
                },
                //server side methods
                methods: ['Hello'],
                //query params sent on initial connection
                queryParams: {
                    'token': 'exampletoken'
                },
                //handle connection error
                errorHandler: function (error) {
                    console.error(error);
                },

                //specify a non default root
                rootPath: serviceBase + 'signalR/hubs/',

                hubDisconnected: function () {
                    if (hub.connection.lastError) {
                        hub.connection.start()
                        .done(function () {
                            if (hub.connection.state == 0)
                                $timeout(function () { }, 2000);
                            else {
                            }
                        })
                        .fail(function (reason) {
                            console.log(reason);
                        });
                    }
                }
            });
        }
        /*
        
        */
    }]);