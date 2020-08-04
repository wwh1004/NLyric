using NLyric.Audio;

namespace NLyric.Ncm {
	public sealed class NcmTrack : Track {
		private readonly int _id;

		public int Id => _id;

		public NcmTrack(Track track, int id) : base(track.Name, track.Artists) {
			_id = id;
		}

		public override string ToString() {
			return $"{base.ToString()} | Id:{_id}";
		}
	}
}
