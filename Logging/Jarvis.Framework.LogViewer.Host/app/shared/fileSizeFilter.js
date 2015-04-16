(function (window, angular, undefined) {
    'use strict';

    angular.module('admin.shared').filter('fileSize', fileSize);


    function fileSize() {
        function bytesToSize(bytes) {
            if (bytes === undefined || bytes == 0) return '0 Byte';
            var k = 1000;
            var sizes = ['Bytes', 'KB', 'MB', 'GB', 'TB', 'PB', 'EB', 'ZB', 'YB'];
            var i = Math.floor(Math.log(bytes) / Math.log(k));
//            return (Math.floor(bytes / Math.pow(k, i) * 100) / 100) + ' ' + sizes[i];
            return (bytes / Math.pow(k, i)).toPrecision(3) + ' ' + sizes[i];
        }

        return bytesToSize;
    }

})(window, window.angular);
