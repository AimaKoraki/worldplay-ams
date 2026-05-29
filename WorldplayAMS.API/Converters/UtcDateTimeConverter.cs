using System.Text.Json;
using System.Text.Json.Serialization;

namespace WorldplayAMS.API.Converters;

public class UtcDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var dt = reader.GetDateTime();
        return dt.Kind == DateTimeKind.Unspecified 
            ? DateTime.SpecifyKind(dt, DateTimeKind.Utc) 
            : dt.ToUniversalTime();
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        // Supabase often returns Timestamps as Unspecified but in Local time representation
        // We force conversion to UTC so the frontend always receives clean ISO-8601 UTC dates
        var utcValue = value.Kind == DateTimeKind.Unspecified 
            ? DateTime.SpecifyKind(value, DateTimeKind.Local).ToUniversalTime() 
            : value.ToUniversalTime();

        writer.WriteStringValue(utcValue.ToString("yyyy-MM-ddTHH:mm:ssZ"));
    }
}
