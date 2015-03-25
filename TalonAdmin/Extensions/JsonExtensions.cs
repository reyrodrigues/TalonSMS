using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace TalonAdmin.Extensions
{
    public static class JsonExtensions
    {
        public static T PropertyValueIfExists<T>(this JObject self, string property)
        {
            var exists = self.Properties().Where(p => p.Name == property).Any() && !String.IsNullOrEmpty(self[property].ToString());
            return exists ? self[property].ToObject<T>() : default(T);
        }
    }
}