using NLyric.Lyrics;

namespace NLyric.Ncm {
	public sealed class NcmLyric {
		private readonly int _id;
		private readonly bool _isCollected;
		private readonly bool _isAbsoluteMusic;
		private readonly Lrc _raw;
		private readonly int _rawVersion;
		private readonly Lrc _translated;
		private readonly int _translatedVersion;

		public int Id => _id;

		public bool IsCollected => _isCollected;

		public bool IsAbsoluteMusic => _isAbsoluteMusic;

		public Lrc Raw => _raw;

		public int RawVersion => _rawVersion;

		public Lrc Translated => _translated;

		public int TranslatedVersion => _translatedVersion;

		public NcmLyric(int id, bool isCollected, bool isAbsoluteMusic, Lrc raw, int rawVersion, Lrc translated, int translatedVersion) {
			_id = id;
			_isCollected = isCollected;
			_isAbsoluteMusic = isAbsoluteMusic;
			_raw = raw;
			_rawVersion = rawVersion;
			_translated = translated;
			_translatedVersion = translatedVersion;
		}
	}
}
