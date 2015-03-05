module.exports = {
    angular: {
        src: [
        'bower_components/jquery/dist/jquery.min.js',
        'bower_components/bootstrap/dist/js/bootstrap.js',
        '../Scripts/datajs-1.1.3.js',
        '../Scripts/odatajs.js',
        '../Scripts/NavigationMenu.js',
        '../Scripts/jquery.signalR-2.2.0.min.js',
        'bower_components/chosen/chosen.jquery.min.js',
        'bower_components/moment/moment.js',

        'bower_components/angular/angular.js',

        'bower_components/breeze-client/build/breeze.debug.js',
        'bower_components/breeze-client/build/adapters/breeze.bridge.angular.js',

        'bower_components/angular-animate/angular-animate.js',
        'bower_components/angular-local-storage/dist/angular-local-storage.min.js',
        'bower_components/angular-cookies/angular-cookies.js',
        'bower_components/angular-resource/angular-resource.js',
        'bower_components/angular-sanitize/angular-sanitize.js',
        'bower_components/angular-touch/angular-touch.js',
        'bower_components/angularjs-toaster/toaster.js',
        'bower_components/ng-grid/build/ng-grid.min.js',
        'bower_components/angular-signalr-hub/signalr-hub.min.js',

        'bower_components/angular-ui-router/release/angular-ui-router.js',
        'bower_components/ngstorage/ngStorage.js',
        'bower_components/angular-ui-utils/ui-utils.js',

        'bower_components/angular-bootstrap/ui-bootstrap-tpls.js',

        'bower_components/oclazyload/dist/ocLazyLoad.js',

        'bower_components/angular-translate/angular-translate.js',
        'bower_components/angular-translate-loader-static-files/angular-translate-loader-static-files.js',
        'bower_components/angular-translate-storage-cookie/angular-translate-storage-cookie.js',
        'bower_components/angular-translate-storage-local/angular-translate-storage-local.js',

        'bower_components/angular-dialog-service/dist/dialogs.min.js',

        'bower_components/angular-dialog-service/dist/dialogs-default-translations.min.js',


        'src/js/*.js',
        'src/js/directives/*.js',
        'src/js/services/*.js',
        'src/js/filters/*.js',
        'src/js/controllers/*.js',
        ],
        dest: '../app/js/app.src.js'
    }
}
