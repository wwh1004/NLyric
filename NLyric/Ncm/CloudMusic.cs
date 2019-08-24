using System;
using System.Collections.Generic;
using System.Extensions;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NeteaseCloudMusicApi;
using Newtonsoft.Json.Linq;
using NLyric.Audio;
using NLyric.Lyrics;

namespace NLyric.Ncm {
	public static class CloudMusic {
		private static readonly CloudMusicApi _api = new CloudMusicApi();
		private static bool _isLoggedIn;

		public static async Task<bool> LoginAsync(string account, string password) {
			Dictionary<string, string> queries;
			bool isPhone;

			queries = new Dictionary<string, string>();
			isPhone = Regex.Match(account, "^[0-9]+$").Success;
			queries[isPhone ? "phone" : "email"] = account;
			queries["password"] = password;
			(_isLoggedIn, _) = await _api.RequestAsync(isPhone ? CloudMusicApiProviders.LoginCellphone : CloudMusicApiProviders.Login, queries);
			return _isLoggedIn;
		}

		public static async Task<NcmTrack[]> SearchTrackAsync(Track track, int limit, bool withArtists) {
			List<string> keywords;
			bool isOk;
			JObject json;

			keywords = new List<string>();
			if (track.Name.Length != 0)
				keywords.Add(track.Name);
			if (withArtists)
				keywords.AddRange(track.Artists);
			if (keywords.Count == 0)
				throw new ArgumentException("歌曲信息无效");
			for (int i = 0; i < keywords.Count; i++)
				keywords[i] = keywords[i].WholeWordReplace();
			if (_isLoggedIn) {
				(isOk, json) = await _api.RequestAsync(CloudMusicApiProviders.Search, new Dictionary<string, string> {
					{ "keywords", string.Join(" ", keywords) },
					{ "type", "1" },
					{ "limit", limit.ToString() }
				});
			}
			else {
				json = await NormalApi.SearchAsync(keywords, NormalApi.SearchType.Track, limit);
				isOk = true;
			}
			if (!isOk)
				throw new ApplicationException(nameof(CloudMusicApiProviders.Search) + " API错误");
			json = (JObject)json["result"];
			if (json is null)
				throw new ArgumentException($"\"{string.Join(" ", keywords)}\" 中有关键词被屏蔽");
			if ((int)json["songCount"] == 0)
				return Array.Empty<NcmTrack>();
			return json["songs"].Select(t => ParseTrack(t, false)).ToArray();
		}

		public static async Task<NcmAlbum[]> SearchAlbumAsync(Album album, int limit, bool withArtists) {
			List<string> keywords;
			bool isOk;
			JObject json;

			keywords = new List<string>();
			if (album.Name.Length != 0)
				keywords.Add(album.Name);
			if (withArtists)
				keywords.AddRange(album.Artists);
			if (keywords.Count == 0)
				throw new ArgumentException("专辑信息无效");
			for (int i = 0; i < keywords.Count; i++)
				keywords[i] = keywords[i].WholeWordReplace();
			if (_isLoggedIn) {
				(isOk, json) = await _api.RequestAsync(CloudMusicApiProviders.Search, new Dictionary<string, string> {
					{ "keywords", string.Join(" ", keywords) },
					{ "type", "10" },
					{ "limit", limit.ToString() }
				});
			}
			else {
				json = await NormalApi.SearchAsync(keywords, NormalApi.SearchType.Album, limit);
				isOk = true;
			}
			if (!isOk)
				throw new ApplicationException(nameof(CloudMusicApiProviders.Search) + " API错误");
			json = (JObject)json["result"];
			if (json is null)
				throw new ArgumentException($"\"{string.Join(" ", keywords)}\" 中有关键词被屏蔽");
			if ((int)json["albumCount"] == 0)
				return Array.Empty<NcmAlbum>();
			return json["albums"].Select(t => ParseAlbum(t)).ToArray();
		}

		public static async Task<NcmTrack[]> GetTracksAsync(int albumId) {
			if (_isLoggedIn) {
				bool isOk;
				JObject json;

				(isOk, json) = await _api.RequestAsync(CloudMusicApiProviders.Album, new Dictionary<string, string> {
					{ "id", albumId.ToString() }
				});
				if (!isOk)
					throw new ApplicationException(nameof(CloudMusicApiProviders.Album) + " API错误");
				return json["songs"].Select(t => ParseTrack(t, true)).ToArray();
			}
			else {
				JObject json;

				json = await NormalApi.GetAlbumAsync(albumId);
				return json["album"]["songs"].Select(t => ParseTrack(t, false)).ToArray();
			}
		}

		public static async Task<NcmLyric> GetLyricAsync(int trackId) {
			bool isOk;
			JObject json;
			Lrc rawLrc;
			int rawVersion;
			Lrc translatedLrc;
			int translatedVersion;

			if (_isLoggedIn) {
				(isOk, json) = await _api.RequestAsync(CloudMusicApiProviders.Lyric, new Dictionary<string, string> {
					{ "id", trackId.ToString() }
				});
			}
			else {
				json = await NormalApi.GetLyricAsync(trackId);
				isOk = true;
			}
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

			album = new Album((string)json["name"], ParseNames(json["artists"]), (int)json["size"], TimeStampToDateTime((long)json["publishTime"]).Year);
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

		private static DateTime TimeStampToDateTime(long timeStamp) {
			return new DateTime(1970, 1, 1).AddMilliseconds(timeStamp);
		}

		internal static class NormalApi {
			private const string SEARCH_URL = "http://music.163.com/api/search/pc";
			private const string ALBUM_URL = "http://music.163.com/api/album";
			private const string LYRIC_URL = "http://music.163.com/api/song/lyric";

			/// <summary>
			/// 搜索类型
			/// </summary>
			public enum SearchType {
				Track = 1,
				Album = 10
			}

			public static async Task<JObject> SearchAsync(IEnumerable<string> keywords, SearchType type, int limit) {
				QueryCollection queries;

				queries = new QueryCollection {
					{ "s", string.Join(" ", keywords) },
					{ "type", ((int)type).ToString() },
					{ "limit", limit.ToString() }
				};
				using (HttpClient client = new HttpClient())
				using (HttpResponseMessage response = await client.SendAsync(HttpMethod.Get, SEARCH_URL, queries, null)) {
					JObject json;

					if (!response.IsSuccessStatusCode)
						throw new HttpRequestException();
					json = JObject.Parse(await response.Content.ReadAsStringAsync());
					if ((int)json["code"] != 200)
						throw new HttpRequestException();
					return json;
				}
			}

			public static async Task<JObject> GetAlbumAsync(int id) {
				using (HttpClient client = new HttpClient())
				using (HttpResponseMessage response = await client.SendAsync(HttpMethod.Get, ALBUM_URL + "/" + id.ToString())) {
					JObject json;

					if (!response.IsSuccessStatusCode)
						throw new HttpRequestException();
					json = JObject.Parse(await response.Content.ReadAsStringAsync());
					if ((int)json["code"] != 200)
						throw new HttpRequestException();
					return json;
				}
			}

			public static async Task<JObject> GetLyricAsync(int id) {
				QueryCollection queries;

				queries = new QueryCollection {
					{ "id", id.ToString() },
					{ "lv", "-1" },
					{ "tv", "-1" }
				};
				using (HttpClient client = new HttpClient())
				using (HttpResponseMessage response = await client.SendAsync(HttpMethod.Get, LYRIC_URL, queries, null)) {
					JObject json;

					if (!response.IsSuccessStatusCode)
						throw new HttpRequestException();
					json = JObject.Parse(await response.Content.ReadAsStringAsync());
					if ((int)json["code"] != 200)
						throw new HttpRequestException();
					return json;
				}
			}
		}
	}
}
