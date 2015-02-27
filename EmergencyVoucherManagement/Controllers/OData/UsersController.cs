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
using EmergencyVoucherManagement.Models;


namespace EmergencyVoucherManagement.Controllers.OData
{
    public class UsersController : System.Web.OData.ODataController
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
            return this.db.Users.Any(p => p.Id == key);
        }


        [EnableQuery]
        public async Task<IQueryable<ApplicationUser>> Get()
        {
            var users = await this.db.Users.ToArrayAsync();
            return users.AsQueryable();
        }

        [EnableQuery]
        public SingleResult<ApplicationUser> Get([FromODataUri] string key)
        {
            IQueryable<ApplicationUser> result = this.db.Users.Where(p => p.Id == key).Take(1).ToArray().AsQueryable();

            return SingleResult.Create(result);
        }

    }
}