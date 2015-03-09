// lazyload config

angular.module('app')
    /**
   * jQuery plugin config use ui-jq directive , config the js and css files that required
   * key: function name of the jQuery plugin
   * value: array of the css js file located
   */
  .constant('JQ_CONFIG', {
      easyPieChart:   [   'app/bower/jquery.easy-pie-chart/dist/jquery.easypiechart.min.js'],
      sparkline:      [   'app/lib/jquery.sparkline.retina.js'],
      plot:           [   'app/bower/flot/jquery.flot.js',
                          'app/bower/flot/jquery.flot.pie.js', 
                          'app/bower/flot/jquery.flot.resize.js',
                          'app/bower/flot.tooltip/js/jquery.flot.tooltip.js',
                          'app/bower/flot.orderbars/js/jquery.flot.orderBars.js',
                          'app/lib/jquery.flot.spline.min.js'],
      moment:         [   'app/bower/moment/moment.js'],
      screenfull:     [   'app/bower/screenfull/dist/screenfull.min.js'],
      slimScroll:     [   'app/bower/slimscroll/jquery.slimscroll.min.js'],
      sortable:       [   'app/bower/html5sortable/jquery.sortable.js'],
      nestable:       [   'app/bower/nestable/jquery.nestable.js',
                          'app/bower/nestable/jquery.nestable.css'],
      filestyle:      [   'app/bower/bootstrap-filestyle/src/bootstrap-filestyle.js'],
      slider:         [   'app/bower/bootstrap-slider/bootstrap-slider.js',
                          'app/bower/bootstrap-slider/slider.css'],
      chosen:         [   'app/bower/chosen/chosen.jquery.min.js',
                          'app/lib/bootstrap-chosen/bootstrap-chosen.css'],
      TouchSpin:      [   'app/bower/bootstrap-touchspin/dist/jquery.bootstrap-touchspin.min.js',
                          'app/bower/bootstrap-touchspin/dist/jquery.bootstrap-touchspin.min.css'],
      wysiwyg:        [   'app/bower/bootstrap-wysiwyg/bootstrap-wysiwyg.js',
                          'app/bower/bootstrap-wysiwyg/external/jquery.hotkeys.js'],
      dataTable:      [   'app/bower/datatables/media/js/jquery.dataTables.min.js',
                          'app/bower/plugins/integration/bootstrap/3/dataTables.bootstrap.js',
                          'app/bower/plugins/integration/bootstrap/3/dataTables.bootstrap.css'],
      vectorMap:      [   'app/bower/bower-jvectormap/jquery-jvectormap-1.2.2.min.js', 
                          'app/bower/bower-jvectormap/jquery-jvectormap-world-mill-en.js',
                          'app/bower/bower-jvectormap/jquery-jvectormap-us-aea-en.js',
                          'app/bower/bower-jvectormap/jquery-jvectormap-1.2.2.css'],
      footable:       [   'app/bower/footable/dist/footable.all.min.js',
                          'app/bower/footable/css/footable.core.css'],
      fullcalendar:   [   'app/bower/moment/moment.js',
                          'app/bower/fullcalendar/dist/fullcalendar.min.js',
                          'app/bower/fullcalendar/dist/fullcalendar.css',
                          'app/bower/fullcalendar/dist/fullcalendar.theme.css'],
      daterangepicker:[   'app/bower/moment/moment.js',
                          'app/bower/bootstrap-daterangepicker/daterangepicker.js',
                          'app/bower/bootstrap-daterangepicker/daterangepicker-bs3.css'],
      tagsinput:      [   'app/bower/bootstrap-tagsinput/dist/bootstrap-tagsinput.js',
                          'app/bower/bootstrap-tagsinput/dist/bootstrap-tagsinput.css']
                      
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
                      'app/bower/ng-grid/build/ng-grid.min.js',
                      'app/bower/ng-grid/ng-grid.min.css',
                      'app/lib/ng-grid.bootstrap.css'
                  ]
              },
              {
                  name: 'ui.grid',
                  files: [
                      'app/bower/angular-ui-grid/ui-grid.min.js',
                      'app/bower/angular-ui-grid/ui-grid.min.css',
                      'app/bower/angular-ui-grid/ui-grid.bootstrap.css'
                  ]
              },
              {
                  name: 'ui.select',
                  files: [
                      'app/bower/angular-ui-select/dist/select.min.js',
                      'app/bower/angular-ui-select/dist/select.min.css'
                  ]
              },
              {
                  name:'angularFileUpload',
                  files: [
                    'app/bower/angular-file-upload/angular-file-upload.min.js'
                  ]
              },
              {
                  name:'ui.calendar',
                  files: ['app/bower/angular-ui-calendar/src/calendar.js']
              },
              {
                  name: 'ngImgCrop',
                  files: [
                      'app/bower/ngImgCrop/compile/minified/ng-img-crop.js',
                      'app/bower/ngImgCrop/compile/minified/ng-img-crop.css'
                  ]
              },
              {
                  name: 'angularBootstrapNavTree',
                  files: [
                      'app/bower/angular-bootstrap-nav-tree/dist/abn_tree_directive.js',
                      'app/bower/angular-bootstrap-nav-tree/dist/abn_tree.css'
                  ]
              },
              {
                  name: 'toaster',
                  files: [
                      'app/bower/angularjs-toaster/toaster.js',
                      'app/bower/angularjs-toaster/toaster.css'
                  ]
              },
              {
                  name: 'textAngular',
                  files: [
                      'app/bower/textAngular/dist/textAngular-sanitize.min.js',
                      'app/bower/textAngular/dist/textAngular.min.js'
                  ]
              },
              {
                  name: 'vr.directives.slider',
                  files: [
                      'app/bower/venturocket-angular-slider/build/angular-slider.min.js',
                      'app/bower/venturocket-angular-slider/build/angular-slider.css'
                  ]
              },
              {
                  name: 'com.2fdevs.videogular',
                  files: [
                      'app/bower/videogular/videogular.min.js'
                  ]
              },
              {
                  name: 'com.2fdevs.videogular.plugins.controls',
                  files: [
                      'app/bower/videogular-controls/controls.min.js'
                  ]
              },
              {
                  name: 'com.2fdevs.videogular.plugins.buffering',
                  files: [
                      'app/bower/videogular-buffering/buffering.min.js'
                  ]
              },
              {
                  name: 'com.2fdevs.videogular.plugins.overlayplay',
                  files: [
                      'app/bower/videogular-overlay-play/overlay-play.min.js'
                  ]
              },
              {
                  name: 'com.2fdevs.videogular.plugins.poster',
                  files: [
                      'app/bower/videogular-poster/poster.min.js'
                  ]
              },
              {
                  name: 'com.2fdevs.videogular.plugins.imaads',
                  files: [
                      'app/bower/videogular-ima-ads/ima-ads.min.js'
                  ]
              },
              {
                  name: 'xeditable',
                  files: [
                      'app/bower/angular-xeditable/dist/js/xeditable.min.js',
                      'app/bower/angular-xeditable/dist/css/xeditable.css'
                  ]
              },
              {
                  name: 'smart-table',
                  files: [
                      'app/bower/angular-smart-table/dist/smart-table.min.js'
                  ]
              }
          ]
      });
  }])
;
