(function(window, angular, undefined) {'use strict';

    angular.module('admin', [
        'ui.router',
        'ui.bootstrap',
        'admin.shared',
        'admin.layout',
        'admin.logs',
        'angularUtils.directives.uiBreadcrumbs',
        'angularUtils.directives.dirPagination'
    ]);

})(window, window.angular);
