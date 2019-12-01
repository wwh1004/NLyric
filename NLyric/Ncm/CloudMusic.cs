using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NeteaseCloudMusicApi;
using Newtonsoft.Json.Linq;
using NLyric.Audio;
using NLyric.Lyrics;

namespace NLyric.Ncm {
	public static class CloudMusic {
		private static readonly CloudMusicApi _api = new CloudMusicApi();

		public static async Task<bool> LoginAsync(string account, string password) {
			Dictionary<string, string> queries;
			bool isPhone;
			bool result;

			queries = new Dictionary<string, string>();
			isPhone = Regex.Match(account, "^[0-9]+$").Success;
			queries[isPhone ? "phone" : "email"] = account;
			queries["password"] = password;
			(result, _) = await _api.RequestAsync(isPhone ? CloudMusicApiProviders.LoginCellphone : CloudMusicApiProviders.Login, queries);
			return result;
		}

		public static async Task<NcmTrack[]> SearchTrackAsync(Track track, int limit, bool withArtists) {
			List<string> keywords;
			bool isOk;
			JObject json;
			JArray songs;

			keywords = new List<string>();
			if (track.Name.Length != 0)
				keywords.Add(track.Name);
			if (withArtists)
				keywords.AddRange(track.Artists);
			if (keywords.Count == 0)
				throw new ArgumentException("歌曲信息无效");
			for (int i = 0; i < keywords.Count; i++)
				keywords[i] = keywords[i].WholeWordReplace();
			(isOk, json) = await _api.RequestAsync(CloudMusicApiProviders.Search, new Dictionary<string, string> {
				{ "keywords", string.Join(" ", keywords) },
				{ "type", "1" },
				{ "limit", limit.ToString() }
			});
			if (!isOk)
				throw new ApplicationException(nameof(CloudMusicApiProviders.Search) + " API错误");
			json = (JObject)json["result"];
			if (json is null)
				throw new KeywordForbiddenException(string.Join(" ", keywords));
			songs = json["songs"] as JArray;
			if (songs is null)
				return Array.Empty<NcmTrack>();
			return songs.Select(t => ParseTrack(t, false)).ToArray();
		}

		public static async Task<NcmAlbum[]> SearchAlbumAsync(Album album, int limit, bool withArtists) {
			List<string> keywords;
			bool isOk;
			JObject json;
			JArray albums;

			keywords = new List<string>();
			if (album.Name.Length != 0)
				keywords.Add(album.Name);
			if (withArtists)
				keywords.AddRange(album.Artists);
			if (keywords.Count == 0)
				throw new ArgumentException("专辑信息无效");
			for (int i = 0; i < keywords.Count; i++)
				keywords[i] = keywords[i].WholeWordReplace();
			(isOk, json) = await _api.RequestAsync(CloudMusicApiProviders.Search, new Dictionary<string, string> {
				{ "keywords", string.Join(" ", keywords) },
				{ "type", "10" },
				{ "limit", limit.ToString() }
			});
			if (!isOk)
				throw new ApplicationException(nameof(CloudMusicApiProviders.Search) + " API错误");
			json = (JObject)json["result"];
			if (json is null)
				throw new KeywordForbiddenException(string.Join(" ", keywords));
			albums = json["albums"] as JArray;
			if (albums is null)
				return Array.Empty<NcmAlbum>();
			// albumCount不可信，搜索"U-87 陈奕迅"返回albums有内容，但是albumCount为0
			return albums.Select(t => ParseAlbum(t)).ToArray();
		}

		public static async Task<NcmTrack[]> GetTracksAsync(int albumId) {
			bool isOk;
			JObject json;

			(isOk, json) = await _api.RequestAsync(CloudMusicApiProviders.Album, new Dictionary<string, string> {
				{ "id", albumId.ToString() }
			});
			if (!isOk)
				throw new ApplicationException(nameof(CloudMusicApiProviders.Album) + " API错误");
			return json["songs"].Select(t => ParseTrack(t, true)).ToArray();
		}

		public static async Task<NcmLyric> GetLyricAsync(int trackId) {
			bool isOk;
			JObject json;
			Lrc rawLrc;
			int rawVersion;
			Lrc translatedLrc;
			int translatedVersion;

			(isOk, json) = await _api.RequestAsync(CloudMusicApiProviders.Lyric, new Dictionary<string, string> {
				{ "id", trackId.ToString() }
			});
			if (!isOk)
				throw new ApplicationException(nameof(CloudMusicApiProviders.Lyric) + " API错误");
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

			album = new Album((string)json["name"], ParseNames(json["artists"]));
			ncmAlbum = new NcmAlbum(album, (int)json["id"]);
			return ncmAlbum;
		}

		private static NcmTrack ParseTrack(JToken json, bool isShortName) {
			Track track;
			NcmTrack ncmTrack;

			track = new Track((string)json["name"], ParseNames(json[isShortName ? "ar" : "artists"]));
			ncmTrack = new NcmTrack(track, (int)json["id"]);
			return ncmTrack;
		}

		private static string[] ParseNames(JToken json) {
			return json.Select(t => (string)t["name"]).ToArray();
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
	}
}
