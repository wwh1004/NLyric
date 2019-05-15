using System;
using System.Linq;
using NLyric.Ncm;

namespace NLyric.Caches {
	internal sealed class TrackCache {
		public string Name { get; set; }

		public string Artists { get; set; }

		public int Id { get; set; }

		public string AlbumName { get; set; }

		public LyricCache Lyric { get; set; }

		public TrackCache() {
		}

		public TrackCache(NcmTrack track, string albumName, NcmLyric lyric, string checkSum) : this(track.Name, string.Join(",", track.Artists.OrderBy(s => s)), track.Id, albumName, new LyricCache(lyric, checkSum)) {
		}

		public TrackCache(string name, string artists, int id, string albumName, LyricCache lyric) {
			if (name == null)
				throw new ArgumentNullException(nameof(name));
			if (artists == null)
				throw new ArgumentNullException(nameof(artists));
			if (albumName == null)
				throw new ArgumentNullException(nameof(albumName));
			if (lyric == null)
				throw new ArgumentNullException(nameof(lyric));

			Name = name;
			Artists = artists;
			Id = id;
			AlbumName = albumName;
			Lyric = lyric;
		}
	}
}
