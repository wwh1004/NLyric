using System;
using System.Collections.Generic;
using System.Linq;
using TagLib;

namespace NLyric.Audio {
	/// <summary>
	/// 专辑
	/// </summary>
	public class Album : ITrackOrAlbum {
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

		public Album(string name, IEnumerable<string> artists) {
			if (name is null)
				throw new ArgumentNullException(nameof(name));
			if (artists is null)
				throw new ArgumentNullException(nameof(artists));

			_name = name;
			_artists = artists.Select(t => t.Trim()).ToArray();
			Array.Sort(_artists, StringComparer.Ordinal);
		}

		/// <summary>
		/// 构造器
		/// </summary>
		/// <param name="tag"></param>
		/// <param name="getArtistsFromTrack">当 <see cref="Track.AlbumArtist"/> 为空时，是否从 <see cref="Track.Artist"/> 获取艺术家</param>
		public Album(Tag tag, bool getArtistsFromTrack) {
			if (tag is null)
				throw new ArgumentNullException(nameof(tag));
			if (!HasAlbumInfo(tag))
				throw new ArgumentException(nameof(tag) + " 中不存在专辑信息");

			_name = tag.Album.GetSafeString();
			string[] artists = tag.AlbumArtists.SelectMany(t => t.GetSafeString().SplitEx()).ToArray();
			if (getArtistsFromTrack && artists.Length == 0)
				artists = tag.Performers.SelectMany(t => t.GetSafeString().SplitEx()).ToArray();
			Array.Sort(artists, StringComparer.Ordinal);
			_artists = artists;
		}

		/// <summary>
		/// 是否存在专辑信息
		/// </summary>
		/// <param name="tag"></param>
		/// <returns></returns>
		public static bool HasAlbumInfo(Tag tag) {
			if (tag is null)
				throw new ArgumentNullException(nameof(tag));

			return !string.IsNullOrWhiteSpace(tag.Album);
		}

		public override string ToString() {
			return "Name:" + _name + " | Artists:" + string.Join(",", _artists);
		}
	}
}
