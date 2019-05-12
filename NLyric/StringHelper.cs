using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NLyric {
	internal static class StringHelper {
		private static readonly SearchSettings _searchSettings = Settings.Default.Search;
		private static readonly FuzzySettings _fuzzySettings = Settings.Default.Fuzzy;
		private static readonly MatchSettings _matchSettings = Settings.Default.Match;

		/// <summary>
		/// 获取非空字符串，并且清楚首尾空格
		/// </summary>
		/// <param name="value"></param>
		/// <returns></returns>
		public static string GetSafeString(this string value) {
			return value == null ? string.Empty : value.Trim();
		}

		/// <summary>
		/// 同时调用 <see cref="WholeWordReplace(string)"/> 与 <see cref="CharReplace(string)"/>
		/// </summary>
		/// <param name="value"></param>
		/// <returns></returns>
		public static string ReplaceEx(this string value) {
			if (value == null)
				throw new ArgumentNullException(nameof(value));

			return value.WholeWordReplace().CharReplace();
		}

		/// <summary>
		/// 使用 <see cref="SearchSettings.WholeWordReplace"/> 进行全词替换
		/// </summary>
		/// <param name="value"></param>
		/// <returns></returns>
		public static string WholeWordReplace(this string value) {
			if (value == null)
				throw new ArgumentNullException(nameof(value));

			if (value.Length == 0)
				return value;
			foreach (KeyValuePair<string, string> pair in _searchSettings.WholeWordReplace)
				if (value.Equals(pair.Key, StringComparison.OrdinalIgnoreCase))
					return pair.Value;
			return value;
		}

		/// <summary>
		/// 使用 <see cref="MatchSettings.CharReplace"/> 进行字符替换
		/// </summary>
		/// <param name="value"></param>
		/// <returns></returns>
		public static string CharReplace(this string value) {
			if (value == null)
				throw new ArgumentNullException(nameof(value));

			StringBuilder buffer;

			if (value.Length == 0)
				return value;
			buffer = new StringBuilder(value);
			for (int i = 0; i < buffer.Length; i++)
				foreach (KeyValuePair<char, char> pair in _matchSettings.CharReplace)
					if (buffer[i] == pair.Key)
						buffer[i] = pair.Value;
			return buffer.ToString();
		}

		/// <summary>
		/// 使用 <see cref="FuzzySettings"/> 进行模糊处理
		/// </summary>
		/// <param name="value"></param>
		/// <returns></returns>
		public static string Fuzzy(this string value) {
			if (value == null)
				throw new ArgumentNullException(nameof(value));

			int fuzzyStartIndex;

			fuzzyStartIndex = -1;
			while ((fuzzyStartIndex = value.IndexOfAny(_fuzzySettings.ExtraInfoStart, fuzzyStartIndex + 1)) != -1) {
				string extraInfo;

				extraInfo = value.Substring(fuzzyStartIndex + 1);
				if (Enumerable.Concat(_fuzzySettings.Covers, _fuzzySettings.Featurings).Any(s => extraInfo.StartsWith(s, StringComparison.OrdinalIgnoreCase)))
					return value.Substring(0, fuzzyStartIndex).TrimEnd();
			}
			return value;
		}

		/// <summary>
		/// 使用 <see cref="SearchSettings.Separators"/> 进行分割字符串并且移除空字符串
		/// </summary>
		/// <param name="value"></param>
		/// <returns></returns>
		public static string[] SplitEx(this string value) {
			if (value == null)
				throw new ArgumentNullException(nameof(value));

			return value.Split(_searchSettings.Separators, StringSplitOptions.RemoveEmptyEntries);
		}

		/// <summary>
		/// 全角字符转半角字符
		/// </summary>
		/// <param name="value"></param>
		/// <returns></returns>
		public static string ToHalfWidth(this string value) {
			if (value == null)
				throw new ArgumentNullException(nameof(value));

			char[] chars;

			chars = value.ToCharArray();
			for (int i = 0; i < chars.Length; i++) {
				if (chars[i] == '\u3000')
					chars[i] = '\u0020';
				else if (chars[i] > '\uFF00' && chars[i] < '\uFF5F')
					chars[i] = (char)(chars[i] - 0xFEE0);
			}
			return new string(chars);
		}
	}
}
