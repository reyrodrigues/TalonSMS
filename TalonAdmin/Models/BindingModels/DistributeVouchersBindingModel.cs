using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace TalonAdmin.Models.BindingModels
{
    public class DistributeVouchersBindingModel
    {
        public int ProgramId { get; set; }

        public int GroupId { get; set; }

        public int LocationId { get; set; }
    }
}