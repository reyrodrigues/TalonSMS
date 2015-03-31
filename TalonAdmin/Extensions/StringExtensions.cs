using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace System
{
    public  static class StringExtensions
    {
        /// <summary>
        /// Pythons definition of string.join
        /// </summary>
        /// <param name="self"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public static string Join(this string self,ICollection<string> parameters)
        {
            return String.Join(self, parameters);
        }
    }
}