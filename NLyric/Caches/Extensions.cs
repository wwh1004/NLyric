using System.Collections.Generic;
using System.Linq;
using NLyric.Audio;

namespace NLyric.Caches {
	public static class Extensions {
		public static AlbumCache Match(this IEnumerable<AlbumCache> caches, Album album) {
			return caches.FirstOrDefault(t => IsMatched(t, album));
		}

		public static TrackCache Match(this IEnumerable<TrackCache> caches, Track track, Album album) {
			return caches.FirstOrDefault(t => IsMatched(t, track, album));
		}

		public static LyricCache Match(this IEnumerable<LyricCache> caches, int id) {
			return caches.FirstOrDefault(t => IsMatched(t, id));
		}

		public static bool IsMatched(this AlbumCache cache, Album album) {
			return cache.Name == album.Name;
		}

		public static bool IsMatched(this TrackCache cache, Track track, Album album) {
			return cache.Name == track.Name && cache.AlbumName == album.Name && cache.Artists.SequenceEqual(track.Artists);
		}

		public static bool IsMatched(this LyricCache cache, int id) {
			return cache.Id == id;
		}
	}
}
