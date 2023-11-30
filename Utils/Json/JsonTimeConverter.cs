using System.Text.Json;
using System.Text.Json.Serialization;

namespace Octopus_Energy_iOS_Shortcut_Serverless_Backend.Utils.Json;

public class JsonDateTimeToUtcConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            if (DateTime.TryParse(reader.GetString(), out DateTime dateTime))
            {
                return dateTime.ToUniversalTime();
            }
        }
        return DateTime.MinValue; // or throw an exception if parsing fails
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToUniversalTime().ToString("o"));
    }
}