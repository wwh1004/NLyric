using System;
using Newtonsoft.Json;
using NLyric.Audio;

namespace NLyric.Caches {
	public sealed class AlbumCache {
		public string Name { get; set; }

		public int Id { get; set; }

		[JsonConstructor]
		[Obsolete("Deserialization only", true)]
		public AlbumCache() {
		}

		public AlbumCache(Album album, int id) : this(album.Name, id) {
		}

		public AlbumCache(string name, int id) {
			if (name is null)
				throw new ArgumentNullException(nameof(name));

			Name = name;
			Id = id;
		}
	}
}
