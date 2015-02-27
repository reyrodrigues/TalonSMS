using System.Data;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Web.Http.ModelBinding;
using EmergencyVoucherManagement.Models.Vouchers;
using Microsoft.Data.OData;
using System.Threading.Tasks;
using System.Data.Entity.Infrastructure;
using System.Data.Entity;
using System.Web.OData.Query;
using System.Web.OData;
using Microsoft.AspNet.Identity.EntityFramework;
using EmergencyVoucherManagement.Models;


namespace EmergencyVoucherManagement.Controllers.OData
{
    public class RolesController : System.Web.OData.ODataController
    {
        private static ODataValidationSettings _validationSettings = new ODataValidationSettings();

        protected Models.ApplicationDbContext db = new ApplicationDbContext();


        protected override void Dispose(bool disposing)
        {
            db.Dispose();
            base.Dispose(disposing);
        }


        private bool Exists(string key)
        {
            return this.db.Roles.Any(p => p.Id == key);
        }

        [EnableQuery]
        public async Task<IQueryable<IdentityRole>> Get()
        {
            var roles =  await this.db.Roles.ToArrayAsync();
            return roles.AsQueryable();
        }

        [EnableQuery]
        public SingleResult<IdentityRole> Get([FromODataUri] string key)
        {
            IQueryable<IdentityRole> result = this.db.Roles.Where(p => p.Id == key).Take(1).ToArray().AsQueryable();

            return SingleResult.Create(result);
        }
    }
}