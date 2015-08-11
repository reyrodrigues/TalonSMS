using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace System.Data
{
    public static class SystemDotDataExtensions
    {
        public static void RemoveSafe(this System.Data.DataColumnCollection self, string name)
        {
            var query = self.OfType<DataColumn>().Where(c => c.ColumnName.ToLower() == name.ToLower());
            if (query.Any())
            {
                self.Remove(query.First());
            }
        }
    }
}
