using System;

namespace TalonAdmin.Reporting
{
    /// <summary>
    /// Base for every report view model.
    ///
    /// The reports used to be handed anonymous types by RazorEngine. Runtime-compiled Razor views
    /// live in their own assembly and anonymous types are internal, so those models have to be
    /// public named types now.
    ///
    /// <see cref="DateToOffsetString"/> is the former
    /// <c>TalonAdmin.Extensions.ReportExtensions.DateToOffsetString</c>, which extended
    /// <c>RazorEngine.Templating.TemplateBase</c>. The templates call it as
    /// <c>@Model.DateToOffsetString(...)</c> instead of <c>@this.DateToOffsetString(...)</c>.
    /// </summary>
    public abstract class ReportModelBase
    {
        public string PaperSize { get; set; }

        public double? TimezoneOffset { get; set; }

        public string DateToOffsetString(DateTime? utcDate, double offset)
        {
            if (!utcDate.HasValue)
                return "";

            var dto = new DateTimeOffset(utcDate.Value).ToOffset(TimeSpan.FromHours(offset));
            return dto.ToString();
        }
    }
}
