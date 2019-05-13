using System;
using System.Collections.Generic;

namespace NLyric.Settings {
	internal sealed class MatchSettings {
		private double _minimumSimilarity;

		public double MinimumSimilarity {
			get => _minimumSimilarity;
			set {
				if (value < 0 || value > 1)
					throw new ArgumentOutOfRangeException(nameof(value));

				_minimumSimilarity = value;
			}
		}

		public Dictionary<char, char> CharReplace { get; set; }
	}
}
