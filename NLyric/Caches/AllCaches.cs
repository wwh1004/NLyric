using System.Collections.Generic;

namespace NLyric.Caches {
	internal sealed class AllCaches {
		public List<AlbumCache> AlbumCaches { get; set; }

		public List<TrackCache> TrackCaches { get; set; }
	}
}
