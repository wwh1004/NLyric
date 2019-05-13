using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace NLyric.Lyrics {
	public sealed class Lrc {
		const string TI = "ti:";
		const string AR = "ar:";
		const string AL = "al:";
		const string BY = "by:";
		const string OFFSET = "offset:";

		private string _title;
		private string _artist;
		private string _album;
		private string _by;
		private TimeSpan? _offset;
		private readonly Dictionary<TimeSpan, string> _lyrics = new Dictionary<TimeSpan, string>();

		public string Title {
			get => _title;
			set {
				if (value == null) {
					_title = value;
					return;
				}
				value = value.Trim();
				_title = value.Length == 0 ? null : value;
			}
		}

		public string Artist {
			get => _artist;
			set {
				if (value == null) {
					_artist = value;
					return;
				}
				value = value.Trim();
				_artist = value.Length == 0 ? null : value;
			}
		}

		public string Album {
			get => _album;
			set {
				if (value == null) {
					_album = value;
					return;
				}
				value = value.Trim();
				_album = value.Length == 0 ? null : value;
			}
		}

		public string By {
			get => _by;
			set {
				if (value == null) {
					_by = value;
					return;
				}
				value = value.Trim();
				_by = value.Length == 0 ? null : value;
			}
		}

		public TimeSpan? Offset {
			get => _offset;
			set => _offset = (value == null || value.Value.Ticks == 0) ? null : value;
		}

		public Dictionary<TimeSpan, string> Lyrics => _lyrics;

		public static Lrc Parse(string text) {
			if (string.IsNullOrEmpty(text))
				throw new ArgumentNullException(nameof(text));

			Lrc lrc;

			lrc = new Lrc();
			using (StringReader reader = new StringReader(text)) {
				string line;

				while ((line = reader.ReadLine()) != null)
					if (!TryParseLine(line.Trim(), lrc))
						throw new FormatException();
			}
			return lrc;
		}

		public static Lrc UnsafeParse(string text) {
			if (string.IsNullOrEmpty(text))
				throw new ArgumentNullException(nameof(text));

			Lrc lrc;

			lrc = new Lrc();
			using (StringReader reader = new StringReader(text)) {
				string line;

				while ((line = reader.ReadLine()) != null)
					TryParseLine(line.Trim(), lrc);
			}
			return lrc;
		}

		private static bool TryParseLine(string line, Lrc lrc) {
			int startIndex;
			int endIndex;
			List<TimeSpan> times;
			string lyric;

			if (string.IsNullOrEmpty(line) || line[0] != '[')
				return false;
			startIndex = 0;
			times = new List<TimeSpan>();
			do {
				string token;

				endIndex = line.IndexOf(']', startIndex + 1);
				if (endIndex == -1)
					// 有"["但是没有"]"
					return false;
				token = line.Substring(startIndex + 1, endIndex - startIndex - 1);
				if (token.StartsWith(TI))
					lrc.Title = GetMetadata(token, TI);
				else if (token.StartsWith(AR))
					lrc.Artist = GetMetadata(token, AR);
				else if (token.StartsWith(AL))
					lrc.Album = GetMetadata(token, AL);
				else if (token.StartsWith(BY))
					lrc.By = GetMetadata(token, BY);
				else if (token.StartsWith(OFFSET)) {
					int offset;

					if (!int.TryParse(GetMetadata(token, OFFSET), out offset))
						return false;
					lrc.Offset = new TimeSpan(0, 0, 0, 0, offset);
				}
				else {
					TimeSpan time;

					if (!TimeSpan.TryParse("00:" + token, out time))
						return false;
					times.Add(time);
				}
			} while ((startIndex = line.IndexOf('[', endIndex + 1)) != -1);
			if (endIndex + 1 == line.Length)
				// 没有歌词
				return true;
			lyric = line.Substring(endIndex + 1).Trim();
			foreach (TimeSpan time in times)
				lrc._lyrics[time] = lyric;
			return true;

			string GetMetadata(string _line, string _key) {
				return _line.Substring(_key.Length, _line.Length - _key.Length);
			}
		}

		public override string ToString() {
			StringBuilder sb;

			sb = new StringBuilder();
			if (_title != null)
				AppendLine(sb, TI, _title);
			if (_artist != null)
				AppendLine(sb, AR, _artist);
			if (_album != null)
				AppendLine(sb, AL, _album);
			if (_by != null)
				AppendLine(sb, BY, _by);
			if (_offset != null)
				AppendLine(sb, OFFSET, ((long)_offset.Value.TotalMilliseconds).ToString());
			foreach (KeyValuePair<TimeSpan, string> lyric in _lyrics)
				sb.AppendLine($"[{TimeSpanToLyricString(lyric.Key)}]{lyric.Value}");
			return sb.ToString();

			void AppendLine(StringBuilder _sb, string key, string value) {
				_sb.AppendLine($"[{key}{value}]");
			}

			string TimeSpanToLyricString(TimeSpan _timeSpan) {
				string milliseconds;

				milliseconds = _timeSpan.Milliseconds.ToString("D3");
				return _timeSpan.Minutes.ToString("D2") + ":" + _timeSpan.Seconds.ToString("D2") + "." + milliseconds.Substring(0, 2);
			}
		}
	}
}
