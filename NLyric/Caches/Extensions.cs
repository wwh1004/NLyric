using System;
using System.Collections.Generic;
using System.Linq;
using NLyric.Audio;

namespace NLyric.Caches {
	public static class Extensions {
		public static AlbumCache Match(this IEnumerable<AlbumCache> caches, Album album) {
			if (album == null)
				throw new ArgumentNullException(nameof(album));

			return caches.FirstOrDefault(t => IsMatched(t, album));
		}

		public static TrackCache Match(this IEnumerable<TrackCache> caches, Track track, Album album) {
			if (track == null)
				throw new ArgumentNullException(nameof(track));
			if (album == null)
				throw new ArgumentNullException(nameof(album));

			return caches.FirstOrDefault(t => IsMatched(t, track, album));
		}

		public static TrackCache Match(this IEnumerable<TrackCache> caches, Track track, string fileName) {
			if (track == null)
				throw new ArgumentNullException(nameof(track));
			if (fileName == null)
				throw new ArgumentNullException(nameof(fileName));

			return caches.FirstOrDefault(t => IsMatched(t, track, fileName));
		}

		public static LyricCache Match(this IEnumerable<LyricCache> caches, int id) {
			return caches.FirstOrDefault(t => IsMatched(t, id));
		}

		public static bool IsMatched(this AlbumCache cache, Album album) {
			if (album == null)
				throw new ArgumentNullException(nameof(album));

			return cache.Name == album.Name;
		}

		public static bool IsMatched(this TrackCache cache, Track track, Album album) {
			if (track == null)
				throw new ArgumentNullException(nameof(track));
			if (album == null)
				throw new ArgumentNullException(nameof(album));

			return cache.Name == track.Name && cache.AlbumName == album.Name && cache.Artists.SequenceEqual(track.Artists);
		}

		public static bool IsMatched(this TrackCache cache, Track track, string fileName) {
			if (track == null)
				throw new ArgumentNullException(nameof(track));
			if (fileName == null)
				throw new ArgumentNullException(nameof(fileName));

			return cache.Name == track.Name && cache.FileName == fileName && cache.Artists.SequenceEqual(track.Artists);
		}

		public static bool IsMatched(this LyricCache cache, int id) {
			return cache.Id == id;
		}
	}
}
