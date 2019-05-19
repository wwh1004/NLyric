using System;
using Newtonsoft.Json;
using NLyric.Audio;

namespace NLyric.Caches {
	public sealed class AlbumCache : IEquatable<AlbumCache> {
		public string Name { get; set; }

		public int Id { get; set; }

		[JsonConstructor]
		[Obsolete("Deserialization only", true)]
		public AlbumCache() {
		}

		public AlbumCache(Album album, int id) : this(album.Name, id) {
		}

		public AlbumCache(string name, int id) {
			if (name == null)
				throw new ArgumentNullException(nameof(name));

			Name = name;
			Id = id;
		}

		public static bool operator ==(AlbumCache x, AlbumCache y) {
			if (x == null)
				return x == null;
			return x.Equals(y);
		}

		public static bool operator !=(AlbumCache x, AlbumCache y) {
			return !(x == y);
		}

		public bool Equals(AlbumCache obj) {
			return !(obj is null) && obj.Id == Id && obj.Name == Name;
		}

		public override bool Equals(object obj) {
			AlbumCache cache;

			cache = obj as AlbumCache;
			return !(cache is null) && Equals(cache);
		}

		public override int GetHashCode() {
			return Id.GetHashCode();
		}
	}
}
