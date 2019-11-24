using System;
using Newtonsoft.Json;
using NLyric.Audio;

namespace NLyric.Database {
	/// <summary>
	/// 专辑信息
	/// </summary>
	public sealed class AlbumInfo {
		/// <summary>
		/// 名称
		/// </summary>
		public string Name { get; set; }

		/// <summary>
		/// 网易云音乐ID
		/// </summary>
		public int Id { get; set; }

		[JsonConstructor]
		[Obsolete("Deserialization only", true)]
		public AlbumInfo() {
		}

		public AlbumInfo(Album album, int id) : this(album.Name, id) {
		}

		public AlbumInfo(string name, int id) {
			if (name is null)
				throw new ArgumentNullException(nameof(name));

			Name = name;
			Id = id;
		}
	}
}
