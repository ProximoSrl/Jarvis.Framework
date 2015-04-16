(function (window, angular, undefined) {
    'use strict';

    angular
        .module('admin')
        .config(config);

    /**/
    function config($stateProvider, $urlRouterProvider) {
        //
        // For any unmatched url, redirect to /state1
        $urlRouterProvider.otherwise("/logs");
        //
        // Now set up the states
        $stateProvider
            .state('logs', {
                url: "/logs",
                templateUrl: "logs/logs.html",
                controller: "LogsController as logs",
                data: { pageTitle: 'Logs', description: 'what\'s happening...' }
            });
    }
})(window, window.angular);
