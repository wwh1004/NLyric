using NLyric.Lyrics;

namespace NLyric.Ncm {
	public sealed class NcmLyric {
		private readonly bool _isCollected;
		private readonly bool _isAbsoluteMusic;
		private readonly Lrc _raw;
		private readonly int _rawVersion;
		private readonly Lrc _translated;
		private readonly int _translatedVersion;

		public bool IsCollected => _isCollected;

		public bool IsAbsoluteMusic => _isAbsoluteMusic;

		public Lrc Raw => _raw;

		public int RawVersion => _rawVersion;

		public Lrc Translated => _translated;

		public int TranslatedVersion => _translatedVersion;

		public NcmLyric(bool isCollected, bool isAbsoluteMusic, Lrc raw, int rawVersion, Lrc translated, int translatedVersion) {
			_isCollected = isCollected;
			_isAbsoluteMusic = isAbsoluteMusic;
			_raw = raw;
			_rawVersion = rawVersion;
			_translated = translated;
			_translatedVersion = translatedVersion;
		}
	}
}
