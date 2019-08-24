using System;
using System.Collections.Generic;

namespace NLyric.Audio {
	public class Track : ITrackOrAlbum {
		private readonly string _name;
		private readonly string[] _artists;

		public string Name => _name;

		public string[] Artists => _artists;

		public Track(string name, string[] artists) {
			if (name is null)
				throw new ArgumentNullException(nameof(name));
			if (artists is null)
				throw new ArgumentNullException(nameof(artists));

			_name = name;
			_artists = artists;
		}

		public Track(ATL.Track track) {
			if (track is null)
				throw new ArgumentNullException(nameof(track));

			_name = track.Title.GetSafeString();
			_artists = track.Artist.GetSafeString().SplitEx();
			Array.Sort(_artists, StringComparer.Instance);
		}

		public override string ToString() {
			return "Name:" + _name + " | Artists:" + string.Join(",", _artists);
		}

		private sealed class StringComparer : IComparer<string> {
			private static readonly StringComparer _instance = new StringComparer();

			public static StringComparer Instance => _instance;

			private StringComparer() {
			}

			public int Compare(string x, string y) {
				return string.CompareOrdinal(x, y);
			}
		}
	}
}
