using System.Collections.Generic;

namespace NLyric.Audio {
	public interface ITrackOrAlbum {
		string Name { get; }

		IReadOnlyList<string> Artists { get; }
	}
}
