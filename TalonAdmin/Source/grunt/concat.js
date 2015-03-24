module.exports = {
    angular: {
        src: [
        'bower_components/jquery/dist/jquery.min.js',
        'bower_components/bootstrap/dist/js/bootstrap.js',
        '../Scripts/jquery.signalR-2.2.0.min.js',
        'bower_components/chosen/chosen.jquery.min.js',
        'bower_components/moment/min/moment-with-locales.min.js',
        'bower_components/moment-timezone/builds/moment-timezone-with-data.min.js',

        'bower_components/angular/angular.js',

        'bower_components/breeze-client/build/breeze.debug.js',
        'bower_components/breeze-client/build/adapters/breeze.bridge.angular.js',

        'bower_components/angular-file-upload/angular-file-upload.min.js',
        'bower_components/angular-animate/angular-animate.js',
        'bower_components/angular-local-storage/dist/angular-local-storage.min.js',
        'bower_components/angular-cookies/angular-cookies.js',
        'bower_components/angular-resource/angular-resource.js',
        'bower_components/angular-sanitize/angular-sanitize.js',
        'bower_components/angular-touch/angular-touch.js',
        'bower_components/angularjs-toaster/toaster.js',
        'bower_components/ng-grid/build/ng-grid.min.js',
        'bower_components/angular-signalr-hub/signalr-hub.min.js',
        'bower_components/angular-busy/dist/angular-busy.min.js',

        'bower_components/angular-ui-router/release/angular-ui-router.js',
        'bower_components/ngstorage/ngStorage.js',
        'bower_components/angular-ui-utils/ui-utils.js',
        'bower_components/angular-bootstrap/ui-bootstrap-tpls.js',

        'bower_components/oclazyload/dist/ocLazyLoad.js',
        'bower_components/angular-gettext/dist/angular-gettext.min.js',

        'bower_components/angular-dialog-service/dist/dialogs.min.js',

        'bower_components/angular-dialog-service/dist/dialogs-default-translations.min.js',


        'src/js/*.js',
        'src/tpl/app.templates.js',
        'src/js/directives/*.js',
        'src/js/services/*.js',
        'src/js/factories/*.js',
        'src/js/filters/*.js',
        'src/js/controllers/*.js',
        ],
        dest: '../app/js/app.src.js'
    }
}
