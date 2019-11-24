using System;
using Newtonsoft.Json;
using NLyric.Ncm;

namespace NLyric.Database {
	/// <summary>
	/// 歌词信息
	/// </summary>
	public sealed class LyricInfo {
		/// <summary>
		/// 原始歌词版本
		/// </summary>
		public int RawVersion { get; set; }

		/// <summary>
		/// 翻译歌词版本（如果有）
		/// </summary>
		public int TranslatedVersion { get; set; }

		/// <summary>
		/// 歌词校验值
		/// </summary>
		public string CheckSum { get; set; }

		[JsonConstructor]
		[Obsolete("Deserialization only", true)]
		public LyricInfo() {
		}

		public LyricInfo(NcmLyric lyric, string checkSum) : this(lyric.RawVersion, lyric.TranslatedVersion, checkSum) {
			if (!lyric.IsCollected)
				throw new ArgumentException("未收录的歌词不能添加到缓存", nameof(lyric));
		}

		public LyricInfo(int rawVersion, int translatedVersion, string checkSum) {
			if (checkSum is null)
				throw new ArgumentNullException(nameof(checkSum));

			RawVersion = rawVersion;
			TranslatedVersion = translatedVersion;
			CheckSum = checkSum;
		}
	}
}
