using System;
using System.Linq;
using NLyric.Ncm;

namespace NLyric.Caches {
	internal sealed class AlbumCache {
		public string Name { get; set; }

		public string Artists { get; set; }

		public int Id { get; set; }

		public AlbumCache() {
		}

		public AlbumCache(NcmAlbum album) : this(album.Name, string.Join(",", album.Artists.OrderBy(s => s)), album.Id) {
		}

		public AlbumCache(string name, string artists, int id) {
			if (name == null)
				throw new ArgumentNullException(nameof(name));
			if (artists == null)
				throw new ArgumentNullException(nameof(artists));

			Name = name;
			Artists = artists;
			Id = id;
		}
	}
}
