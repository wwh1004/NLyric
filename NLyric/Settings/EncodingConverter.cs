using System;
using System.Text;
using Newtonsoft.Json;

namespace NLyric.Settings {
	internal sealed class EncodingConverter : JsonConverter<Encoding> {
		public override Encoding ReadJson(JsonReader reader, Type objectType, Encoding existingValue, bool hasExistingValue, JsonSerializer serializer) {
			return Encoding.GetEncoding((string)reader.Value);
		}

		public override void WriteJson(JsonWriter writer, Encoding value, JsonSerializer serializer) {
			writer.WriteValue(value.WebName);
		}
	}
}
