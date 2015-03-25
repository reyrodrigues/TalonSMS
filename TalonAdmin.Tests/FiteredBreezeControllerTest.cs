using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Web.Http;
using System.Net.Http;
using System.Web.Http.Routing;
using System.Web.Http.Controllers;
using System.Web.Http.Hosting;
using System.Linq;

namespace TalonAdmin.Tests
{
    [TestClass]
    public class FiteredBreezeControllerTest
    {
        [TestMethod]
        public void CheckOrganizations()
        {
            var controller = new TalonAdmin.Controllers.Breeze.EVMController();
            var ctx = new TalonAdmin.Models.Vouchers.Context();


            // Adding some folks
            Enumerable.Range(0, 1000).ToList().ForEach((i) => ctx.Beneficiaries.Add(new TalonAdmin.Models.Vouchers.Beneficiary { Name = Guid.NewGuid().ToString(), DateOfBirth=DateTime.Now }));
            Enumerable.Range(0, 100).ToList().ForEach((i) => ctx.Beneficiaries.Add(new TalonAdmin.Models.Vouchers.Beneficiary { Name = Guid.NewGuid().ToString(), DateOfBirth = DateTime.Now, OrganizationId = 1, CountryId = 1 }));
            
            ctx.SaveChanges();


            var config = new HttpConfiguration();
            var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost/Breeze/EVM/Beneficiaries");
            var route = config.Routes.MapHttpRoute("DefaultApi", "Breeze/{controller}/{action}");
            var routeData = new HttpRouteData(route, new HttpRouteValueDictionary { { "EVM", "Beneficiaries" } });

            request.Headers.Add("X-Tenant-Country", "1");
            request.Headers.Add("X-Tenant-Organization", "1");

            controller.ControllerContext = new HttpControllerContext(config, routeData, request);
            controller.Request = request;
            controller.Request.Properties[HttpPropertyKeys.HttpConfigurationKey] = config;

            var breezeCount = controller.Beneficiaries().Count();
            var actualCount = ctx.Beneficiaries.Count();

            Assert.AreNotEqual(breezeCount, actualCount);
        }
    }
}
