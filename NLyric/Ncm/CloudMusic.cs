using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NLyric.AudioInfo;
using NLyric.Lyrics;

namespace NLyric.Ncm {
	internal static class CloudMusic {
		public static async Task<NcmTrack[]> SearchTrackAsync(Track track, bool withArtists) {
			List<string> keywords;
			JToken json;

			keywords = new List<string>();
			if (track.Name.Length != 0)
				keywords.Add(track.Name);
			if (withArtists)
				keywords.AddRange(track.Artists);
			if (keywords.Count == 0)
				throw new ArgumentException("歌曲信息无效");
			for (int i = 0; i < keywords.Count; i++)
				keywords[i] = keywords[i].WholeWordReplace();
			json = await NcmApi.SearchAsync(keywords, NcmApi.SearchType.Track);
			if ((int)json["songCount"] == 0)
				return Array.Empty<NcmTrack>();
			return ((JArray)json["songs"]).Select(t => ParseTrack(t, false)).ToArray();
		}

		public static async Task<NcmAlbum[]> SearchAlbumAsync(Album album, bool withArtists) {
			List<string> keywords;
			JToken json;

			keywords = new List<string>();
			if (album.Name.Length != 0)
				keywords.Add(album.Name);
			if (withArtists)
				keywords.AddRange(album.Artists);
			if (keywords.Count == 0)
				throw new ArgumentException("专辑信息无效");
			for (int i = 0; i < keywords.Count; i++)
				keywords[i] = keywords[i].WholeWordReplace();
			json = await NcmApi.SearchAsync(keywords, NcmApi.SearchType.Album);
			if ((int)json["albumCount"] == 0)
				return Array.Empty<NcmAlbum>();
			return ((JArray)json["albums"]).Select(t => ParseAlbum(t)).ToArray();
		}

		public static async Task<NcmTrack[]> GetTracksAsync(int albumId) {
			JToken json;

			json = await NcmApi.GetAlbumAsync(albumId);
			return ((JArray)json["songs"]).Select(t => ParseTrack(t, true)).ToArray();
		}

		public static async Task<(bool, Lrc, Lrc)> GetLyricAsync(int trackId) {
			bool hasLyric;
			JToken json;
			JToken lrc;
			string lyric;
			Lrc rawLrc;
			JToken tlyric;
			Lrc translatedLrc;

			(hasLyric, json) = await NcmApi.GetLyricAsync(trackId);
			if (!hasLyric)
				// 未收录
				return (false, null, null);
			if (json == null)
				// 纯音乐
				return (true, null, null);
			lrc = json["lrc"];
			lyric = (string)lrc["lyric"];
			rawLrc = string.IsNullOrEmpty(lyric) ? null : Lrc.UnsafeParse(lyric);
			// 未翻译歌词
			tlyric = json["tlyric"];
			lyric = (string)tlyric["lyric"];
			translatedLrc = string.IsNullOrEmpty(lyric) ? null : Lrc.UnsafeParse(lyric);
			// 翻译歌词
			return (true, rawLrc, translatedLrc);
		}

		private static NcmAlbum ParseAlbum(JToken json) {
			Album album;
			NcmAlbum ncmAlbum;

			album = new Album((string)json["name"], ParseNames((JArray)json["artists"]), (int)json["size"], TimeStampToDateTime((long)json["publishTime"]).Year);
			ncmAlbum = new NcmAlbum(album, (int)json["id"]);
			return ncmAlbum;
		}

		private static NcmTrack ParseTrack(JToken json, bool fromAlbumDetail) {
			Track track;
			NcmTrack ncmTrack;

			track = new Track((string)json["name"], ParseNames((JArray)json[fromAlbumDetail ? "ar" : "artists"]));
			ncmTrack = new NcmTrack(track, (int)json["id"]);
			return ncmTrack;
		}

		private static string[] ParseNames(JArray array) {
			return array.Select(t => (string)t["name"]).ToArray();
		}

		private static DateTime TimeStampToDateTime(long timeStamp) {
			return new DateTime(1970, 1, 1).AddMilliseconds(timeStamp);
		}
	}
}
