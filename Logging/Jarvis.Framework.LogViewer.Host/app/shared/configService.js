(function (window, angular, undefined) {
    'use strict';

    angular.module('admin.shared')
            .factory('configService', configService);

    configService.$inject = ['$http','$q'];

    function configService($http,$q) {
        var service = {
            getTenants: getTenants
        };

        return service;

        var tenantsCache;

        /* */
        function getTenants() {
            var d = $q.defer();

            if (tenantsCache){
                d.resolve(tenantsCache);
            }
            else {
                $http.get('/config/tenants').then(function (r) {
                    tenantsCache = r.data;
                    d.resolve(tenantsCache);
                }, function (err) {
                    d.reject(err);
                });
            }

            return d.promise;
        }
    }

})(window, window.angular);
