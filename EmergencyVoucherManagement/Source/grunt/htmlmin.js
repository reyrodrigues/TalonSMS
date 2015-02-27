module.exports = {
	min: {
      files: [{
          expand: true,
          cwd: 'src/tpl/',
          src: ['*.html', '**/*.html'],
          dest: '../App/tpl/',
          ext: '.html',
          extDot: 'first'
      }]
  }
}