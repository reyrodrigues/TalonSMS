using System;
using System.Collections.Generic;
using System.Linq;

namespace TalonAdmin.Models.BindingModels
{
    public class DistributeVouchersBindingModel
    {
        public int ProgramId { get; set; }

        public int GroupId { get; set; }

        public int LocationId { get; set; }
    }
}