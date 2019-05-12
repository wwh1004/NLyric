using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace NLyric {
	internal sealed class Settings {
		private static Settings _default;

		public static Settings Default {
			get {
				if (_default == null)
					throw new InvalidOperationException();
				return _default;
			}
			set {
				if (value == null)
					throw new ArgumentNullException(nameof(value));
				if (_default != null)
					throw new InvalidOperationException();
				_default = value;
			}
		}

		public SearchSettings Search { get; set; }

		public FuzzySettings Fuzzy { get; set; }

		public MatchSettings Match { get; set; }

		public LyricSettings Lyric { get; set; }
	}

	internal sealed class SearchSettings {
		public string[] AudioExtensions { get; set; }

		[JsonConverter(typeof(CharArrayJsonConverter))]
		public char[] Separators { get; set; }

		public Dictionary<string, string> WholeWordReplace { get; set; }
	}

	internal sealed class FuzzySettings {
		public bool TryIgnoringArtists { get; set; }

		public bool TryIgnoringExtraInfo { get; set; }

		[JsonConverter(typeof(CharArrayJsonConverter))]
		public char[] ExtraInfoStart { get; set; }

		public string[] Covers { get; set; }

		public string[] Featurings { get; set; }
	}

	internal sealed class MatchSettings {
		private double _minimumSimilarity;

		public double MinimumSimilarity {
			get => _minimumSimilarity;
			set {
				if (value < 0 || value > 1)
					throw new ArgumentOutOfRangeException(nameof(value));

				_minimumSimilarity = value;
			}
		}

		public Dictionary<char, char> CharReplace { get; set; }
	}

	internal sealed class LyricSettings {
		public string[] Modes { get; set; }
	}

	internal sealed class CharArrayJsonConverter : JsonConverter<char[]> {
		public override char[] ReadJson(JsonReader reader, Type objectType, char[] existingValue, bool hasExistingValue, JsonSerializer serializer) {
			return ((string)reader.Value).ToCharArray();
		}

		public override void WriteJson(JsonWriter writer, char[] value, JsonSerializer serializer) {
			writer.WriteValue(new string(value));
		}
	}
}
