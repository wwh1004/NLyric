using System.Collections.Generic;

namespace NLyric.Database {
	/// <summary>
	/// NLyric数据库
	/// </summary>
	public sealed class NLyricDatabase {
		/// <summary>
		/// 专辑信息
		/// </summary>
		public List<AlbumInfo> AlbumInfos { get; set; }

		/// <summary>
		/// 单曲信息
		/// </summary>
		public List<TrackInfo> TrackInfos { get; set; }

		/// <summary>
		/// 数据库格式版本
		/// </summary>
		public int FormatVersion { get; set; }

		/// <summary>
		/// 检查 <see cref="FormatVersion"/>
		/// </summary>
		/// <returns></returns>
		public bool CheckFormatVersion() {
			switch (FormatVersion) {
			case 0:
			case 1:
				return true;
			default:
				return false;
			}
		}

		/// <summary>
		/// 是否为老版本数据库
		/// </summary>
		/// <returns></returns>
		public bool IsOldFormat() {
			return FormatVersion < 1;
		}
	}
}
