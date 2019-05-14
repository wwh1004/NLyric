using System;
using NLyric.Ncm;

namespace NLyric.Caches {
	internal sealed class LyricCache {
		public bool IsCollected { get; set; }

		public bool IsAbsoluteMusic { get; set; }

		public int RawVersion { get; set; }

		public int TranslatedVersion { get; set; }

		public string CheckSum { get; set; }

		public LyricCache() {
		}

		public LyricCache(NcmLyric lyric, string checkSum) : this(lyric.IsCollected, lyric.IsAbsoluteMusic, lyric.RawVersion, lyric.TranslatedVersion, checkSum) {
		}

		public LyricCache(bool isCollected, bool isAbsoluteMusic, int rawVersion, int translatedVersion, string checkSum) {
			if (checkSum == null)
				throw new ArgumentNullException(nameof(checkSum));

			IsCollected = isCollected;
			IsAbsoluteMusic = isAbsoluteMusic;
			RawVersion = rawVersion;
			TranslatedVersion = translatedVersion;
			CheckSum = checkSum;
		}
	}
}
