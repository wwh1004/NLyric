using System;

namespace NLyric.AudioInfo {
	internal class Album : ITrackOrAlbum {
		private readonly string _name;
		private readonly string[] _artists;
		private readonly int? _trackCount;
		private readonly int? _year;

		public string Name => _name;

		public string[] Artists => _artists;

		public int? TrackCount => _trackCount;

		public int? Year => _year;

		public Album(string name, string[] artists, int? trackCount, int? year) {
			if (name == null)
				throw new ArgumentNullException(nameof(name));
			if (artists == null)
				throw new ArgumentNullException(nameof(artists));

			_name = name;
			_artists = artists;
			_trackCount = trackCount;
			_year = year;
		}

		/// <summary>
		/// 构造器
		/// </summary>
		/// <param name="track"></param>
		/// <param name="getArtistsFromTrack">当 <see cref="Track.AlbumArtist"/> 为空时，是否从 <see cref="Track.Artist"/> 获取艺术家</param>
		public Album(ATL.Track track, bool getArtistsFromTrack) {
			if (track == null)
				throw new ArgumentNullException(nameof(track));
			if (!HasAlbumInfo(track))
				throw new ArgumentException(nameof(track) + " 中不存在专辑信息");

			string artists;

			_name = track.Album.GetSafeString();
			artists = track.AlbumArtist.GetSafeString();
			if (getArtistsFromTrack && artists.Length == 0)
				artists = track.Artist.GetSafeString();
			_artists = artists.Length == 0 ? Array.Empty<string>() : artists.SplitEx();
			if (track.TrackTotal != 0)
				_trackCount = track.TrackTotal;
			if (track.Year != 0)
				_year = track.Year;
		}

		public static bool HasAlbumInfo(ATL.Track track) {
			if (track == null)
				throw new ArgumentNullException(nameof(track));

			return !string.IsNullOrWhiteSpace(track.Album);
		}

		public override string ToString() {
			return "Name:" + _name + " | Artists:" + string.Join(",", _artists) + " | TrackCount:" + _trackCount.ToString() + " | Year:" + _year.ToString();
		}
	}
}
