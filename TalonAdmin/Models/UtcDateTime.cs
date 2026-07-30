using System;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace TalonAdmin.Models
{
    /// <summary>
    /// EF6 stamped <see cref="DateTimeKind.Utc"/> onto every materialised DateTime via the
    /// ObjectMaterialized event. EF Core has no such event, so the same guarantee is expressed
    /// as a value converter applied to every DateTime property in both contexts.
    /// </summary>
    public class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
    {
        public UtcDateTimeConverter()
            : base(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc))
        {
        }
    }

    public class NullableUtcDateTimeConverter : ValueConverter<DateTime?, DateTime?>
    {
        public NullableUtcDateTimeConverter()
            : base(
                v => v,
                v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v)
        {
        }
    }
}
