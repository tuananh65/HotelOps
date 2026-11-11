using System.Text.Json;
using System.Text.Json.Serialization;

namespace HotelOps.Api.Services.Insights;

// SQL datetime columns arrive with Kind=Unspecified. API report fields use the
// same explicit UTC policy as audit timestamps, never the host's local zone.
public sealed class InsightUtcJson : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        DateTime.SpecifyKind(reader.GetDateTime(), DateTimeKind.Utc);
    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) =>
        writer.WriteStringValue(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}

public sealed class InsightDecimalJson : JsonConverter<decimal>
{
    public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.String ? decimal.Parse(reader.GetString()!, System.Globalization.CultureInfo.InvariantCulture) : reader.GetDecimal();
    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString(System.Globalization.CultureInfo.InvariantCulture));
}
