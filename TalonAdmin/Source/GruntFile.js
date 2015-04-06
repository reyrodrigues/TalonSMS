module.exports = function (grunt) {
    var gtx = require('gruntfile-gtx').wrap(grunt);

    gtx.loadAuto();

    var gruntConfig = require('./grunt');
    gruntConfig.package = require('./package.json');

    gtx.config(gruntConfig);

    gtx.loadNpm('grunt-contrib-watch');
    gtx.loadNpm('grunt-yaml');
    gtx.loadNpm('grunt-angular-templates');
    gtx.loadNpm('grunt-angular-gettext');

    gtx.config({
        ngtemplates: {
            app: {
                cwd: 'src/',
                src: 'tpl/**/*.html',
                dest: 'src/tpl/app.templates.js',
                options: {
                    htmlmin: {
                        collapseBooleanAttributes: true,
                        collapseWhitespace: true
                    }
                }
            }
        }
    });

    gtx.config({
        nggettext_compile: {
            all: {
                files: {
                    'src/js/translations.js': ['po/*.po']
                }
            }
        }
    });

    gtx.config({
        nggettext_extract: {
            pot: {
                files: {
                    'po/template.pot': ['src/tpl/**/*.html', 'src/js/**/*.js']
                }
            }
        }
    });

    gtx.config({
        yaml: {
            angular: {
                options: {
                    ignored: /^_/,
                    space: 4,
                    customTypes: {
                        '!include scalar': function (value, yamlLoader) {
                            return yamlLoader(value);
                        },
                        '!max sequence': function (values) {
                            return Math.max.apply(null, values);
                        },
                        '!extend mapping': function (value, yamlLoader) {
                            return _.extend(yamlLoader(value.basePath), value.partial);
                        }
                    }
                },
                files: [
                  { expand: true, cwd: 'src/l10n/', src: ['**/*.yaml'], dest: '../App/l10n/' }
                ]
            },
        }
    });
    gtx.config({
        watch: {
            scripts: {
                files: ['src/**/*.*'],
                tasks: ['dev'],
                options: {
                    spawn: false,
                },
            }
        }
    });

    // We need our bower components in order to develop
    gtx.alias('build', ['recess:less', 'nggettext_compile', 'ngtemplates', 'copy:angular', 'copy:bower', 'recess:angular', 'concat:angular', 'uglify:angular']);
    gtx.alias('dev', ['recess:less', 'nggettext_compile', 'ngtemplates', 'copy:angular', 'recess:angular', 'concat:angular']);

    gtx.alias('release', ['bower-install-simple', 'build:dev', 'bump-commit']);
    gtx.alias('release-patch', ['bump-only:patch', 'release']);
    gtx.alias('release-minor', ['bump-only:minor', 'release']);
    gtx.alias('release-major', ['bump-only:major', 'release']);
    gtx.alias('prerelease', ['bump-only:prerelease', 'release']);



    gtx.finalise();
}
