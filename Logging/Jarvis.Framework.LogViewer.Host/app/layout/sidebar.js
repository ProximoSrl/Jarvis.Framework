(function (window, angular, undefined) {
    'use strict';

    angular
        .module('admin.layout')
        .directive('dsSidebar', dsSidebar);

    function dsSidebar() {
        var directive = {
            link: link,
            templateUrl: '/layout/sidebar.html',
            restrict: 'E',
            replace:true
        };

        return directive;

        function link(scope, element, attrs) {
        }
    };
})(window, window.angular);
