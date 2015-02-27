using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EmergencyVoucherManagement.Models.BindingModels
{
    public class AssignVoucherBidingModel
    {
        public int BeneficiaryId { get; set; }
        public int VoucherId { get; set; }
    }
}
