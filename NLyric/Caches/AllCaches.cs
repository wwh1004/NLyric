using System.Collections.Generic;

namespace NLyric.Caches {
	public sealed class AllCaches {
		public List<AlbumCache> AlbumCaches { get; set; }

		public List<LyricCache> LyricCaches { get; set; }

		public List<TrackCache> TrackCaches { get; set; }
	}
}
