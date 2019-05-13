using Newtonsoft.Json;

namespace NLyric.Settings {
	internal sealed class FuzzySettings {
		public bool TryIgnoringArtists { get; set; }

		public bool TryIgnoringExtraInfo { get; set; }

		[JsonConverter(typeof(CharArrayJsonConverter))]
		public char[] ExtraInfoStart { get; set; }

		public string[] Covers { get; set; }

		public string[] Featurings { get; set; }
	}
}
