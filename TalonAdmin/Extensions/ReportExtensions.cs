using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace TalonAdmin.Extensions
{
    public static class ReportExtensions
    {
        public static string DateToOffsetString(this RazorEngine.Templating.TemplateBase self, DateTime? utcDate, double offset)
        {
            if (!utcDate.HasValue)
                return "";

            var dto = new DateTimeOffset(utcDate.Value).ToOffset(TimeSpan.FromHours(offset));
            return dto.ToString();
        }
    }
}