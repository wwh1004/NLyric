using System;
using Newtonsoft.Json;

namespace NLyric.Settings {
	internal sealed class CharArrayJsonConverter : JsonConverter<char[]> {
		public override char[] ReadJson(JsonReader reader, Type objectType, char[] existingValue, bool hasExistingValue, JsonSerializer serializer) {
			return ((string)reader.Value).ToCharArray();
		}

		public override void WriteJson(JsonWriter writer, char[] value, JsonSerializer serializer) {
			writer.WriteValue(new string(value));
		}
	}
}
