using System;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AxarDB.Helpers
{
    public class BigIntegerConverter : JsonConverter<BigInteger>
    {
        public override BigInteger Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Number)
            {
                using var doc = JsonDocument.ParseValue(ref reader);
                return BigInteger.Parse(doc.RootElement.GetRawText());
            }
            if (reader.TokenType == JsonTokenType.String)
            {
                return BigInteger.Parse(reader.GetString()!);
            }
            throw new JsonException($"Unexpected token type: {reader.TokenType}");
        }

        public override void Write(Utf8JsonWriter writer, BigInteger value, JsonSerializerOptions options)
        {
            writer.WriteRawValue(value.ToString());
        }
    }
}
