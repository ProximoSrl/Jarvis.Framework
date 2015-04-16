(function (window, angular, undefined) {
    'use strict';

    angular.module('admin.logs').controller('LogsController', LogsController);

    LogsController.$inject = ['$scope', 'logsData'];

    function LogsController($scope, logsData) {
        var vm = this;

        vm.items = [];
        vm.totalItems = 0;
        vm.filters = {
            info: false,
            warn: false,
            error: false,
            debug: false,
            searchText : ''
        };


        vm.refresh = refresh;
        vm.page = 1;
        vm.pageChanged = pageChanged;

        start();

        /* */
        function start() {
        };

        function pageChanged(newPage) {
            vm.page = newPage;
            refresh();
        };

        function refresh() {
            logsData.getLogs(vm.filters, vm.page).then(function (data) {
                console.log('logs from server', data);
                vm.items = data.items;
                vm.totalItems = data.count;
            });
        };

        $scope.$watch(function() {
            return vm.filters.info + '|'
                + vm.filters.debug + '|'
                + vm.filters.warn + '|'
                + vm.filters.error + '|'
                + vm.filters.searchText;
        }, function() {
            refresh();
        });
    }
})(window, window.angular);
