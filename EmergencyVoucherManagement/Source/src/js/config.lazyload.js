// lazyload config

angular.module('app')
    /**
   * jQuery plugin config use ui-jq directive , config the js and css files that required
   * key: function name of the jQuery plugin
   * value: array of the css js file located
   */
  .constant('JQ_CONFIG', {
      easyPieChart:   [   'bower/jquery.easy-pie-chart/dist/jquery.easypiechart.min.js'],
      sparkline:      [   'lib/jquery.sparkline.retina.js'],
      plot:           [   'bower/flot/jquery.flot.js',
                          'bower/flot/jquery.flot.pie.js', 
                          'bower/flot/jquery.flot.resize.js',
                          'bower/flot.tooltip/js/jquery.flot.tooltip.js',
                          'bower/flot.orderbars/js/jquery.flot.orderBars.js',
                          'lib/jquery.flot.spline.min.js'],
      moment:         [   'bower/moment/moment.js'],
      screenfull:     [   'bower/screenfull/dist/screenfull.min.js'],
      slimScroll:     [   'bower/slimscroll/jquery.slimscroll.min.js'],
      sortable:       [   'bower/html5sortable/jquery.sortable.js'],
      nestable:       [   'bower/nestable/jquery.nestable.js',
                          'bower/nestable/jquery.nestable.css'],
      filestyle:      [   'bower/bootstrap-filestyle/src/bootstrap-filestyle.js'],
      slider:         [   'bower/bootstrap-slider/bootstrap-slider.js',
                          'bower/bootstrap-slider/slider.css'],
      chosen:         [   'bower/chosen/chosen.jquery.min.js',
                          'lib/bootstrap-chosen/bootstrap-chosen.css'],
      TouchSpin:      [   'bower/bootstrap-touchspin/dist/jquery.bootstrap-touchspin.min.js',
                          'bower/bootstrap-touchspin/dist/jquery.bootstrap-touchspin.min.css'],
      wysiwyg:        [   'bower/bootstrap-wysiwyg/bootstrap-wysiwyg.js',
                          'bower/bootstrap-wysiwyg/external/jquery.hotkeys.js'],
      dataTable:      [   'bower/datatables/media/js/jquery.dataTables.min.js',
                          'bower/plugins/integration/bootstrap/3/dataTables.bootstrap.js',
                          'bower/plugins/integration/bootstrap/3/dataTables.bootstrap.css'],
      vectorMap:      [   'bower/bower-jvectormap/jquery-jvectormap-1.2.2.min.js', 
                          'bower/bower-jvectormap/jquery-jvectormap-world-mill-en.js',
                          'bower/bower-jvectormap/jquery-jvectormap-us-aea-en.js',
                          'bower/bower-jvectormap/jquery-jvectormap-1.2.2.css'],
      footable:       [   'bower/footable/dist/footable.all.min.js',
                          'bower/footable/css/footable.core.css'],
      fullcalendar:   [   'bower/moment/moment.js',
                          'bower/fullcalendar/dist/fullcalendar.min.js',
                          'bower/fullcalendar/dist/fullcalendar.css',
                          'bower/fullcalendar/dist/fullcalendar.theme.css'],
      daterangepicker:[   'bower/moment/moment.js',
                          'bower/bootstrap-daterangepicker/daterangepicker.js',
                          'bower/bootstrap-daterangepicker/daterangepicker-bs3.css'],
      tagsinput:      [   'bower/bootstrap-tagsinput/dist/bootstrap-tagsinput.js',
                          'bower/bootstrap-tagsinput/dist/bootstrap-tagsinput.css']
                      
    }
  )
  // oclazyload config
  .config(['$ocLazyLoadProvider', function($ocLazyLoadProvider) {
      // We configure ocLazyLoad to use the lib script.js as the async loader
      $ocLazyLoadProvider.config({
          modules: [
              {
                  name: 'ngGrid',
                  files: [
                      'bower/ng-grid/build/ng-grid.min.js',
                      'bower/ng-grid/ng-grid.min.css',
                      'lib/ng-grid.bootstrap.css'
                  ]
              },
              {
                  name: 'ui.grid',
                  files: [
                      'bower/angular-ui-grid/ui-grid.min.js',
                      'bower/angular-ui-grid/ui-grid.min.css',
                      'bower/angular-ui-grid/ui-grid.bootstrap.css'
                  ]
              },
              {
                  name: 'ui.select',
                  files: [
                      'bower/angular-ui-select/dist/select.min.js',
                      'bower/angular-ui-select/dist/select.min.css'
                  ]
              },
              {
                  name:'angularFileUpload',
                  files: [
                    'bower/angular-file-upload/angular-file-upload.min.js'
                  ]
              },
              {
                  name:'ui.calendar',
                  files: ['bower/angular-ui-calendar/src/calendar.js']
              },
              {
                  name: 'ngImgCrop',
                  files: [
                      'bower/ngImgCrop/compile/minified/ng-img-crop.js',
                      'bower/ngImgCrop/compile/minified/ng-img-crop.css'
                  ]
              },
              {
                  name: 'angularBootstrapNavTree',
                  files: [
                      'bower/angular-bootstrap-nav-tree/dist/abn_tree_directive.js',
                      'bower/angular-bootstrap-nav-tree/dist/abn_tree.css'
                  ]
              },
              {
                  name: 'toaster',
                  files: [
                      'bower/angularjs-toaster/toaster.js',
                      'bower/angularjs-toaster/toaster.css'
                  ]
              },
              {
                  name: 'textAngular',
                  files: [
                      'bower/textAngular/dist/textAngular-sanitize.min.js',
                      'bower/textAngular/dist/textAngular.min.js'
                  ]
              },
              {
                  name: 'vr.directives.slider',
                  files: [
                      'bower/venturocket-angular-slider/build/angular-slider.min.js',
                      'bower/venturocket-angular-slider/build/angular-slider.css'
                  ]
              },
              {
                  name: 'com.2fdevs.videogular',
                  files: [
                      'bower/videogular/videogular.min.js'
                  ]
              },
              {
                  name: 'com.2fdevs.videogular.plugins.controls',
                  files: [
                      'bower/videogular-controls/controls.min.js'
                  ]
              },
              {
                  name: 'com.2fdevs.videogular.plugins.buffering',
                  files: [
                      'bower/videogular-buffering/buffering.min.js'
                  ]
              },
              {
                  name: 'com.2fdevs.videogular.plugins.overlayplay',
                  files: [
                      'bower/videogular-overlay-play/overlay-play.min.js'
                  ]
              },
              {
                  name: 'com.2fdevs.videogular.plugins.poster',
                  files: [
                      'bower/videogular-poster/poster.min.js'
                  ]
              },
              {
                  name: 'com.2fdevs.videogular.plugins.imaads',
                  files: [
                      'bower/videogular-ima-ads/ima-ads.min.js'
                  ]
              },
              {
                  name: 'xeditable',
                  files: [
                      'bower/angular-xeditable/dist/js/xeditable.min.js',
                      'bower/angular-xeditable/dist/css/xeditable.css'
                  ]
              },
              {
                  name: 'smart-table',
                  files: [
                      'bower/angular-smart-table/dist/smart-table.min.js'
                  ]
              }
          ]
      });
  }])
;
