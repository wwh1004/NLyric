using NLyric.Ncm;

namespace NLyric.Caches {
	internal sealed class LyricCache {
		public bool IsCollected { get; set; }

		public bool IsAbsoluteMusic { get; set; }

		public int RawVersion { get; set; }

		public int TranslatedVersion { get; set; }

		public LyricCache() {
		}

		public LyricCache(NcmLyric lyric) : this(lyric.IsCollected, lyric.IsAbsoluteMusic, lyric.RawVersion, lyric.TranslatedVersion) {
		}

		public LyricCache(bool isCollected, bool isAbsoluteMusic, int rawVersion, int translatedVersion) {
			IsCollected = isCollected;
			IsAbsoluteMusic = isAbsoluteMusic;
			RawVersion = rawVersion;
			TranslatedVersion = translatedVersion;
		}
	}
}
