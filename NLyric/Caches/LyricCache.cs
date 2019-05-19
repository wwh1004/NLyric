using System;
using Newtonsoft.Json;
using NLyric.Ncm;

namespace NLyric.Caches {
	public sealed class LyricCache {
		public int Id { get; set; }

		public bool IsAbsoluteMusic { get; set; }

		public int RawVersion { get; set; }

		public int TranslatedVersion { get; set; }

		public string CheckSum { get; set; }

		[JsonConstructor]
		[Obsolete("Deserialization only", true)]
		public LyricCache() {
		}

		public LyricCache(NcmLyric lyric, string checkSum) : this(lyric.Id, lyric.IsAbsoluteMusic, lyric.RawVersion, lyric.TranslatedVersion, checkSum) {
			if (!lyric.IsCollected)
				throw new ArgumentException("未收录的歌词不能添加到缓存", nameof(lyric));
		}

		public LyricCache(int id, bool isAbsoluteMusic, int rawVersion, int translatedVersion, string checkSum) {
			if (checkSum == null)
				throw new ArgumentNullException(nameof(checkSum));

			Id = id;
			IsAbsoluteMusic = isAbsoluteMusic;
			RawVersion = rawVersion;
			TranslatedVersion = translatedVersion;
			CheckSum = checkSum;
		}
	}
}
