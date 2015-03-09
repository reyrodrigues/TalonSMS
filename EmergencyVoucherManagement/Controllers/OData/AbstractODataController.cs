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


namespace EmergencyVoucherManagement.Controllers.OData
{
    public abstract class AbstractODataController<Context, Type> : System.Web.OData.ODataController
        where Context : DbContext, new()
        where Type : Models.Vouchers.Entity, new()
    {
        protected abstract DbSet<Type> Collection
        {
            get;
        }

        private static ODataValidationSettings _validationSettings = new ODataValidationSettings();

        protected Context db = new Context();


        protected override void Dispose(bool disposing)
        {
            db.Dispose();
            base.Dispose(disposing);
        }


        private bool Exists(int key)
        {
            return this.Collection.Any(p => p.Id == key);
        }

        [EnableQuery]
        public IQueryable<Type> Get()
        {
            return this.Collection;
        }

        [EnableQuery]
        public SingleResult<Type> Get([FromODataUri] int key)
        {
            IQueryable<Type> result = this.Collection.Where(p => p.Id == key);

            return SingleResult.Create(result);
        }

        public async Task<IHttpActionResult> Post(Type entity)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            Collection.Add(entity);
            await db.SaveChangesAsync();
            return Created(entity);
        }

        public async Task<IHttpActionResult> Patch([FromODataUri] int key, Delta<Type> delta)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            var entity = await Collection.FindAsync(key);

            if (entity == null)
            {
                return NotFound();
            }
            delta.Patch(entity);

            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!Exists(key))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
            return Updated(entity);
        }
        public async Task<IHttpActionResult> Put([FromODataUri] int key, Type update)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            if (key != update.Id)
            {
                return BadRequest();
            }
            db.Entry(update).State = EntityState.Modified;
            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!Exists(key))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
            return Updated(update);
        }

        public async Task<IHttpActionResult> Delete([FromODataUri] int key)
        {
            var vendor = await Collection.FindAsync(key);
            if (vendor == null)
            {
                return NotFound();
            }
           Collection.Remove(vendor);
            await db.SaveChangesAsync();
            return StatusCode(HttpStatusCode.NoContent);
        }
    }
}