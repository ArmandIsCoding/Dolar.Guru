using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ARM.Dolar.Guru.Models
{
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Globalization;

    public class StringOrNumberConverter : JsonConverter<string>
    {
        public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            // Si el token es un string, devuélvelo directamente
            if (reader.TokenType == JsonTokenType.String)
            {
                return reader.GetString();
            }

            // Si el token es un número, conviértelo a string
            if (reader.TokenType == JsonTokenType.Number)
            {
                // Usamos GetDouble para manejar números con decimales
                return reader.GetDouble().ToString(CultureInfo.InvariantCulture);
            }

            // Si es cualquier otra cosa (null, etc.), puedes devolver null o un string vacío
            // según lo que prefieras. Devolver el valor del token como string es una opción segura.
            using (var jsonDoc = JsonDocument.ParseValue(ref reader))
            {
                return jsonDoc.RootElement.Clone().ToString();
            }
        }

        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
        {
            // Al escribir, simplemente escribimos el valor como un string.
            writer.WriteStringValue(value);
        }
    }
}
