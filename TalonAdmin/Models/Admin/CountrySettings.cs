using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace TalonAdmin.Models.Admin
{
    public class CountrySettings : Entity
    {
        public virtual SmsBackendType BackendType { get; set; }
        public virtual string ServiceUrl { get; set; }
        public virtual string ServiceUser { get; set; }
        public virtual string ServicePassword { get; set; }
    }

    public enum SmsBackendType
    {
        RescueSMS = 1,
        Twilio = 2,
        CustomRest = 3
    }
}