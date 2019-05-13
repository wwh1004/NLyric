using System.Collections.Generic;
using Newtonsoft.Json;

namespace NLyric.Settings {
	internal sealed class SearchSettings {
		public string[] AudioExtensions { get; set; }

		[JsonConverter(typeof(CharArrayJsonConverter))]
		public char[] Separators { get; set; }

		public Dictionary<string, string> WholeWordReplace { get; set; }

		public int Limit { get; set; }
	}
}
