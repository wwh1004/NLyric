using System;
using System.Linq;
using Newtonsoft.Json;
using NLyric.Audio;

namespace NLyric.Caches {
	public sealed class TrackCache : IEquatable<TrackCache> {
		public string Name { get; set; }

		public string[] Artists { get; set; }

		public string AlbumName { get; set; }

		public int Id { get; set; }

		[JsonConstructor]
		[Obsolete("Deserialization only", true)]
		public TrackCache() {
		}

		public TrackCache(Track track, Album album, int id) : this(track.Name, track.Artists, album.Name, id) {
		}

		public TrackCache(string name, string[] artists, string albumName, int id) {
			if (name == null)
				throw new ArgumentNullException(nameof(name));
			if (artists == null)
				throw new ArgumentNullException(nameof(artists));
			if (albumName == null)
				throw new ArgumentNullException(nameof(albumName));

			Name = name;
			Artists = artists;
			AlbumName = albumName;
			Id = id;
		}

		public static bool operator ==(TrackCache x, TrackCache y) {
			if (x == null)
				return x == null;
			return x.Equals(y);
		}

		public static bool operator !=(TrackCache x, TrackCache y) {
			return !(x == y);
		}

		public bool Equals(TrackCache obj) {
			return !(obj is null) && obj.Id == Id && obj.Name == Name && obj.AlbumName == AlbumName && obj.Artists.SequenceEqual(Artists);
		}

		public override bool Equals(object obj) {
			TrackCache cache;

			cache = obj as TrackCache;
			return !(cache is null) && Equals(cache);
		}

		public override int GetHashCode() {
			return Id.GetHashCode();
		}
	}
}
