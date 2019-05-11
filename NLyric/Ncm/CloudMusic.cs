using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NLyric.AudioInfo;

namespace NLyric.Ncm {
	internal static class CloudMusic {
		public static async Task<IEnumerable<NcmTrack>> SearchTrackAsync(Track track, bool withArtists) {
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
			return ((JArray)json["songs"]).Select(t => ParseTrack(t, false));
		}

		public static async Task<IEnumerable<NcmAlbum>> SearchAlbumAsync(Album album, bool withArtists) {
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
			return ((JArray)json["albums"]).Select(t => ParseAlbum(t));
		}

		public static async Task<IEnumerable<NcmTrack>> GetTracksAsync(NcmAlbum ncmAlbum) {
			JToken json;

			json = await NcmApi.GetAlbumAsync(ncmAlbum.Id);
			return ((JArray)json["songs"]).Select(t => ParseTrack(t, true));
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
