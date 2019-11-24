using System;
using System.Collections.Generic;
using System.Linq;
using NLyric.Audio;

namespace NLyric.Database {
	public static class Extensions {
		public static AlbumInfo Match(this IEnumerable<AlbumInfo> caches, Album album) {
			if (album is null)
				throw new ArgumentNullException(nameof(album));

			return caches.FirstOrDefault(t => IsMatched(t, album));
		}

		public static TrackInfo Match(this IEnumerable<TrackInfo> caches, Track track, Album album) {
			if (track is null)
				throw new ArgumentNullException(nameof(track));

			return caches.FirstOrDefault(t => IsMatched(t, track, album));
		}

		public static bool IsMatched(this AlbumInfo cache, Album album) {
			if (album is null)
				throw new ArgumentNullException(nameof(album));

			return cache.Name == album.Name;
		}

		public static bool IsMatched(this TrackInfo cache, Track track, Album album) {
			if (track is null)
				throw new ArgumentNullException(nameof(track));

			return cache.Name == track.Name && (album is null ? cache.AlbumName is null : cache.AlbumName == album.Name) && cache.Artists.SequenceEqual(track.Artists);
			// 如果album为空，要求cache中AlbumName也为空，如果album不为空，要求cache中AlbumName匹配
		}
	}
}
