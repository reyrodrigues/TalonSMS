'use strict';


app.controller('AppDashboardCtrl', ['breeze', '$scope', '$rootScope', '$http', '$timeout', 'serviceBase', 'Hub', 'toaster',
    function (breeze, $scope, $rootScope, $http, $timeout, serviceBase, Hub, toaster) {
        var loadDashboard = function () {
            $http.get(serviceBase + 'api/Dashboard/DashboardSummary')
                .then(function (res) {
                    $scope.distributions = res.data;
                });
        };

        loadDashboard();

        window.loadDashboard = loadDashboard;

        if(!window.dashboardHub) {
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