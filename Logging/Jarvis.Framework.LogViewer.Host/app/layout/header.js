(function (window, angular, undefined) {
    'use strict';

    angular
        .module('admin.layout')
        .directive('dsHeader', dsHeader);

    function dsHeader() {
        var directive = {
            link: link,
            templateUrl: '/layout/header.html',
            restrict: 'E',
            replace:true
        };

        return directive;

        function link(scope, element, attrs) {
        }
    };
})(window, window.angular);
