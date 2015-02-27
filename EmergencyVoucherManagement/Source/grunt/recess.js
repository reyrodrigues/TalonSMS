module.exports = {
    less: {
        files: {
            'src/css/app.css': [
              'src/css/less/app.less'
            ]
        },
        options: {
            compile: true
        }
    },
    angular: {
        files: {
            '../App/css/app.min.css': [
                'bower_components/bootstrap/dist/css/bootstrap.css',
                'bower_components/animate.css/animate.css',
                'bower_components/font-awesome/css/font-awesome.css',
                'bower_components/simple-line-icons/css/simple-line-icons.css',
                'bower_components/angular-dialog-service/dist/dialogs.min.css',
                'src/css/*.css',
                'lib/bootstrap-chosen/bootstrap-chosen.css',
                'bower_components/angularjs-toaster/toaster.css'
            ]
        },
        options: {
            compress: true
        }
    }
}
