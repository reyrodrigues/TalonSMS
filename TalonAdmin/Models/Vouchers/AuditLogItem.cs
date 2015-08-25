using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TalonAdmin.Models.Vouchers
{
    public class AuditLogItem : Entity
    {
        public virtual string ObjectType { get; set; }
        public virtual int ObjectId { get; set; }

        public virtual string ModifiedBy { get; set; }
        public virtual DateTime ModifiedOn { get; set; }
    }
}
