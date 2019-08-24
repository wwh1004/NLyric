using System;
using System.Linq;

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
			_artists = artists.Select(t => t.Trim()).ToArray();
		}

		public Track(ATL.Track track) {
			if (track is null)
				throw new ArgumentNullException(nameof(track));

			_name = track.Title.GetSafeString();
			_artists = track.Artist.GetSafeString().SplitEx();
			Array.Sort(_artists, StringHelper.OrdinalComparer);
		}

		public override string ToString() {
			return "Name:" + _name + " | Artists:" + string.Join(",", _artists);
		}
	}
}
