(function (window, angular, undefined) {
    'use strict';

    angular.module('admin.shared')
        .directive('updateTitle', updateTitle);

    updateTitle.$inject = ['$rootScope', '$timeout'];

    function updateTitle($rootScope, $timeout) {
        return {
            link: function(scope, element) {
                var listener = function(event, toState, toParams, fromState, fromParams) {
                    var title = 'DocumentStore';
                    if (toState.data && toState.data.pageTitle)
                        title = toState.data.pageTitle;

                    if (toState.data && toState.data.description)
                        title = title +"<small>"+toState.data.description+"</small>";

                    // Set asynchronously so page changes before title does
                    $timeout(function() {
                        element.html(title);
                    });
                };

                $rootScope.$on('$stateChangeStart', listener);
            }
        }
    };

})(window, window.angular);
