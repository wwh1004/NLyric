using System;
using System.Collections.Generic;

namespace NLyric.Settings {
	internal sealed class MatchSettings {
		private double _minimumSimilarityAuto;
		private double _minimumSimilarityUser;

		public double MinimumSimilarityAuto {
			get => _minimumSimilarityAuto;
			set {
				if (value < 0 || value > 1)
					throw new ArgumentOutOfRangeException(nameof(value));

				_minimumSimilarityAuto = value;
			}
		}

		public double MinimumSimilarityUser {
			get => _minimumSimilarityUser;
			set {
				if (value < 0 || value > 1)
					throw new ArgumentOutOfRangeException(nameof(value));

				_minimumSimilarityUser = value;
			}
		}

		public Dictionary<char, char> CharReplace { get; set; }
	}
}
