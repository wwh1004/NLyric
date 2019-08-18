using System;

namespace NLyric.Settings {
	internal sealed class AllSettings {
		private static AllSettings _default;

		public static AllSettings Default {
			get {
				if (_default is null)
					throw new InvalidOperationException();

				return _default;
			}
			set {
				if (value is null)
					throw new ArgumentNullException(nameof(value));
				if (!(_default is null))
					throw new InvalidOperationException();

				_default = value;
			}
		}

		public SearchSettings Search { get; set; }

		public FuzzySettings Fuzzy { get; set; }

		public MatchSettings Match { get; set; }

		public LyricSettings Lyric { get; set; }
	}
}
