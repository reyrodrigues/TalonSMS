angular.module('app').run(['$templateCache', function($templateCache) {
  'use strict';

  $templateCache.put('tpl/admin/locations/form.html',
    "<div class=\"panel panel-default\"><div class=\"panel-body\"><div class=\"form-group\"><label class=\"col-lg-2 control-label\" translate>Name</label><div class=\"col-lg-10\"><input type=\"text\" class=\"form-control\" placeholder=\"{{'Name'|translate}}\" ng-model=\"entity.Name\" required></div></div></div></div>"
  );


  $templateCache.put('tpl/admin/voucherTypes/form.html',
    "<div class=\"panel panel-default\"><div class=\"panel-body\"><div class=\"form-group\"><label class=\"col-lg-2 control-label\" translate>Name</label><div class=\"col-lg-10\"><input type=\"text\" class=\"form-control\" placeholder=\"{{'Name'|translate}}\" ng-model=\"entity.Name\" required></div></div></div></div>"
  );


  $templateCache.put('tpl/app.html',
    "<!-- navbar --><div data-ng-include=\" 'tpl/blocks/header.html' \" class=\"app-header navbar\"></div><!-- / navbar --><!-- menu --><div data-ng-include=\" 'tpl/blocks/aside.html' \" class=\"app-aside hidden-xs {{app.settings.asideColor}}\"></div><!-- / menu --><!-- content --><div class=\"app-content\"><div ui-butterbar></div><a href class=\"off-screen-toggle hide\" ui-toggle-class=\"off-screen\" data-target=\".app-aside\"></a><div class=\"app-content-body fade-in-up\" ui-view></div></div><!-- /content --><!-- footer --><div class=\"app-footer wrapper b-t bg-light\"><span class=\"pull-right\">{{app.version}} <a href ui-scroll-to=\"app\" class=\"m-l-sm text-muted\"><i class=\"fa fa-long-arrow-up\"></i></a></span> Copyright &copy; The International Rescue Committee, {{ app.currentYear }}.</div><!-- / footer -->"
  );


  $templateCache.put('tpl/app_dashboard.html',
    "<div class=\"hbox hbox-auto-xs hbox-auto-sm\" ng-init=\"\r" +
    "\n" +
    "    app.settings.asideFolded = false;\r" +
    "\n" +
    "    app.settings.asideDock = false;\r" +
    "\n" +
    "  \"><!-- main --><div class=\"col\"><!-- main header --><div class=\"bg-light lter b-b wrapper-md\"><div class=\"row\"><div class=\"col-sm-6 col-xs-12\"><h1 class=\"m-n font-thin h3 text-black\">Dashboard</h1></div><div class=\"col-sm-6 text-right hidden-xs\"></div></div></div><!-- / main header --><div class=\"wrapper-md\" ng-controller=\"DashboardCtrl\"><div ng-repeat=\"d in distributions\"><!-- stats --><div class=\"panel\"><div class=\"panel-heading no-border bg-primary\"><h1 class=\"m-n h4\"><a class=\"text-black\" ui-sref=\"distributions.edit({id: d.Distribution.Id})\" href=\"\">{{ d.Distribution.Title || \"Distribution \" + d.Location.Name }} - {{ d.Location.Name }}</a></h1></div><div class=\"panel-body\"><div class=\"row\"><div class=\"col-md-5\"><div class=\"row row-sm text-center\"><div class=\"col-xs-6\"><div class=\"panel padder-v bg-primary item\"><div class=\"h1 text-black font-thin h1\">{{ d.Beneficiaries }}</div><span class=\"text-xs text-black\">Beneficiaries</span></div></div><div class=\"col-xs-6\"><div class=\"panel padder-v bg-primary item\"><div class=\"h1 text-black font-thin h1\">{{ d.Vendors }}</div><span class=\"text-xs text-black\">Vendors</span></div></div><div class=\"col-xs-6\"><div class=\"panel padder-v bg-primary item\"><div class=\"h1 text-black font-thin h1\">{{ d.TotalVouchers }}</div><span class=\"text-xs text-black\">Vouchers Issued</span></div></div><div class=\"col-xs-6\"><div class=\"panel padder-v bg-primary item\"><div class=\"h1 text-black font-thin h1\">{{ d.InactiveVouchers }}</div><span class=\"ext-xs text-black\">Vouchers Used</span></div></div></div></div><div class=\"col-md-7\"><div class=\"row row-sm text-center\"><div class=\"col-xs-12 m-b-md\"><div class=\"r bg-dark item hbox no-border\"><div class=\"col dk padder-v r-r\"><div class=\"text-primary-lter font-thin h1\"><span>{{ d.IssuedAmount|currency }}</span></div><span class=\"text-primary-lter text-xs\">Issued</span></div></div></div><div class=\"col-xs-12 m-b-md\"><div class=\"r bg-dark item hbox no-border\"><div class=\"col dk padder-v r-r\"><div class=\"text-primary-lter font-thin h1\"><span>{{ (d.ClaimedAmount || 0)|currency }}</span></div><span class=\"text-primary-lter text-xs\">Claimed</span></div></div></div></div></div></div></div></div></div><!-- / stats --></div></div><!-- / main --></div>"
  );


  $templateCache.put('tpl/beneficiaries/bulkEdit.html',
    "<div class=\"bg-light lter b-b wrapper-md\"><h1 class=\"m-n font-thin h3\" translate>Beneficiaries - <small>Assign Group</small></h1></div><div class=\"vbox\"><div class=\"col\"><div class=\"wrapper bg-light lter b-b\"><form class=\"form-horizontal\" ng-submit=\"runFilters()\" role=\"form\"><div class=\"form-group\"><div class=\"col-sm-4\"><label class=\"col-sm-1 control-label\">Sex &nbsp;</label><div class=\"col-sm-3\"><select class=\"form-control\" ng-model=\"bulkFilters.Sex\"><option></option><option value=\"1\">Female</option><option value=\"0\">Male</option></select></div><label class=\"col-sm-2 control-label\">Location &nbsp;</label><div class=\"col-sm-6\"><input type=\"text\" ng-model=\"bulkFilters.Location\" typeahead=\"item as item.Name for item in filterLocations($viewValue) | limitTo:8\" class=\"form-control\"></div></div><div class=\"col-sm-8\"><label class=\"col-sm-2 control-label\">Date of birth</label><label class=\"col-sm-1 control-label\">From:</label><div class=\"col-sm-4\"><input type=\"date\" ng-model=\"bulkFilters.DateOfBirthFrom\" class=\"form-control\"></div><label class=\"col-sm-1 control-label\">To:</label><div class=\"col-sm-4\"><input type=\"date\" ng-model=\"bulkFilters.DateOfBirthTo\" class=\"form-control\"></div></div></div></form></div></div><div class=\"wrapper-md col\"><div class=\"panel panel-default\"><div class=\"table-responsive\"><div ng-grid=\"gridOptions\" style=\"min-height:320px\"></div></div></div></div><div class=\"wrapper-md col\"><div class=\"btn-group\"><a href ng-click=\"assignToGroup()\" class=\"btn btn-success\">Assign Group</a></div></div></div>"
  );


  $templateCache.put('tpl/beneficiaries/create.html',
    "<div class=\"bg-light lter b-b wrapper-md\"><h1 class=\"m-n font-thin h3\">Beneficiary Information - <small>{{ entity.Name }}</small></h1></div><div class=\"wrapper-md\" toggle-editing is-editing=\"isEditing\"><form class=\"bs-example form-horizontal form-validation\" ng-submit=\"save()\"><div class=\"row\"><div class=\"col-sm-12\" ng-include=\"'tpl/beneficiaries/form.html'\"></div></div><div class=\"row\"><div class=\"col-sm-12\" ng-if=\"isEditing\"><div class=\"form-group\"><div class=\"col-lg-12\"><div class=\"btn-group pull-right\"><button type=\"submit\" class=\"btn btn-primary\">Save</button> <button ui-sref=\"beneficiaries.list\" class=\"btn btn-info\">Back</button></div></div></div></div></div></form></div>"
  );


  $templateCache.put('tpl/beneficiaries/edit.html',
    "<div class=\"bg-light lter b-b wrapper-md\"><h1 class=\"m-n font-thin h3\">Beneficiary Information - <small>{{ entity.Name }}</small></h1></div><div class=\"wrapper-md\"><form class=\"bs-example form-horizontal form-validation\" ng-submit=\"save()\"><div class=\"row\"><div class=\"col-sm-12\" ng-include=\"'tpl/beneficiaries/form.html'\" toggle-editing is-editing=\"isEditing\"></div></div><div class=\"row\"><div class=\"col-sm-12\"><h1 class=\"m-n font-thin h4\">Voucher History</h1></div></div><div class=\"row\"><div class=\"col-sm-12\"><div class=\"panel panel-default\"><div class=\"table-responsive\"><div ng-grid=\"gridOptionsVoucherTransactionRecords\" style=\"min-height:300px\"></div></div></div></div></div><div class=\"row\"><div class=\"col-sm-12\" ng-if=\"!isEditing\"><div class=\"form-group\"><div class=\"col-lg-6\"><div class=\"btn-group\"><a href ng-click=\"delete()\" class=\"btn btn-sm btn-danger\">Delete</a> <button ng-click=\"deactivate()\" class=\"btn btn-sm btn-danger\" ng-if=\"!entity.Disabled\">Deactivate</button> <button ng-click=\"reactivate()\" class=\"btn btn-sm btn-success\" ng-if=\"entity.Disabled\">Reactivate</button></div><button ng-click=\"resendMessage()\" class=\"btn btn-sm btn-info\" ng-if=\"entity.WasWelcomeMessageSent && !entity.Disabled\">Resend Welcome Message</button></div><div class=\"col-lg-6\"><div class=\"btn-group pull-right\" ng-if=\"!entity.Disabled\"><button ng-click=\"startEditing()\" class=\"btn btn-primary\">Edit</button> <button ui-sref=\"beneficiaries.list\" class=\"btn btn-info\">Back</button></div></div></div></div></div><div class=\"row\"><div class=\"col-sm-12\" ng-if=\"isEditing\"><div class=\"form-group\"><div class=\"col-lg-12\"><div class=\"btn-group pull-right\"><button type=\"submit\" class=\"btn btn-primary\">Save</button> <button ng-click=\"save(true)\" class=\"btn btn-primary\">Save and Continue Editing</button> <button ng-click=\"endEditing()\" class=\"btn btn-info\">Back</button></div></div></div></div></div></form></div>"
  );


  $templateCache.put('tpl/beneficiaries/form.html',
    "<div class=\"panel panel-default\"><div class=\"panel-heading font-bold\">Basic Information</div><div class=\"panel-body\"><div class=\"form-group\"><label class=\"col-lg-2 control-label\">Name</label><div class=\"col-lg-4\"><input type=\"text\" class=\"form-control\" placeholder=\"Name\" ng-model=\"entity.Name\" required></div><label class=\"col-lg-2 control-label\">Mobile Number</label><div class=\"col-lg-4\"><div class=\"input-group m-b\"><span class=\"input-group-addon\">+</span> <input type=\"text\" class=\"form-control\" placeholder=\"Mobile Number\" ng-model=\"entity.MobileNumber\" ng-pattern=\"/[0-9]$/\" required></div></div></div><div class=\"line line-dashed b-b line-lg pull-in\"></div><div class=\"form-group\"><label class=\"col-sm-2 control-label\">Date of Birth</label><div class=\"col-sm-4\"><input type=\"date\" class=\"form-control\" date-field ng-model=\"entity.DateOfBirth\"></div><label class=\"col-sm-2 control-label\">Sex</label><div class=\"col-sm-4\"><select chosen class=\"form-control\" ng-model=\"entity.Sex\" required><option></option><option value=\"1\">Female</option><option value=\"0\">Male</option></select></div></div><div class=\"line line-dashed b-b line-lg pull-in\"></div><div class=\"form-group\"><label class=\"col-lg-2 control-label\">National Id</label><div class=\"col-lg-4\"><input type=\"text\" class=\"form-control\" placeholder=\"National Id\" ng-model=\"entity.NationalId\"></div><label class=\"col-lg-2 control-label\">IRC Id</label><div class=\"col-lg-4\"><input type=\"text\" class=\"form-control\" placeholder=\"IRC Id\" ng-model=\"entity.IRCId\" disabled></div></div><div class=\"line line-dashed b-b line-lg pull-in\"></div><div class=\"form-group\"><label class=\"col-sm-2 control-label\">Location</label><div class=\"col-sm-4\"><select chosen class=\"form-control\" ng-model=\"entity.LocationId\" ng-options=\"item.Id as item.Name for item in locations | orderBy:'item.Name'\"></select></div><label class=\"col-sm-2 control-label\">Group</label><div class=\"col-sm-4\"><select chosen class=\"form-control\" ng-model=\"entity.GroupId\" ng-options=\"item.Id as item.Name for item in groups | orderBy:'item.Name'\"></select></div></div></div></div>"
  );


  $templateCache.put('tpl/beneficiaries/list.html',
    "<div class=\"bg-light lter b-b wrapper-md\"><h1 class=\"m-n font-thin h3\">Beneficiaries</h1></div><div class=\"vbox\" ng-controller=\"BeneficiaryGridCtrl\"><div class=\"col\"><div class=\"wrapper bg-light lter b-b\"><div class=\"btn-toolbar\"><div class=\"btn-group dropdown\"><button class=\"btn btn-default btn-sm btn-bg dropdown-toggle\" data-toggle=\"dropdown\"><span class=\"dropdown-label\">Filter</span> <span class=\"caret\"></span></button><ul class=\"dropdown-menu text-left text-sm\"><li ng-if=\"!showingDisabled\"><a href ng-click=\"showDisabled()\">Show Deactivated</a></li><li ng-if=\"showingDisabled\"><a href ng-click=\"hideDisabled()\">Show All</a></li></ul></div><div class=\"btn-group\"><button class=\"btn btn-sm btn-bg btn-default\" data-toggle=\"tooltip\" data-placement=\"bottom\" ng-click=\"loadGridData()\" title=\"Refresh\"><i class=\"fa fa-refresh\"></i></button></div></div></div></div><div class=\"wrapper-md col\"><div class=\"panel panel-default\"><div class=\"table-responsive\"><div ng-grid=\"gridOptions\" style=\"min-height:620px\"></div></div></div></div></div>"
  );


  $templateCache.put('tpl/blank.html',
    ""
  );


  $templateCache.put('tpl/blocks/aside.html',
    "<div class=\"aside-wrap\"><!-- if you want to use a custom scroll when aside fixed, use the slimScroll\r" +
    "\n" +
    "      <div class=\"navi-wrap\" ui-jq=\"slimScroll\" ui-options=\"{height:'100%', size:'8px'}\">\r" +
    "\n" +
    "    --><div class=\"navi-wrap\"><!-- nav --><nav ui-nav class=\"navi clearfix\" ng-include=\"'tpl/blocks/nav.html'\"></nav><!-- nav --></div></div>"
  );


  $templateCache.put('tpl/blocks/header.html',
    "<!-- navbar header --><div class=\"navbar-header {{app.settings.navbarHeaderColor}}\"><button class=\"pull-right visible-xs dk\" ui-toggle-class=\"show\" data-target=\".navbar-collapse\"><i class=\"glyphicon glyphicon-cog\"></i></button> <button class=\"pull-right visible-xs\" ui-toggle-class=\"off-screen\" data-target=\".app-aside\" ui-scroll-to=\"app\"><i class=\"glyphicon glyphicon-align-justify\"></i></button><!-- brand --> <a href=\"\" ui-sref=\"app.dashboard\" class=\"navbar-brand text-lt\"><i class=\"fa fa-ticket\"></i> <span class=\"hidden-folded m-l-xs\">{{app.name}}</span></a><!-- / brand --></div><!-- / navbar header --><!-- navbar collapse --><div class=\"collapse pos-rlt navbar-collapse box-shadow {{app.settings.navbarCollapseColor}}\"><!-- buttons --><div class=\"nav navbar-nav hidden-xs\"><a href class=\"btn no-shadow navbar-btn\" ng-click=\"app.settings.asideFolded = !app.settings.asideFolded\"><i class=\"fa {{app.settings.asideFolded ? 'fa-indent' : 'fa-dedent'}} fa-fw\"></i></a></div><!-- / buttons --><!-- search form --><!-- / search form --><!-- nabar right --><ul class=\"nav navbar-nav navbar-right\"><li class=\"dropdown hidden-sm\" is-open=\"lang.isopen\" dropdown><a href class=\"dropdown-toggle\" dropdown-toggle>{{selectLang|translate}} <b class=\"caret\"></b></a><!-- dropdown --><ul class=\"dropdown-menu animated fadeInRight w\"><li ng-repeat=\"(langKey, label) in langs\"><a ng-click=\"setLang(langKey, $event)\" href>{{label|translate}}</a></li></ul><!-- / dropdown --></li><li class=\"dropdown\" dropdown><a href class=\"dropdown-toggle clear\" dropdown-toggle><span class=\"hidden-sm hidden-md\">{{ me.FullName }}</span> <b class=\"caret\"></b></a><!-- dropdown --><ul class=\"dropdown-menu animated fadeInRight w\"><li><a ui-sref=\"app.page.profile\" translate>Profile</a></li><li><a ui-sref=\"app.docs\" translate>Help</a></li><li class=\"divider\"></li><li><a href=\"\" ng-click=\"logOut()\" translate>Logout</a></li></ul><!-- / dropdown --></li></ul><!-- / navbar right --></div><!-- / navbar collapse -->"
  );


  $templateCache.put('tpl/blocks/nav.html',
    "<!-- list --><ul class=\"nav ng-scope\"><li class=\"hidden-folded padder m-t m-b-sm text-muted text-xs\"><span class=\"ng-scope\" translate>Voucher Management</span></li><li ui-sref-active=\"active\"><a href=\"\" ui-sref=\"app.dashboard\" class=\"auto\"><i class=\"glyphicon glyphicon-stats icon text-primary-dker\"></i> <span class=\"font-bold ng-scope\" translate>Dashboard</span></a></li><li ng-class=\"{active:$state.includes('vendors')}\"><a href=\"\" class=\"auto\"><span class=\"pull-right text-muted\"><i class=\"fa fa-fw fa-angle-right text\"></i> <i class=\"fa fa-fw fa-angle-down text-active\"></i></span> <i class=\"fa fa-building-o text-success-lter\"></i> <span translate>Vendors</span></a><ul class=\"nav nav-sub dk\"><li class=\"nav-sub-header\"><a href=\"\"><span translate>Vendors</span></a></li><li ui-sref-active=\"active\"><a ui-sref=\"vendors.list\" href=\"#\"><span translate>List</span></a></li><li ui-sref-active=\"active\"><a ui-sref=\"vendors.create\" href=\"#\"><span translate>Register</span></a></li></ul></li><li ng-class=\"{active:$state.includes('beneficiaries')}\"><a href=\"\" class=\"auto\"><span class=\"pull-right text-muted\"><i class=\"fa fa-fw fa-angle-right text\"></i> <i class=\"fa fa-fw fa-angle-down text-active\"></i></span> <i class=\"icon-user icon text-success-lter\"></i> <span translate>Beneficiaries</span></a><ul class=\"nav nav-sub dk\"><li class=\"nav-sub-header\"><a href=\"\"><span translate>Beneficiaries</span></a></li><li ui-sref-active=\"active\"><a ui-sref=\"beneficiaries.list\" href=\"#\"><span translate>List</span></a></li><li ui-sref-active=\"active\"><a ui-sref=\"beneficiaries.create\" href=\"#\"><span translate>Register</span></a></li><li ui-sref-active=\"active\"><a ui-sref=\"beneficiaries.bulkEdit\" href=\"#\"><span translate>Assign Group</span></a></li></ul></li><li ng-class=\"{active:$state.includes('groups')}\"><a href=\"\" class=\"auto\"><span class=\"pull-right text-muted\"><i class=\"fa fa-fw fa-angle-right text\"></i> <i class=\"fa fa-fw fa-angle-down text-active\"></i></span> <i class=\"icon-users icon text-success-lter\"></i> <span translate>Ben. Groups</span></a><ul class=\"nav nav-sub dk\"><li class=\"nav-sub-header\"><a href=\"\"><span translate>Beneficiary Groups</span></a></li><li ui-sref-active=\"active\"><a ui-sref=\"groups.list\" href=\"#\"><span translate>List</span></a></li><li ui-sref-active=\"active\"><a ui-sref=\"groups.create\" href=\"#\"><span translate>Create</span></a></li></ul></li><li ng-class=\"{active:$state.includes('distributions')}\"><a href=\"\" class=\"auto\"><span class=\"pull-right text-muted\"><i class=\"fa fa-fw fa-angle-right text\"></i> <i class=\"fa fa-fw fa-angle-down text-active\"></i></span> <i class=\"fa fa-truck text-primary-lter\"></i> <span translate>Distributions</span></a><ul class=\"nav nav-sub dk\"><li class=\"nav-sub-header\"><a href=\"\"><span translate>Distributions</span></a></li><li ui-sref-active=\"active\"><a ui-sref=\"distributions.list\" href=\"#\"><span translate>List</span></a></li><li ui-sref-active=\"active\"><a ui-sref=\"distributions.create\" href=\"#\"><span translate>Create</span></a></li></ul></li><li class=\"hidden-folded padder m-t m-b-sm text-muted text-xs\"><span class=\"ng-scope\" translate>Reporting</span></li><li ng-class=\"{active:$state.includes('reporting')}\"><a href=\"\" class=\"auto\"><span class=\"pull-right text-muted\"><i class=\"fa fa-fw fa-angle-right text\"></i> <i class=\"fa fa-fw fa-angle-down text-active\"></i></span> <i class=\"icon-bar-chart icon text-success-lter\"></i> <span translate>Reporting</span></a><ul class=\"nav nav-sub dk\"><li class=\"nav-sub-header\"><a href=\"\"><span translate>Reports</span></a></li><li ui-sref-active=\"active\"><a ui-sref=\"reporting.activeVouchers\" href=\"#\"><span translate>Active Vouchers</span></a></li><li ui-sref-active=\"active\"><a ui-sref=\"reporting.vendoreReceipt\" href=\"#\"><span translate>Vendor Receipt</span></a></li></ul></li><li class=\"hidden-folded padder m-t m-b-sm text-muted text-xs\"><span class=\"ng-scope\" translate>Administration</span></li><li ng-class=\"{active:$state.includes('admin.locations')}\"><a href=\"\" class=\"auto\"><span class=\"pull-right text-muted\"><i class=\"fa fa-fw fa-angle-right text\"></i> <i class=\"fa fa-fw fa-angle-down text-active\"></i></span> <i class=\"icon icon-pin text-info-lter\"></i> <span translate>Locations</span></a><ul class=\"nav nav-sub dk\"><li class=\"nav-sub-header\"><a href=\"\"><span translate>Locations</span></a></li><li ui-sref-active=\"active\"><a ui-sref=\"admin.locations.list\" href=\"#\"><span translate>List</span></a></li><li ui-sref-active=\"active\"><a ui-sref=\"admin.locations.create\" href=\"#\"><span translate>Create</span></a></li></ul></li><li ng-class=\"{active:$state.includes('admin.voucherTypes')}\"><a href=\"\" class=\"auto\"><span class=\"pull-right text-muted\"><i class=\"fa fa-fw fa-angle-right text\"></i> <i class=\"fa fa-fw fa-angle-down text-active\"></i></span> <i class=\"fa fa-tags text-success-lter\"></i> <span translate>Voucher Types</span></a><ul class=\"nav nav-sub dk\"><li class=\"nav-sub-header\"><a href=\"\"><span translate>Voucher Types</span></a></li><li ui-sref-active=\"active\"><a ui-sref=\"admin.voucherTypes.list\" href=\"#\"><span translate>List</span></a></li><li ui-sref-active=\"active\"><a ui-sref=\"admin.voucherTypes.create\" href=\"#\"><span translate>Create</span></a></li></ul></li></ul>"
  );


  $templateCache.put('tpl/blocks/page_footer.html',
    "<p><small class=\"text-muted\">The International Rescue Committee<br>&copy; 2015</small></p>"
  );


  $templateCache.put('tpl/blocks/settings.html',
    "<!-- settings --> <button class=\"btn btn-default no-shadow pos-abt\" ui-toggle-class=\"active\" target=\".settings\"><i class=\"fa fa-spin fa-gear\"></i></button><div class=\"panel-heading\">Settings</div><div class=\"panel-body\"><div class=\"m-b-sm\"><label class=\"i-switch bg-info pull-right\"><input type=\"checkbox\" ng-model=\"app.settings.headerFixed\"> <i></i></label>Fixed header</div><div class=\"m-b-sm\"><label class=\"i-switch bg-info pull-right\"><input type=\"checkbox\" ng-model=\"app.settings.asideFixed\"> <i></i></label>Fixed aside</div><div class=\"m-b-sm\"><label class=\"i-switch bg-info pull-right\"><input type=\"checkbox\" ng-model=\"app.settings.asideFolded\"> <i></i></label>Folded aside</div><div class=\"m-b-sm\"><label class=\"i-switch bg-info pull-right\"><input type=\"checkbox\" ng-model=\"app.settings.asideDock\"> <i></i></label>Dock aside</div><div><label class=\"i-switch bg-info pull-right\"><input type=\"checkbox\" ng-model=\"app.settings.container\"> <i></i></label>Boxed layout</div></div><div class=\"wrapper b-t b-light bg-light lter r-b\"><div class=\"row row-sm\"><div class=\"col-xs-6\"><label class=\"i-checks block m-b\" ng-click=\"\r" +
    "\n" +
    "          app.settings.navbarHeaderColor='bg-black'; \r" +
    "\n" +
    "          app.settings.navbarCollapseColor='bg-white-only'; \r" +
    "\n" +
    "          app.settings.asideColor='bg-black';\r" +
    "\n" +
    "         \"><input type=\"radio\" name=\"a\" ng-model=\"app.settings.themeID\" value=\"1\"> <span class=\"block bg-light clearfix pos-rlt\"><span class=\"active pos-abt w-full h-full bg-black-opacity text-center\"><i class=\"glyphicon glyphicon-ok text-white m-t-xs\"></i></span> <b class=\"bg-black header\"></b> <b class=\"bg-white header\"></b> <b class=\"bg-black\"></b></span></label><label class=\"i-checks block m-b\" ng-click=\"\r" +
    "\n" +
    "          app.settings.navbarHeaderColor='bg-dark'; \r" +
    "\n" +
    "          app.settings.navbarCollapseColor='bg-white-only'; \r" +
    "\n" +
    "          app.settings.asideColor='bg-dark';\r" +
    "\n" +
    "         \"><input type=\"radio\" name=\"a\" ng-model=\"app.settings.themeID\" value=\"13\"> <span class=\"block bg-light clearfix pos-rlt\"><span class=\"active pos-abt w-full h-full bg-black-opacity text-center\"><i class=\"glyphicon glyphicon-ok text-white m-t-xs\"></i></span> <b class=\"bg-dark header\"></b> <b class=\"bg-white header\"></b> <b class=\"bg-dark\"></b></span></label><label class=\"i-checks block m-b\" ng-click=\"\r" +
    "\n" +
    "          app.settings.navbarHeaderColor='bg-white-only'; \r" +
    "\n" +
    "          app.settings.navbarCollapseColor='bg-white-only'; \r" +
    "\n" +
    "          app.settings.asideColor='bg-black';\r" +
    "\n" +
    "         \"><input type=\"radio\" ng-model=\"app.settings.themeID\" value=\"2\"> <span class=\"block bg-light clearfix pos-rlt\"><span class=\"active pos-abt w-full h-full bg-black-opacity text-center\"><i class=\"glyphicon glyphicon-ok text-white m-t-xs\"></i></span> <b class=\"bg-white header\"></b> <b class=\"bg-white header\"></b> <b class=\"bg-black\"></b></span></label><label class=\"i-checks block m-b\" ng-click=\"\r" +
    "\n" +
    "          app.settings.navbarHeaderColor='bg-primary'; \r" +
    "\n" +
    "          app.settings.navbarCollapseColor='bg-white-only'; \r" +
    "\n" +
    "          app.settings.asideColor='bg-dark';\r" +
    "\n" +
    "         \"><input type=\"radio\" ng-model=\"app.settings.themeID\" value=\"3\"> <span class=\"block bg-light clearfix pos-rlt\"><span class=\"active pos-abt w-full h-full bg-black-opacity text-center\"><i class=\"glyphicon glyphicon-ok text-white m-t-xs\"></i></span> <b class=\"bg-primary header\"></b> <b class=\"bg-white header\"></b> <b class=\"bg-dark\"></b></span></label><label class=\"i-checks block m-b\" ng-click=\"\r" +
    "\n" +
    "          app.settings.navbarHeaderColor='bg-info'; \r" +
    "\n" +
    "          app.settings.navbarCollapseColor='bg-white-only'; \r" +
    "\n" +
    "          app.settings.asideColor='bg-black';\r" +
    "\n" +
    "         \"><input type=\"radio\" ng-model=\"app.settings.themeID\" value=\"4\"> <span class=\"block bg-light clearfix pos-rlt\"><span class=\"active pos-abt w-full h-full bg-black-opacity text-center\"><i class=\"glyphicon glyphicon-ok text-white m-t-xs\"></i></span> <b class=\"bg-info header\"></b> <b class=\"bg-white header\"></b> <b class=\"bg-black\"></b></span></label><label class=\"i-checks block m-b\" ng-click=\"\r" +
    "\n" +
    "          app.settings.navbarHeaderColor='bg-success'; \r" +
    "\n" +
    "          app.settings.navbarCollapseColor='bg-white-only'; \r" +
    "\n" +
    "          app.settings.asideColor='bg-dark';\r" +
    "\n" +
    "         \"><input type=\"radio\" ng-model=\"app.settings.themeID\" value=\"5\"> <span class=\"block bg-light clearfix pos-rlt\"><span class=\"active pos-abt w-full h-full bg-black-opacity text-center\"><i class=\"glyphicon glyphicon-ok text-white m-t-xs\"></i></span> <b class=\"bg-success header\"></b> <b class=\"bg-white header\"></b> <b class=\"bg-dark\"></b></span></label><label class=\"i-checks block\" ng-click=\"\r" +
    "\n" +
    "          app.settings.navbarHeaderColor='bg-danger'; \r" +
    "\n" +
    "          app.settings.navbarCollapseColor='bg-white-only'; \r" +
    "\n" +
    "          app.settings.asideColor='bg-dark';\r" +
    "\n" +
    "         \"><input type=\"radio\" ng-model=\"app.settings.themeID\" value=\"6\"> <span class=\"block bg-light clearfix pos-rlt\"><span class=\"active pos-abt w-full h-full bg-black-opacity text-center\"><i class=\"glyphicon glyphicon-ok text-white m-t-xs\"></i></span> <b class=\"bg-danger header\"></b> <b class=\"bg-white header\"></b> <b class=\"bg-dark\"></b></span></label></div><div class=\"col-xs-6\"><label class=\"i-checks block m-b\" ng-click=\"\r" +
    "\n" +
    "          app.settings.navbarHeaderColor='bg-black'; \r" +
    "\n" +
    "          app.settings.navbarCollapseColor='bg-black'; \r" +
    "\n" +
    "          app.settings.asideColor='bg-white b-r';\r" +
    "\n" +
    "         \"><input type=\"radio\" ng-model=\"app.settings.themeID\" value=\"7\"> <span class=\"block bg-light clearfix pos-rlt\"><span class=\"active pos-abt w-full h-full bg-black-opacity text-center\"><i class=\"glyphicon glyphicon-ok text-white m-t-xs\"></i></span> <b class=\"bg-black header\"></b> <b class=\"bg-black header\"></b> <b class=\"bg-white\"></b></span></label><label class=\"i-checks block m-b\" ng-click=\"\r" +
    "\n" +
    "          app.settings.navbarHeaderColor='bg-dark'; \r" +
    "\n" +
    "          app.settings.navbarCollapseColor='bg-dark'; \r" +
    "\n" +
    "          app.settings.asideColor='bg-light';\r" +
    "\n" +
    "         \"><input type=\"radio\" name=\"a\" ng-model=\"app.settings.themeID\" value=\"14\"> <span class=\"block bg-light clearfix pos-rlt\"><span class=\"active pos-abt w-full h-full bg-black-opacity text-center\"><i class=\"glyphicon glyphicon-ok text-white m-t-xs\"></i></span> <b class=\"bg-dark header\"></b> <b class=\"bg-dark header\"></b> <b class=\"bg-light\"></b></span></label><label class=\"i-checks block m-b\" ng-click=\"\r" +
    "\n" +
    "          app.settings.navbarHeaderColor='bg-info dker'; \r" +
    "\n" +
    "          app.settings.navbarCollapseColor='bg-info dker'; \r" +
    "\n" +
    "          app.settings.asideColor='bg-light dker b-r';\r" +
    "\n" +
    "         \"><input type=\"radio\" ng-model=\"app.settings.themeID\" value=\"8\"> <span class=\"block bg-light clearfix pos-rlt\"><span class=\"active pos-abt w-full h-full bg-black-opacity text-center\"><i class=\"glyphicon glyphicon-ok text-white m-t-xs\"></i></span> <b class=\"bg-info dker header\"></b> <b class=\"bg-info dker header\"></b> <b class=\"bg-light dker\"></b></span></label><label class=\"i-checks block m-b\" ng-click=\"\r" +
    "\n" +
    "          app.settings.navbarHeaderColor='bg-primary'; \r" +
    "\n" +
    "          app.settings.navbarCollapseColor='bg-primary'; \r" +
    "\n" +
    "          app.settings.asideColor='bg-dark';\r" +
    "\n" +
    "         \"><input type=\"radio\" ng-model=\"app.settings.themeID\" value=\"9\"> <span class=\"block bg-light clearfix pos-rlt\"><span class=\"active pos-abt w-full h-full bg-black-opacity text-center\"><i class=\"glyphicon glyphicon-ok text-white m-t-xs\"></i></span> <b class=\"bg-primary header\"></b> <b class=\"bg-primary header\"></b> <b class=\"bg-dark\"></b></span></label><label class=\"i-checks block m-b\" ng-click=\"\r" +
    "\n" +
    "          app.settings.navbarHeaderColor='bg-info dker'; \r" +
    "\n" +
    "          app.settings.navbarCollapseColor='bg-info dk'; \r" +
    "\n" +
    "          app.settings.asideColor='bg-black';\r" +
    "\n" +
    "         \"><input type=\"radio\" ng-model=\"app.settings.themeID\" value=\"10\"> <span class=\"block bg-light clearfix pos-rlt\"><span class=\"active pos-abt w-full h-full bg-black-opacity text-center\"><i class=\"glyphicon glyphicon-ok text-white m-t-xs\"></i></span> <b class=\"bg-info dker header\"></b> <b class=\"bg-info dk header\"></b> <b class=\"bg-black\"></b></span></label><label class=\"i-checks block m-b\" ng-click=\"\r" +
    "\n" +
    "          app.settings.navbarHeaderColor='bg-success'; \r" +
    "\n" +
    "          app.settings.navbarCollapseColor='bg-success';\r" +
    "\n" +
    "          app.settings.asideColor='bg-dark';\r" +
    "\n" +
    "          \"><input type=\"radio\" ng-model=\"app.settings.themeID\" value=\"11\"> <span class=\"block bg-light clearfix pos-rlt\"><span class=\"active pos-abt w-full h-full bg-black-opacity text-center\"><i class=\"glyphicon glyphicon-ok text-white m-t-xs\"></i></span> <b class=\"bg-success header\"></b> <b class=\"bg-success header\"></b> <b class=\"bg-dark\"></b></span></label><label class=\"i-checks block\" ng-click=\"\r" +
    "\n" +
    "          app.settings.navbarHeaderColor='bg-danger dker bg-gd'; \r" +
    "\n" +
    "          app.settings.navbarCollapseColor='bg-danger dker bg-gd'; \r" +
    "\n" +
    "          app.settings.asideColor='bg-dark';\r" +
    "\n" +
    "         \"><input type=\"radio\" ng-model=\"app.settings.themeID\" value=\"12\"> <span class=\"block bg-light clearfix pos-rlt\"><span class=\"active pos-abt w-full h-full bg-black-opacity text-center\"><i class=\"glyphicon glyphicon-ok text-white m-t-xs\"></i></span> <b class=\"bg-danger dker header\"></b> <b class=\"bg-danger dker header\"></b> <b class=\"bg-dark\"></b></span></label></div></div></div><!-- /settings -->"
  );


  $templateCache.put('tpl/dialogs/assignToGroup.html',
    "<div class=\"modal-header\"><h4 class=\"modal-title\">Assign group to distribution</h4></div><div class=\"modal-body\"><form class=\"form-horizontal\" ng-submit=\"done()\"><div class=\"form-group\"><label class=\"col-lg-2 control-label\">Group</label><div class=\"col-lg-10\"><input type=\"text\" ng-model=\"group\" typeahead=\"item as item.Name for item in getGroup($viewValue) | limitTo:8\" class=\"form-control\"></div></div></form></div><div class=\"modal-footer\"><button class=\"btn btn-default\" ng-click=\"done()\">Done</button></div>"
  );


  $templateCache.put('tpl/distributions/create.html',
    "<div class=\"bg-light lter b-b wrapper-md\"><h1 class=\"m-n font-thin h3\">Distribution Information - <small>{{ entity.Title }}</small></h1></div><div class=\"wrapper-md\"><form class=\"bs-example form-horizontal form-validation\" ng-submit=\"save()\"><div class=\"row\"><div class=\"col-sm-12\" ng-include=\"'tpl/includes/distribution_form.html'\" toggle-editing is-editing=\"isEditing\"></div></div><div class=\"row\"><div class=\"col-sm-12\"><div class=\"form-group\"><div class=\"col-lg-12\"><div class=\"btn-group pull-right\"><button type=\"submit\" class=\"btn btn-primary\">Save</button> <button ui-sref=\"app.vouchers.distributions\" class=\"btn btn-info\">Back</button></div></div></div></div></div></form></div>"
  );


  $templateCache.put('tpl/distributions/edit.html',
    "<div class=\"bg-light lter b-b wrapper-md\"><h1 class=\"m-n font-thin h3\">Distribution Information - <small>{{ entity.Title }}</small></h1></div><div class=\"wrapper-md\"><form class=\"bs-example form-horizontal form-validation\" ng-submit=\"save()\"><div class=\"row\"><div class=\"col-sm-12\" ng-include=\"'tpl/includes/distribution_form.html'\" toggle-editing is-editing=\"isEditing\"></div></div><div class=\"row\"><div class=\"col-sm-12\"><h1 class=\"m-n font-thin h4\">Generated Vouchers</h1></div></div><div class=\"row\"><div class=\"col-sm-12\"><div class=\"panel panel-default\"><div class=\"table-responsive\"><div ng-grid=\"gridOptions\" style=\"min-height:300px\"></div></div></div></div></div><div class=\"row\"><div class=\"col-sm-12\" ng-if=\"!isEditing\"><div class=\"form-group\"><div class=\"col-lg-6\"><div class=\"btn-group\"><a href ng-click=\"delete()\" class=\"btn btn-sm btn-danger\">Delete</a></div><a href ng-click=\"generateVouchers()\" class=\"btn btn-sm btn-success\">Generate Vouchers</a> <a href ng-click=\"assignToGroup()\" class=\"btn btn-sm btn-success\">Assign Vouchers to Group</a></div><div class=\"col-lg-6\"><div class=\"btn-group pull-right\" ng-if=\"!entity.Disabled\"><button ng-click=\"startEditing()\" class=\"btn btn-primary\">Edit</button> <button ui-sref=\"distributions.list\" class=\"btn btn-info\">Back</button></div></div></div></div></div><div class=\"row\"><div class=\"col-sm-12\" ng-if=\"isEditing\"><div class=\"form-group\"><div class=\"col-lg-12\"><div class=\"btn-group pull-right\"><button type=\"submit\" class=\"btn btn-primary\">Save</button> <button ng-click=\"save(true)\" class=\"btn btn-primary\">Save and Continue Editing</button> <button ng-click=\"endEditing()\" class=\"btn btn-info\">Back</button></div></div></div></div></div></form></div>"
  );


  $templateCache.put('tpl/distributions/list.html',
    "<div class=\"bg-light lter b-b wrapper-md\"><h1 class=\"m-n font-thin h3\">Distributions</h1></div><div class=\"vbox\"><div class=\"col\"><div class=\"wrapper bg-light lter b-b\"><div class=\"btn-toolbar\"><div class=\"btn-group dropdown\"><button class=\"btn btn-default btn-sm btn-bg dropdown-toggle\" data-toggle=\"dropdown\"><span class=\"dropdown-label\">Filter</span> <span class=\"caret\"></span></button><ul class=\"dropdown-menu text-left text-sm\"></ul></div><div class=\"btn-group\"><button class=\"btn btn-sm btn-bg btn-default\" data-toggle=\"tooltip\" data-placement=\"bottom\" ng-click=\"loadGridData()\" title=\"Refresh\"><i class=\"fa fa-refresh\"></i></button></div><button class=\"btn btn-success pull-right\" ui-sref=\"distributions.create\">New Distribution</button></div></div></div><div class=\"wrapper-md col\"><div class=\"panel panel-default\"><div class=\"table-responsive\"><div ng-grid=\"gridOptions\" style=\"min-height:620px\"></div></div></div></div></div>"
  );


  $templateCache.put('tpl/generic/create.html',
    "<div class=\"bg-light lter b-b wrapper-md\"><h1 class=\"m-n font-thin h3\">{{ genericSettings.title }} - <small>{{ entity.Name }}</small></h1></div><div class=\"wrapper-md\" toggle-editing is-editing=\"isEditing\"><form class=\"bs-example form-horizontal form-validation\" ng-submit=\"save()\"><div class=\"row\"><div class=\"col-sm-12\" ng-include=\"genericSettings.formTemplate\"></div></div><div class=\"row\"><div class=\"col-sm-12\"><div class=\"form-group\"><div class=\"col-lg-12\"><div class=\"btn-group pull-right\"><button type=\"submit\" class=\"btn btn-primary\" translate>Save</button> <button ui-sref=\"{{ genericSettings.listState }}\" class=\"btn btn-info\" translate>Back</button></div></div></div></div></div></form></div>"
  );


  $templateCache.put('tpl/generic/edit.html',
    "<div class=\"bg-light lter b-b wrapper-md\"><h1 class=\"m-n font-thin h3\">{{ genericSettings.title }} - <small>{{ entity.Name }}</small></h1></div><div class=\"wrapper-md\"><form class=\"bs-example form-horizontal form-validation\" ng-submit=\"save()\"><div class=\"row\"><div class=\"col-sm-12\" ng-include=\"genericSettings.formTemplate\" toggle-editing is-editing=\"isEditing\"></div></div><div class=\"row\"><div class=\"col-sm-12\" ng-if=\"!isEditing\"><div class=\"form-group\"><div class=\"col-lg-6\"><div class=\"btn-group\"><a href ng-click=\"delete()\" class=\"btn btn-sm btn-danger\" translate>Delete</a></div></div><div class=\"col-lg-6\"><div class=\"btn-group pull-right\" ng-if=\"!entity.Disabled\"><button ng-click=\"startEditing()\" class=\"btn btn-primary\" translate>Edit</button> <button ui-sref=\"{{ genericSettings.listState }}\" class=\"btn btn-info\" translate>Back</button></div></div></div></div></div><div class=\"row\"><div class=\"col-sm-12\" ng-if=\"isEditing\"><div class=\"form-group\"><div class=\"col-lg-12\"><div class=\"btn-group pull-right\"><button type=\"submit\" class=\"btn btn-primary\" translate>Save</button> <a ng-click=\"save(true)\" class=\"btn btn-primary\" translate>Save and Continue Editing</a> <a ng-click=\"endEditing()\" class=\"btn btn-info\" translate>Back</a></div></div></div></div></div></form></div>"
  );


  $templateCache.put('tpl/generic/list.html',
    "<div class=\"bg-light lter b-b wrapper-md\"><h1 class=\"m-n font-thin h3\" ng-pluralize=\"\">{{ genericSettings.title }}</h1></div><div class=\"vbox\"><div class=\"col\"><div class=\"wrapper bg-light lter b-b\"><div class=\"btn-toolbar\"><div class=\"btn-group\"><button class=\"btn btn-sm btn-bg btn-default\" data-toggle=\"tooltip\" data-placement=\"bottom\" ng-click=\"loadGridData()\" title=\"Refresh\"><i class=\"fa fa-refresh\"></i></button></div><button class=\"btn btn-success pull-right\" ui-sref=\"{{ genericSettings.createState}}\" translate>New</button></div></div></div><div class=\"wrapper-md col\"><div class=\"panel panel-default\"><div class=\"table-responsive\"><div ng-grid=\"gridOptions\" style=\"min-height:620px\"></div></div></div></div></div>"
  );


  $templateCache.put('tpl/groups/form.html',
    "<div class=\"panel panel-default\"><div class=\"panel-body\"><div class=\"form-group\"><label class=\"col-lg-2 control-label\">Name</label><div class=\"col-lg-10\"><input type=\"text\" class=\"form-control\" placeholder=\"Name\" ng-model=\"entity.Name\" required></div></div></div></div>"
  );


  $templateCache.put('tpl/includes/distribution_form.html',
    "<div class=\"panel panel-default\"><div class=\"panel-heading font-bold\">Basic Information</div><div class=\"panel-body\"><div class=\"form-group\"><label class=\"col-lg-2 control-label\">Title</label><div class=\"col-lg-10\"><input type=\"text\" class=\"form-control\" placeholder=\"Title\" ng-model=\"entity.Title\" required></div></div><div class=\"line line-dashed b-b line-lg pull-in\"></div><div class=\"form-group\"><label class=\"col-sm-2 control-label\">Location</label><div class=\"col-sm-4\"><select chosen class=\"form-control\" ng-model=\"entity.LocationId\" ng-options=\"item.Id as item.Name for item in locations | orderBy:'item.Name'\"></select></div></div><div class=\"line line-dashed b-b line-lg pull-in\"></div><div class=\"form-group\"><label class=\"col-lg-2 control-label\">Number of digits in voucher</label><div class=\"col-lg-4\"><input type=\"text\" class=\"form-control\" placeholder=\"Value\" ng-model=\"entity.VoucherCodeLength\"></div></div><div class=\"line line-dashed b-b line-lg pull-in\"></div><div class=\"form-group\"><label class=\"col-lg-12\">Authorized Vendors <small>(leave empty for all vendors)</small></label><div class=\"col-lg-12\"><span ng-repeat=\"vendor in vendors\"><label><input type=\"checkbox\" name=\"assignedVendors[]\" value=\"{{vendor.Id}}\" ng-checked=\"assignedVendors.indexOf(vendor.Id) > -1\" ng-click=\"toggleVendor(vendor)\">&nbsp;&nbsp;{{vendor.Name}}</label><br></span></div></div></div></div><div class=\"panel panel-default\" ng-repeat=\"category in categories\" toggle-editing is-editing=\"isEditing\"><div class=\"panel-body\"><div class=\"form-group\"><label class=\"col-sm-2 control-label\">Type of Voucher</label><div class=\"col-sm-4\"><select chosen class=\"form-control\" ng-model=\"category.TypeId\" ng-options=\"item.Id as item.Name for item in voucherTypes | orderBy:'item.Name'\"></select></div></div><div class=\"line line-dashed b-b line-lg pull-in\"></div><div class=\"form-group\"><label class=\"col-lg-2 control-label\">Total Number of Vouchers</label><div class=\"col-lg-4\"><input type=\"text\" class=\"form-control\" placeholder=\"NumberOfVouchers\" ng-model=\"category.NumberOfVouchers\" ui-validate=\"'$value >= category.IssuedVouchers'\" ui-validate-watch=\"category.IssuedVouchers\" required></div><label class=\"col-lg-2 control-label\">Vouchers Issued</label><div class=\"col-lg-4\"><input type=\"text\" class=\"form-control\" placeholder=\"IssuedVouchers\" ng-model=\"category.IssuedVouchers\" disabled></div></div><div class=\"line line-dashed b-b line-lg pull-in\"></div><div class=\"form-group\"><label class=\"col-lg-2 control-label\">Value of Vouchers</label><div class=\"col-lg-4\"><input type=\"text\" class=\"form-control\" placeholder=\"Value\" ng-model=\"category.Value\" required></div></div><div class=\"line line-dashed b-b line-lg pull-in\" ng-if=\"isEditing\"></div><a href=\"#\" ng-click=\"removeCategory(category)\" class=\"btn btn-danger\" ng-if=\"isEditing\">Remove</a></div></div><a href=\"#\" ng-click=\"addCategory()\" class=\"btn btn-info btn-dark pull-right\" ng-if=\"isEditing && !isNew\">Add</a>"
  );


  $templateCache.put('tpl/includes/vendors_form.html',
    "<div class=\"panel panel-default\"><div class=\"panel-heading font-bold\">Basic Information</div><div class=\"panel-body\"><div class=\"form-group\"><label class=\"col-lg-2 control-label\">Name</label><div class=\"col-lg-4\"><input type=\"text\" class=\"form-control\" placeholder=\"Name\" ng-model=\"entity.Name\" required></div><label class=\"col-lg-2 control-label\">Mobile Number</label><div class=\"col-lg-4\"><div class=\"input-group m-b\"><span class=\"input-group-addon\">+</span> <input type=\"text\" class=\"form-control\" placeholder=\"Mobile Number\" ng-model=\"entity.MobileNumber\" ng-pattern=\"/[0-9]$/\" required></div></div></div><div class=\"line line-dashed b-b line-lg pull-in\"></div><div class=\"form-group\"><label class=\"col-lg-2 control-label\">Owner Name</label><div class=\"col-lg-4\"><input type=\"text\" class=\"form-control\" placeholder=\"Name\" ng-model=\"entity.OwnerName\" required></div></div><div class=\"line line-dashed b-b line-lg pull-in\"></div><div class=\"form-group\"><label class=\"col-sm-2 control-label\">Location</label><div class=\"col-sm-10\"><select chosen class=\"form-control\" ng-model=\"entity.LocationId\" ng-options=\"item.Id as item.Name for item in locations | orderBy:'item.Name'\"></select></div></div></div></div>"
  );


  $templateCache.put('tpl/page_signin.html',
    "<div class=\"container w-xxl w-auto-xs\" ng-controller=\"SigninFormController\" ng-init=\"app.settings.container = false;\"><a href class=\"navbar-brand block m-t\">{{app.fullName}}</a><div class=\"m-b-lg\"><form name=\"form\" class=\"form-validation\"><div class=\"text-danger wrapper text-center\" ng-show=\"authError\">{{authError}}</div><div class=\"list-group list-group-sm\"><div class=\"list-group-item\"><input type=\"text\" placeholder=\"User Name\" class=\"form-control no-border\" ng-model=\"user.userName\" required></div><div class=\"list-group-item\"><input type=\"password\" placeholder=\"Password\" class=\"form-control no-border\" ng-model=\"user.password\" required></div></div><button type=\"submit\" class=\"btn btn-lg btn-primary btn-block\" ng-click=\"login()\" ng-disabled=\"form.$invalid\">Log in</button></form></div><div class=\"text-center\" ng-include=\"'tpl/blocks/page_footer.html'\">{% include 'blocks/page_footer.html' %}</div></div>"
  );


  $templateCache.put('tpl/vendors/create.html',
    "<div class=\"bg-light lter b-b wrapper-md\"><h1 class=\"m-n font-thin h3\">Vendor Information - <small>{{ entity.Name }}</small></h1></div><div class=\"wrapper-md\" toggle-editing is-editing=\"isEditing\"><form class=\"bs-example form-horizontal form-validation\" ng-submit=\"save()\"><div class=\"row\"><div class=\"col-sm-12\" ng-include=\"'tpl/includes/vendors_form.html'\"></div></div><div class=\"row\"><div class=\"col-sm-12\"><div class=\"form-group\"><div class=\"col-lg-12\"><div class=\"btn-group pull-right\"><button type=\"submit\" class=\"btn btn-primary\">Save</button> <button ui-sref=\"vendors.list\" class=\"btn btn-info\">Back</button></div></div></div></div></div></form></div>"
  );


  $templateCache.put('tpl/vendors/edit.html',
    "<div class=\"bg-light lter b-b wrapper-md\"><h1 class=\"m-n font-thin h3\">Vendor Information - <small>{{ entity.Name }}</small></h1></div><div class=\"wrapper-md\"><form class=\"bs-example form-horizontal form-validation\" ng-submit=\"save()\"><div class=\"row\"><div class=\"col-sm-12\" ng-include=\"'tpl/includes/vendors_form.html'\" toggle-editing is-editing=\"isEditing\"></div></div><div class=\"row\"><div class=\"col-sm-12\" ng-if=\"!isEditing\"><div class=\"form-group\"><div class=\"col-lg-6\"><div class=\"btn-group\"><a href ng-click=\"delete()\" class=\"btn btn-sm btn-danger\">Delete</a></div></div><div class=\"col-lg-6\"><div class=\"btn-group pull-right\" ng-if=\"!entity.Disabled\"><button ng-click=\"startEditing()\" class=\"btn btn-primary\">Edit</button> <button ui-sref=\"vendors.list\" class=\"btn btn-info\">Back</button></div></div></div></div></div><div class=\"row\"><div class=\"col-sm-12\" ng-if=\"isEditing\"><div class=\"form-group\"><div class=\"col-lg-12\"><div class=\"btn-group pull-right\"><button type=\"submit\" class=\"btn btn-primary\">Save</button> <button ng-click=\"save(true)\" class=\"btn btn-primary\">Save and Continue Editing</button> <button ng-click=\"endEditing()\" class=\"btn btn-info\">Back</button></div></div></div></div></div></form></div>"
  );


  $templateCache.put('tpl/vendors/list.html',
    "<div class=\"bg-light lter b-b wrapper-md\"><h1 class=\"m-n font-thin h3\">Vendors</h1></div><div class=\"vbox\"><div class=\"col\"><div class=\"wrapper bg-light lter b-b\"><div class=\"btn-toolbar\"><div class=\"btn-group dropdown\"><button class=\"btn btn-default btn-sm btn-bg dropdown-toggle\" data-toggle=\"dropdown\"><span class=\"dropdown-label\">Filter</span> <span class=\"caret\"></span></button><ul class=\"dropdown-menu text-left text-sm\"></ul></div><div class=\"btn-group\"><button class=\"btn btn-sm btn-bg btn-default\" data-toggle=\"tooltip\" data-placement=\"bottom\" ng-click=\"loadGridData()\" title=\"Refresh\"><i class=\"fa fa-refresh\"></i></button></div></div></div></div><div class=\"wrapper-md col\"><div class=\"panel panel-default\"><div class=\"table-responsive\"><div ng-grid=\"gridOptions\" style=\"min-height:620px\"></div></div></div></div></div>"
  );

}]);
