using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Web;

namespace Newtonsoft.Json.Linq
{
    public static class JsonExtensions
    {
        /// <summary>
        /// Gets value of JSON Property or default(T) if nothing is found
        /// </summary>
        /// <typeparam name="T">Type of property</typeparam>
        /// <param name="self">JObject</param>
        /// <param name="property">name of property</param>
        /// <returns></returns>
        public static T PropertyValueIfExists<T>(this JObject self, string property)
        {
            var exists = self.Properties().Where(p => p.Name == property).Any() && !String.IsNullOrEmpty(self[property].ToString());
            return exists ? self[property].ToObject<T>() : default(T);
        }

        /// <summary>
        /// Removes properties from JObjects inside of JArray (Chainable)
        /// </summary>
        /// <param name="self">JArray with objects</param>
        /// <param name="properties">Name of the properties that need to be removed</param>
        /// <returns>self</returns>
        public static JArray RemoveProperties(this JArray self, params string[] properties)
        {
            self.ToList().ForEach(o => ((JObject)o).RemoveProperties(properties));

            return self;
        }

        /// <summary>
        /// Removes properties from JObject
        /// </summary>
        /// <param name="self">JObject with properties that need removing</param>
        /// <param name="properties">Case insensitive names of properties</param>
        /// <returns>self</returns>
        public static JObject RemoveProperties(this JObject self, params string[] properties)
        {

            foreach (string property in properties)
            {
                var propQuery = self.Properties().Where(p => p.Name.ToLower() == property.ToLower());
                if (propQuery.Any())
                {
                    propQuery.ToList().ForEach(p => p.Remove());
                }
            }

            return self;
        }

        /// <summary>
        /// Flattens objects in JArray by moving their properties to their nearest parent
        /// </summary>
        /// <param name="self">JArray of JObjects</param>
        /// <param name="properties">JPath queries for properties</param>
        /// <returns>self</returns>
        public static JArray Flatten(this JArray self, params string[] properties)
        {
            self.OfType<JObject>().ToList().ForEach(o => o.Flatten(properties));

            return self;
        }

        /// <summary>
        /// Flattens objects  by moving their properties to their nearest parent
        /// </summary>
        /// <param name="self">JObject with complex properties</param>
        /// <param name="properties">JPath queries for properties</param>
        public static JObject Flatten(this JObject self, params string[] properties)
        {
            foreach (string property in properties)
            {
                var prop = self.SelectToken(property).Parent as JProperty;
                if (prop != null)
                {
                    var parent = prop.Parent as JObject;
                    var child = prop.Value as JObject;
                    if (child != null)
                    {
                        prop.Remove();

                        child.Properties().ToList().ForEach(p => parent.Add(p));
                    }
                }
            }

            return self;
        }

        /// <summary>
        /// Transforms JArray into a DataTable by cleaning up all child objects and casting it to DataTable
        /// </summary>
        /// <param name="self">JArray to be transformed</param>
        /// <returns>DataTable</returns>
        public static DataTable ToDataTable(this JArray self)
        {
            self.OfType<JObject>().ToList().ForEach(o =>
                o.Properties()
                    .Where(p => p.Value is JObject || p.Value is JArray)
                    .ToList()
                    .ForEach(p => p.Remove())
            );

            return self.ToObject<DataTable>();
        }
    }
}