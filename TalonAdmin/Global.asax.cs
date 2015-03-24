using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Http;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using TuesPechkin;

namespace TalonAdmin
{
    public class WebApiApplication : System.Web.HttpApplication
    {
        public static IConverter converter = null;
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            GlobalConfiguration.Configure(config =>
            {
                WebApiConfig.Register(config);
            });

            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);

            using (var db = new Models.Vouchers.Context())
            {
                db.Database.Initialize(true);
            }

            WebApiApplication.converter = new ThreadSafeConverter(new RemotingToolset<PdfToolset>(new Win64EmbeddedDeployment(new TempFolderDeployment())));
        }
    }
}
