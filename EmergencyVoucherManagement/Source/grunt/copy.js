module.exports = {
    angular: {
        files: [
            { expand: true, src: "**", cwd: 'bower_components/bootstrap/fonts', dest: "../App/fonts" },
            { expand: true, src: "**", cwd: 'bower_components/font-awesome/fonts', dest: "../App/fonts" },
            { expand: true, src: "**", cwd: 'bower_components/Simple-Line-Icons/fonts', dest: "../App/fonts" },
            { expand: true, src: "**", cwd: 'src/fonts', dest: "../App/fonts" },
            { expand: true, src: "**", cwd: 'src/api', dest: "../App/api" },
            { expand: true, src: "**", cwd: 'src/img', dest: "../App/img" },
            { expand: true, src: "**", cwd: 'src/js', dest: "../App/js" },
            { expand: true, src: "**", cwd: 'src/tpl', dest: "../App/tpl" },
            { expand: true, src: "**", cwd: 'src/lib', dest: "../App/lib" },
            { src: 'src/index.min.html', dest: '../App/index.html' }
        ]
    },
    bower: {
        files: [
            { expand: true, src: "**/*.js", cwd: 'bower_components', dest: "../App/bower" },
            { expand: true, src: "**/*.css", cwd: 'bower_components', dest: "../App/bower" }
        ]
    },
};
