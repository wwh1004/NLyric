using System.Text;
using Newtonsoft.Json;

namespace NLyric.Settings {
	internal sealed class LyricSettings {
		public string[] Modes { get; set; }

		public bool SimplifyTranslated { get; set; }

		[JsonConverter(typeof(EncodingConverter))]
		public Encoding Encoding { get; set; }

		public bool AutoUpdate { get; set; }

		public bool Overwriting { get; set; }
	}
}
