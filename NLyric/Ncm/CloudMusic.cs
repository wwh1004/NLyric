using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NLyric.Audio;
using NLyric.Lyrics;

namespace NLyric.Ncm {
	public static class CloudMusic {
		public static async Task<NcmTrack[]> SearchTrackAsync(Track track, int limit, bool withArtists) {
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
			json = await NcmApi.SearchAsync(keywords, NcmApi.SearchType.Track, limit);
			if ((int)json["songCount"] == 0)
				return Array.Empty<NcmTrack>();
			return ((JArray)json["songs"]).Select(t => ParseTrack(t, false)).ToArray();
		}

		public static async Task<NcmAlbum[]> SearchAlbumAsync(Album album, int limit, bool withArtists) {
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
			json = await NcmApi.SearchAsync(keywords, NcmApi.SearchType.Album, limit);
			if ((int)json["albumCount"] == 0)
				return Array.Empty<NcmAlbum>();
			return ((JArray)json["albums"]).Select(t => ParseAlbum(t)).ToArray();
		}

		public static async Task<NcmTrack[]> GetTracksAsync(int albumId) {
			JToken json;

			json = await NcmApi.GetAlbumAsync(albumId);
			return ((JArray)json["songs"]).Select(t => ParseTrack(t, true)).ToArray();
		}

		public static async Task<NcmLyric> GetLyricAsync(int trackId) {
			JToken json;
			Lrc rawLrc;
			int rawVersion;
			Lrc translatedLrc;
			int translatedVersion;

			json = await NcmApi.GetLyricAsync(trackId);
			if ((bool?)json["uncollected"] == true)
				// 未收录
				return new NcmLyric(trackId, false, false, null, 0, null, 0);
			if ((bool?)json["nolyric"] == true)
				// 纯音乐
				return new NcmLyric(trackId, true, true, null, 0, null, 0);
			(rawLrc, rawVersion) = ParseLyric(json["lrc"]);
			(translatedLrc, translatedVersion) = ParseLyric(json["tlyric"]);
			return new NcmLyric(trackId, true, false, rawLrc, rawVersion, translatedLrc, translatedVersion);
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

		private static (Lrc, int) ParseLyric(JToken json) {
			string lyric;
			Lrc lrc;
			int version;

			lyric = (string)json["lyric"];
			lrc = string.IsNullOrEmpty(lyric) ? null : Lrc.UnsafeParse(lyric);
			version = (int)json["version"];
			return (lrc, version);
		}

		private static DateTime TimeStampToDateTime(long timeStamp) {
			return new DateTime(1970, 1, 1).AddMilliseconds(timeStamp);
		}
	}
}
