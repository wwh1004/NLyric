using NLyric.AudioInfo;

namespace NLyric.Ncm {
	internal sealed class NcmAlbum : Album {
		private readonly int _id;

		public int Id => _id;

		public NcmAlbum(Album album, int id) : base(album.Name, album.Artists, album.TrackCount, album.Year) {
			_id = id;
		}

		public override string ToString() {
			return base.ToString() + " | Id:" + _id.ToString();
		}
	}
}
