using System.Web;
using System.Web.Optimization;

namespace TalonAdmin
{
    public class BundleConfig
    {
        // For more information on bundling, visit http://go.microsoft.com/fwlink/?LinkId=301862
        public static void RegisterBundles(BundleCollection bundles)
        {
            bundles.Add(new ScriptBundle("~/bundles/jquery").Include(
                        "~/Scripts/jquery-{version}.js"));

            // Use the development version of Modernizr to develop with and learn from. Then, when you're
            // ready for production, use the build tool at http://modernizr.com to pick only the tests you need.
            bundles.Add(new ScriptBundle("~/bundles/modernizr").Include(
                        "~/Scripts/modernizr-*"));

            bundles.Add(new ScriptBundle("~/bundles/bootstrap").Include(
                      "~/Scripts/bootstrap.js",
                      "~/Scripts/respond.js"));

            bundles.Add(new StyleBundle("~/Content/css").Include(
                      "~/Content/bootstrap.css",
                      "~/Content/site.css"));

            bundles.Add(new StyleBundle("~/Content/NonAngularCss").Include(
                 "~/app/bower/nestable/jquery.nestable.css",
                 "~/app/bower/bootstrap-slider/slider.css",
                 "~/app/bower/plugins/integration/bootstrap/3/dataTables.bootstrap.css",
                 "~/app/bower/bower-jvectormap/jquery-jvectormap-1.2.2.css",
                 "~/app/bower/footable/css/footable.core.css",
                 "~/app/bower/fullcalendar/dist/fullcalendar.css",
                 "~/app/bower/fullcalendar/dist/fullcalendar.theme.css",
                 "~/app/bower/bootstrap-daterangepicker/daterangepicker-bs3.css",
                 "~/app/bower/bootstrap-tagsinput/dist/bootstrap-tagsinput.css",
                 "~/app/bower/bootstrap-touchspin/dist/jquery.bootstrap-touchspin.min.css"));


            bundles.Add(new ScriptBundle("~/Scripts/NonAngularJs").Include(
                 "~/app/bower/jquery.easy-pie-chart/dist/jquery.easypiechart.min.js",
                 "~/app/lib/jquery.sparkline.retina.js",
                 "~/app/bower/flot/jquery.flot.js",
                 "~/app/bower/flot/jquery.flot.pie.js",
                 "~/app/bower/flot/jquery.flot.resize.js",
                 "~/app/bower/flot.tooltip/js/jquery.flot.tooltip.js",
                 "~/app/bower/flot.orderbars/js/jquery.flot.orderBars.js",
                 "~/app/lib/jquery.flot.spline.min.js",
                 "~/app/bower/screenfull/dist/screenfull.min.js",
                 "~/app/bower/slimscroll/jquery.slimscroll.min.js",
                 "~/app/bower/html5sortable/jquery.sortable.js",
                 "~/app/bower/nestable/jquery.nestable.js",
                 "~/app/bower/bootstrap-filestyle/src/bootstrap-filestyle.js",
                 "~/app/bower/bootstrap-slider/bootstrap-slider.js",
                 "~/app/bower/chosen/chosen.jquery.min.js",
                 "~/app/bower/bootstrap-touchspin/dist/jquery.bootstrap-touchspin.min.js",
                 "~/app/bower/bootstrap-wysiwyg/bootstrap-wysiwyg.js",
                 "~/app/bower/bootstrap-wysiwyg/external/jquery.hotkeys.js",
                 "~/app/bower/datatables/media/js/jquery.dataTables.min.js",
                 "~/app/bower/plugins/integration/bootstrap/3/dataTables.bootstrap.js",
                 "~/app/bower/bower-jvectormap/jquery-jvectormap-1.2.2.min.js",
                 "~/app/bower/bower-jvectormap/jquery-jvectormap-world-mill-en.js",
                 "~/app/bower/bower-jvectormap/jquery-jvectormap-us-aea-en.js",
                 "~/app/bower/footable/dist/footable.all.min.js",
                 "~/app/bower/fullcalendar/dist/fullcalendar.min.js",
                 "~/app/bower/bootstrap-daterangepicker/daterangepicker.js",
                 "~/app/bower/bootstrap-tagsinput/dist/bootstrap-tagsinput.js"
                ));
        }
    }
}
