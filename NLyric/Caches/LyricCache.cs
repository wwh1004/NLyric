using System;
using Newtonsoft.Json;
using NLyric.Ncm;

namespace NLyric.Caches {
	public sealed class LyricCache : IEquatable<LyricCache> {
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

		public static bool operator ==(LyricCache x, LyricCache y) {
			if (x == null)
				return x == null;
			return x.Equals(y);
		}

		public static bool operator !=(LyricCache x, LyricCache y) {
			return !(x == y);
		}

		public bool Equals(LyricCache obj) {
			return !(obj is null) && obj.Id == Id && obj.CheckSum == CheckSum && obj.IsAbsoluteMusic == IsAbsoluteMusic && obj.RawVersion == RawVersion && obj.TranslatedVersion == TranslatedVersion;
		}

		public override bool Equals(object obj) {
			LyricCache cache;

			cache = obj as LyricCache;
			return !(cache is null) && Equals(cache);
		}

		public override int GetHashCode() {
			return Id.GetHashCode();
		}
	}
}
