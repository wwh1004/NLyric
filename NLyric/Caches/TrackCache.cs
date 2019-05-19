using System;
using Newtonsoft.Json;
using NLyric.Audio;

namespace NLyric.Caches {
	public sealed class TrackCache {
		public string Name { get; set; }

		public string[] Artists { get; set; }

		public string AlbumName { get; set; }

		public string FileName { get; set; }

		public int Id { get; set; }

		[JsonConstructor]
		[Obsolete("Deserialization only", true)]
		public TrackCache() {
		}

		public TrackCache(Track track, Album album, int id) : this(track.Name, track.Artists, album.Name, null, id) {
		}

		public TrackCache(Track track, string fileName, int id) : this(track.Name, track.Artists, null, fileName, id) {
		}

		public TrackCache(string name, string[] artists, string albumName, string fileName, int id) {
			if (name == null)
				throw new ArgumentNullException(nameof(name));
			if (artists == null)
				throw new ArgumentNullException(nameof(artists));
			if (albumName == null && fileName == null)
				throw new ArgumentException($"{nameof(albumName)} 和 {nameof(fileName)} 不能同时为 null");

			Name = name;
			Artists = artists;
			AlbumName = albumName;
			FileName = fileName;
			Id = id;
		}
	}
}
