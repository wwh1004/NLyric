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

			return RemoveFeat(RemoveCover(value));
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

		private static string RemoveCover(string value) {
			int index;
			string right;

			index = value.IndexOfAny(_fuzzySettings.OpenBrackets);
			if (index == -1)
				return value;
			right = value.Substring(index + 1);
			if (_fuzzySettings.Covers.Any(s => right.StartsWith(s, StringComparison.OrdinalIgnoreCase)))
				value = value.Substring(0, index).TrimEnd();
			return value;
		}

		private static string RemoveFeat(string value) {
			int index;

			index = value.IndexOf("feat.", StringComparison.OrdinalIgnoreCase);
			if (index != -1)
				value = value.Substring(0, index).TrimEnd();
			return value;
		}
	}
}
