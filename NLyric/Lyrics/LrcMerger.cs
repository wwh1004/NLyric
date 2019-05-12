using System;
using System.Collections.Generic;

namespace NLyric.Lyrics {
	internal static class LrcMerger {
		public static Lrc Merge(Lrc rawLrc, Lrc translatedLrc) {
			if (rawLrc == null)
				throw new ArgumentNullException(nameof(rawLrc));
			if (translatedLrc == null)
				throw new ArgumentNullException(nameof(translatedLrc));

			Lrc mergedLrc;

			mergedLrc = new Lrc {
				Offset = rawLrc.Offset,
				Title = rawLrc.Title
			};
			foreach (KeyValuePair<TimeSpan, string> rawLyric in rawLrc.Lyrics)
				mergedLrc.Lyrics.Add(rawLyric.Key, rawLyric.Value);
			foreach (KeyValuePair<TimeSpan, string> translatedLyric in translatedLrc.Lyrics) {
				string rawLyric;

				if (translatedLyric.Value.Length == 0)
					// 如果翻译歌词是空字符串，跳过
					continue;
				if (!mergedLrc.Lyrics.ContainsKey(translatedLyric.Key)) {
					// 如果没有对应的未翻译字符串，直接添加
					mergedLrc.Lyrics.Add(translatedLyric.Key, translatedLyric.Value);
					continue;
				}
				rawLyric = mergedLrc.Lyrics[translatedLyric.Key];
				if (rawLyric.Length == 0)
					// 如果未翻译歌词是空字符串，表示上一句歌词的结束，那么跳过
					continue;
				mergedLrc.Lyrics[translatedLyric.Key] = $"{rawLyric} 「{translatedLyric.Value}」";
			}
			return mergedLrc;
		}
	}
}
