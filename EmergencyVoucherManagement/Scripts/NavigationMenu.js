(function () {
    if (window) {
        var navigationMenu = [
           {
               translate: "aside.nav.DASHBOARD",
               items: [
                { sref: "app.dashboard", translate: "aside.nav.DASHBOARD", visibleTo: [], iconClass: "glyphicon glyphicon-stats icon text-primary-dker" }
               ]
           },
           {
               translate: "aside.nav.BENEFICIARIES",
               items: [
                   { sref: "beneficiaries.create", translate: "aside.nav.REGISTER", visibleTo: ["Administrator"], iconClass: "fa fa-pencil-square-o icon" },
                   { sref: "beneficiaries.list", translate: "aside.nav.LIST", visibleTo: ["Administrator"], iconClass: "glyphicon glyphicon-list icon" }
               ]
           },
           {
               translate: "aside.nav.VOUCHERS",
               items: [
                   { sref: "app.vouchers.generate", translate: "aside.nav.voucher.GENERATE", visibleTo: ["Administrator"], iconClass: "fa fa-pencil-square-o icon" },
                   { sref: "app.vouchers.assign", translate: "aside.nav.voucher.ASSIGN", visibleTo: ["Administrator"], iconClass: "fa fa-pencil-square-o icon" },
                   { sref: "app.vouchers.distributions", translate: "aside.nav.voucher.DISTRIBUTIONS", visibleTo: ["Administrator"], iconClass: "glyphicon glyphicon-list icon" },
                   { sref: "vouchers.list", translate: "aside.nav.LIST", visibleTo: ["Administrator"], iconClass: "glyphicon glyphicon-list icon" }
               ]
           },
           {
               translate: "aside.nav.VENDORS",
               items: [
                   { sref: "app.vendors.register", translate: "aside.nav.REGISTER", visibleTo: ["Administrator"], iconClass: "fa fa-pencil-square-o icon" },
                   { sref: "app.vendors.list", translate: "aside.nav.LIST", visibleTo: ["Administrator"], iconClass: "glyphicon glyphicon-list icon" },
                   { sref: "app.vendors.processVoucher", translate: "aside.nav.PROCESS_VOUCHER", visibleTo: ["Vendor"], iconClass: "glyphicon glyphicon-list icon" }
               ]
           },
           {
               translate: "aside.nav.ADMINISTRATION",
               items: [
                   { sref: "app.admin.users", translate: "aside.nav.USERS", visibleTo: ["Administrator"], iconClass: "fa fa-user icon" },
                   { sref: "app.admin.roles", translate: "aside.nav.ROLES", visibleTo: ["Administrator"], iconClass: "fa fa-users icon" }
                   ]
           },
           {
               translate: "aside.nav.CONTROLLED_LISTS",
                items: [
                    { sref: "app.admin.controlledLists.locations", translate: "aside.nav.controlledLists.LOCATIONS", visibleTo: ["Administrator"], iconClass: "glyphicon glyphicon-list icon" },
                { sref: "app.admin.controlledLists.locations", translate: "aside.nav.controlledLists.VOUCHER_TYPES", visibleTo: ["Administrator"], iconClass: "glyphicon glyphicon-list icon" }
                ]
            }
        ];

        window.NavigationMenu = function (userInfo) {
            var filteredGroups = navigationMenu.filter(function (g) {
                return g.items.filter(function (i) {
                    return i.visibleTo.length == 0 || i.visibleTo.filter(function (v) { return userInfo.Roles.indexOf(v) > -1; }).length > 0;
                }).length > 0;
            });

            return filteredGroups.map(function (g) {
                g.items = g.items.filter(function (i) {
                    return i.visibleTo.length == 0 || i.visibleTo.filter(function (v) { return userInfo.Roles.indexOf(v) > -1; }).length > 0;
                });

                return g;
            });
        };
    }
})();