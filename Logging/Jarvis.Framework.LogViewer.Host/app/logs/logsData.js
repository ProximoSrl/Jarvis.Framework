(function (window, angular, undefined) {
    'use strict';

    angular.module('admin.logs').factory('logsData', logsData);

    logsData.$inject = ['$http'];

    function logsData($http) {
        var service = {
            getLogs: getLogs
        };

        return service;

        function getLogs(filters, page) {
          
            var request = {
                level: undefined,
                logsPerPage: 5,
                page: page,
                query : filters.searchText,
                appendLevel: function(l) {
                    if (this.level === undefined) {
                        this.level = l;
                        return;
                    }

                    this.level = this.level+',' + l;
                }
            };

            if (filters.debug) request.appendLevel("DEBUG");
            if (filters.info) request.appendLevel("INFO");
            if (filters.warn) request.appendLevel("WARN");
            if (filters.error) request.appendLevel("ERROR");

            console.log('request', request);

            return $http.post('diagnostic/log', request).then(function (d) {
                return d.data;
            });
        }
    }

})(window, window.angular);
