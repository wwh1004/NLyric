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
		private IDictionary<TimeSpan, string> _lyrics = new Dictionary<TimeSpan, string>();

		public string Title {
			get => _title;
			set {
				if (value is null) {
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
				if (value is null) {
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
				if (value is null) {
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
				if (value is null) {
					_by = value;
					return;
				}
				value = value.Trim();
				_by = value.Length == 0 ? null : value;
			}
		}

		public TimeSpan? Offset {
			get => _offset;
			set => _offset = (value is null || value.Value.Ticks == 0) ? null : value;
		}

		public IDictionary<TimeSpan, string> Lyrics {
			get => _lyrics;
			set {
				if (value is null)
					throw new ArgumentNullException(nameof(value));

				_lyrics = value;
			}
		}

		public static Lrc Parse(string text) {
			if (string.IsNullOrEmpty(text))
				throw new ArgumentNullException(nameof(text));

			var lrc = new Lrc();
			using (var reader = new StringReader(text)) {
				string line;
				while (!((line = reader.ReadLine()?.Trim()) is null) && !string.IsNullOrEmpty(line)) {
					if (!TryParseLine(line, lrc))
						throw new FormatException();
				}
			}
			return lrc;
		}

		public static Lrc UnsafeParse(string text) {
			if (string.IsNullOrEmpty(text))
				throw new ArgumentNullException(nameof(text));

			var lrc = new Lrc();
			using (var reader = new StringReader(text)) {
				string line;
				while (!((line = reader.ReadLine()?.Trim()) is null))
					TryParseLine(line.Trim(), lrc);
			}
			return lrc;
		}

		private static bool TryParseLine(string line, Lrc lrc) {
			if (string.IsNullOrEmpty(line) || line[0] != '[')
				return false;

			int startIndex = 0;
			int endIndex;
			var times = new List<TimeSpan>();

			do {
				endIndex = line.IndexOf(']', startIndex + 1);
				if (endIndex == -1)
					// 有"["但是没有"]"
					return false;
				string token = line.Substring(startIndex + 1, endIndex - startIndex - 1);
				if (token.StartsWith(TI))
					lrc.Title = GetMetadata(token, TI);
				else if (token.StartsWith(AR))
					lrc.Artist = GetMetadata(token, AR);
				else if (token.StartsWith(AL))
					lrc.Album = GetMetadata(token, AL);
				else if (token.StartsWith(BY))
					lrc.By = GetMetadata(token, BY);
				else if (token.StartsWith(OFFSET)) {
					if (!int.TryParse(GetMetadata(token, OFFSET), out int offset))
						return false;
					lrc.Offset = new TimeSpan(0, 0, 0, 0, offset);
				}
				else {
					if (!TimeSpan.TryParse("00:" + token, out var time))
						return false;
					times.Add(time);
				}
			} while ((startIndex = line.IndexOf('[', endIndex + 1)) != -1);

			string lyric = line.Substring(endIndex + 1).Trim();
			foreach (var time in times)
				lrc._lyrics[time] = lyric;
			return true;

			string GetMetadata(string _line, string _key) {
				return _line.Substring(_key.Length, _line.Length - _key.Length);
			}
		}

		public override string ToString() {
			var sb = new StringBuilder();
			if (!(_title is null))
				AppendLine(sb, TI, _title);
			if (!(_artist is null))
				AppendLine(sb, AR, _artist);
			if (!(_album is null))
				AppendLine(sb, AL, _album);
			if (!(_by is null))
				AppendLine(sb, BY, _by);
			if (!(_offset is null))
				AppendLine(sb, OFFSET, ((long)_offset.Value.TotalMilliseconds).ToString());
			foreach (var lyric in _lyrics)
				sb.AppendLine($"[{TimeSpanToLyricString(lyric.Key)}]{lyric.Value}");
			return sb.ToString();

			void AppendLine(StringBuilder _sb, string key, string value) {
				_sb.AppendLine($"[{key}{value}]");
			}

			string TimeSpanToLyricString(TimeSpan _timeSpan) {
				string milliseconds = _timeSpan.Milliseconds.ToString("D3");
				return $"{_timeSpan.Minutes:D2}:{_timeSpan.Seconds:D2}.{milliseconds.Substring(0, 2)}";
			}
		}
	}
}
