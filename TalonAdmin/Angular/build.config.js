/**
 * This file/module contains all configuration for the build process.
 */
module.exports = {
    /**
     * The `build_dir` folder is where our projects are compiled during
     * development and the `compile_dir` folder is where our app resides once it's
     * completely built.
     */
    build_dir: 'build',
    compile_dir: '../app',

    /**
     * This is a collection of file patterns that refer to our app code (the
     * stuff in `src/`). These file paths are used in the configuration of
     * build tasks. `js` is all project javascript, less tests. `ctpl` contains
     * our reusable components' (`src/common`) template HTML files, while
     * `atpl` contains the same, but for our app's code. `html` is just our
     * main HTML file, `less` is our main stylesheet, and `unit` contains our
     * app's unit tests.
     */
    app_files: {
        js: ['src/**/*.js', '!src/**/*.spec.js', '!src/assets/**/*.js'],
        jsunit: ['src/**/*.spec.js'],

        coffee: ['src/**/*.coffee', '!src/**/*.spec.coffee'],
        coffeeunit: ['src/**/*.spec.coffee'],

        atpl: ['src/app/**/*.tpl.html'],
        ctpl: ['src/common/**/*.tpl.html'],

        html: ['src/index.html'],
        less: 'src/less/main.less'
    },

    /**
     * This is a collection of files used during testing only.
     */
    test_files: {
        js: [
          'vendor/angular-mocks/angular-mocks.js'
        ]
    },

    /**
     * This is the same as `app_files`, except it contains patterns that
     * reference vendor code (`vendor/`) that we need to place into the build
     * process somewhere. While the `app_files` property ensures all
     * standardized files are collected for compilation, it is the user's job
     * to ensure non-standardized (i.e. vendor-related) files are handled
     * appropriately in `vendor_files.js`.
     *
     * The `vendor_files.js` property holds files to be automatically
     * concatenated and minified with our project source files.
     *
     * The `vendor_files.css` property holds any CSS files to be automatically
     * included in our app.
     *
     * The `vendor_files.assets` property holds any assets to be copied along
     * with our app's assets. This structure is flattened, so it is not
     * recommended that you use wildcards.
     */
    vendor_files: {
        js: [
         'vendor/bootstrap/dist/js/bootstrap.min.js',
        'vendor/angular/angular.js',
        'vendor/angular-bootstrap/ui-bootstrap.min.js',
        'vendor/angular-bootstrap/ui-bootstrap-tpls.min.js',
        'vendor/placeholders/angular-placeholders-0.0.1-SNAPSHOT.min.js',
        'vendor/angular-ui-router/release/angular-ui-router.js',
        'vendor/angular-ui-utils/modules/route/route.js',
        'vendor/ngstorage/ngStorage.min.js',
        'vendor/breeze-client/build/breeze.min.js',
        'vendor/breeze-client/build/adapters/breeze.bridge.angular.js',
        'vendor/angular-ui-grid/ui-grid.min.js',
        'vendor/datatables/media/js/jquery.dataTables.min.js',
        'vendor/angular-datatables/dist/plugins/bootstrap/angular-datatables.bootstrap.min.js',
        'vendor/angular-datatables/dist/angular-datatables.min.js',
        'vendor/angular-sanitize/angular-sanitize.min.js',
        'vendor/angular-animate/angular-animate.min.js',
        'vendor/angularjs-toaster/toaster.min.js',
        'vendor/angular-dialog-service/dist/dialogs.min.js',
        'vendor/moment/min/moment-with-locales.min.js',
        'vendor/moment-timezone/builds/moment-timezone-with-data.min.js',
        'vendor/ng-file-upload/ng-file-upload.min.js',
        'vendor/chosen/chosen.jquery.min.js',
        'vendor/footable/dist/footable.all.min.js',
        'vendor/flot/excanvas.js',
        'vendor/flot/jquery.flot.js',
        'vendor/flot/jquery.flot.*.js',
        'vendor/flot.orderbars/js/jquery.flot.orderBars.js',
        'vendor/flot.tooltip/js/jquery.flot.tooltip.js',
        'vendor/flot-spline/js/jquery.flot.spline.js',
        'vendor/angular-block-ui/dist/angular-block-ui.min.js',


        'lib/ui-utils-ieshiv.min.js',
        'lib/ui-utils.min.js'
        ],
        css: [
          'vendor/datatables/media/css/jquery.dataTables.min.css',
          'vendor/angular-datatables/dist/plugins/bootstrap/datatables.bootstrap.min.css',
          'vendor/angularjs-toaster/toaster.min.css',
          'vendor/angular-dialog-service/dist/dialogs.min.css',
          'vendor/angular-block-ui/dist/angular-block-ui.min.css',
        ],
        assets: [
          'vendor/chosen/chosen-sprite.png',
          'vendor/angular-bootstrap-chosen/dist/img/spinner.gif',
          'vendor/jqplot-bower/dist/jquery.jqplot.min.css',
          'vendor/footable/css/fonts/footable.ttf',
          'vendor/footable/css/fonts/footable.woff',
          'vendor/footable/css/fonts/footable.svg',
          'vendor/footable/css/fonts/footable.eot',
          'vendor/angular-ui-grid/*.ttf',
          'vendor/angular-ui-grid/*.woff',
          'vendor/angular-ui-grid/*.svg',
          'vendor/angular-ui-grid/*.eot',
          'vendor/angular-ui-grid/*.css'
        ]
    },
};
