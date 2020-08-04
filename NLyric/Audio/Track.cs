using System;
using System.Collections.Generic;
using System.Linq;
using TagLib;

namespace NLyric.Audio {
	/// <summary>
	/// 单曲
	/// </summary>
	public class Track : ITrackOrAlbum {
		private readonly string _name;
		private readonly string[] _artists;

		/// <summary>
		/// 名称
		/// </summary>
		public string Name => _name;

		/// <summary>
		/// 艺术家
		/// </summary>
		public IReadOnlyList<string> Artists => _artists;

		public Track(string name, IEnumerable<string> artists) {
			if (name is null)
				throw new ArgumentNullException(nameof(name));
			if (artists is null)
				throw new ArgumentNullException(nameof(artists));

			_name = name;
			_artists = artists.Select(t => t.Trim()).ToArray();
			Array.Sort(_artists, StringComparer.Ordinal);
		}

		public Track(Tag tag) {
			if (tag is null)
				throw new ArgumentNullException(nameof(tag));

			_name = tag.Title.GetSafeString();
			_artists = tag.Performers.SelectMany(s => s.GetSafeString().SplitEx()).ToArray();
			Array.Sort(_artists, StringComparer.Ordinal);
		}

		public override string ToString() {
			return "Name:" + _name + " | Artists:" + string.Join(",", _artists);
		}
	}
}
