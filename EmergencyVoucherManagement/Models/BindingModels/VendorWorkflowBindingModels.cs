using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace EmergencyVoucherManagement.Models.BindingModels
{
    public class IncomingSmsBindingModel
    {
        [Newtonsoft.Json.JsonProperty(PropertyName = "to")]
        public string To { get; set; }

        [Newtonsoft.Json.JsonProperty(PropertyName = "from")]
        public string From { get; set; }
        
        [Newtonsoft.Json.JsonProperty(PropertyName = "message")]
        public string Message { get; set; }
    }
}