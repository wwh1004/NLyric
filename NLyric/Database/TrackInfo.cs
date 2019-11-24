using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using NLyric.Audio;

namespace NLyric.Database {
	/// <summary>
	/// 单曲信息
	/// </summary>
	public sealed class TrackInfo {
		/// <summary>
		/// 名称
		/// </summary>
		public string Name { get; set; }

		/// <summary>
		/// 艺术家
		/// </summary>
		public IReadOnlyList<string> Artists { get; set; }

		/// <summary>
		/// 专辑名
		/// </summary>
		public string AlbumName { get; set; }

		/// <summary>
		/// 网易云音乐ID
		/// </summary>
		public int Id { get; set; }

		/// <summary>
		/// 歌词缓存
		/// </summary>
		public LyricInfo Lyric { get; set; }

		[JsonConstructor]
		[Obsolete("Deserialization only", true)]
		public TrackInfo() {
		}

		public TrackInfo(Track track, Album album, int id) : this(track.Name, track.Artists, album?.Name, id) {
		}

		public TrackInfo(string name, IEnumerable<string> artists, string albumName, int id) {
			if (name is null)
				throw new ArgumentNullException(nameof(name));
			if (artists is null)
				throw new ArgumentNullException(nameof(artists));

			Name = name;
			Artists = artists.Select(t => t.Trim()).ToArray();
			AlbumName = albumName;
			Id = id;
		}
	}
}
