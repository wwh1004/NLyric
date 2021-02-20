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
	public sealed class CloudMusic {
		private readonly CloudMusicApi _api = new CloudMusicApi();

		public CloudMusicApi Api => _api;

		public async Task<bool> LoginAsync(string account, string password) {
			var queries = new Dictionary<string, object>();
			bool isPhone = Regex.Match(account, "^[0-9]+$").Success;
			queries[isPhone ? "phone" : "email"] = account;
			queries["password"] = password;
			var (result, _) = await _api.RequestAsync(isPhone ? CloudMusicApiProviders.LoginCellphone : CloudMusicApiProviders.Login, queries);
			return result;
		}

		public async Task<NcmTrack[]> SearchTrackAsync(Track track, int limit, bool withArtists) {
			var keywords = new List<string>();
			if (track.Name.Length != 0)
				keywords.Add(track.Name);
			if (withArtists)
				keywords.AddRange(track.Artists);
			if (keywords.Count == 0)
				throw new ArgumentException("歌曲信息无效");
			for (int i = 0; i < keywords.Count; i++)
				keywords[i] = keywords[i].WholeWordReplace();
			var (isOk, json) = await _api.RequestAsync(CloudMusicApiProviders.Search, new Dictionary<string, object> {
				{ "keywords", string.Join(" ", keywords) },
				{ "type", 1 },
				{ "limit", limit }
			});
			if (!isOk)
				throw new ApplicationException(nameof(CloudMusicApiProviders.Search) + " API错误");
			if ((JObject)json["result"] is null)
				throw new KeywordForbiddenException(string.Join(" ", keywords));
			return ParseSearchTracks(json);
		}

		public NcmTrack[] ParseSearchTracks(JObject json) {
			json = (JObject)json["result"];
			if (!(json["songs"] is JArray songs))
				return Array.Empty<NcmTrack>();
			return songs.Select(t => ParseTrack(t, false)).ToArray();
		}

		public async Task<NcmAlbum[]> SearchAlbumAsync(Album album, int limit, bool withArtists) {
			var keywords = new List<string>();
			if (album.Name.Length != 0)
				keywords.Add(album.Name);
			if (withArtists)
				keywords.AddRange(album.Artists);
			if (keywords.Count == 0)
				throw new ArgumentException("专辑信息无效");
			for (int i = 0; i < keywords.Count; i++)
				keywords[i] = keywords[i].WholeWordReplace();
			var (isOk, json) = await _api.RequestAsync(CloudMusicApiProviders.Search, new Dictionary<string, object> {
				{ "keywords", string.Join(" ", keywords) },
				{ "type", 10 },
				{ "limit", limit }
			});
			if (!isOk)
				throw new ApplicationException(nameof(CloudMusicApiProviders.Search) + " API错误");
			if ((JObject)json["result"] is null)
				throw new KeywordForbiddenException(string.Join(" ", keywords));
			return ParseSearchAlbums(json);
		}

		public NcmAlbum[] ParseSearchAlbums(JObject json) {
			json = (JObject)json["result"];
			if (!(json["albums"] is JArray albums))
				return Array.Empty<NcmAlbum>();
			// albumCount不可信，搜索"U-87 陈奕迅"返回albums有内容，但是albumCount为0
			return albums.Select(t => ParseAlbum(t)).ToArray();
		}

		public async Task<NcmTrack[]> GetTracksAsync(int albumId) {
			var (isOk, json) = await _api.RequestAsync(CloudMusicApiProviders.Album, new Dictionary<string, object> {
				{ "id", albumId }
			});
			if (!isOk)
				throw new ApplicationException(nameof(CloudMusicApiProviders.Album) + " API错误");
			return ParseTracks(json);
		}

		public NcmTrack[] ParseTracks(JObject json) {
			return json["songs"].Select(t => ParseTrack(t, true)).ToArray();
		}

		public async Task<NcmLyric> GetLyricAsync(int trackId) {
			var (isOk, json) = await _api.RequestAsync(CloudMusicApiProviders.Lyric, new Dictionary<string, object> {
				{ "id", trackId }
			});
			if (!isOk)
				throw new ApplicationException(nameof(CloudMusicApiProviders.Lyric) + " API错误");
			return ParseLyric(trackId, json);
		}

		public NcmLyric ParseLyric(int trackId, JObject json) {
			if (json is null)
				throw new ArgumentNullException(nameof(json));

			if ((bool?)json["uncollected"] == true)
				return new NcmLyric(trackId, false, false, null, 0, null, 0);
			// 未收录
			if ((bool?)json["nolyric"] == true)
				return new NcmLyric(trackId, true, true, null, 0, null, 0);
			// 纯音乐
			var (rawLrc, rawVersion) = ParseLyric(json["lrc"]);
			var (translatedLrc, translatedVersion) = ParseLyric(json["tlyric"]);
			return new NcmLyric(trackId, true, false, rawLrc, rawVersion, translatedLrc, translatedVersion);
		}

		private NcmAlbum ParseAlbum(JToken json) {
			var album = new Album((string)json["name"], ParseNames(json["artists"]));
			var ncmAlbum = new NcmAlbum(album, (int)json["id"]);
			return ncmAlbum;
		}

		private NcmTrack ParseTrack(JToken json, bool isShortName) {
			var track = new Track((string)json["name"], ParseNames(json[isShortName ? "ar" : "artists"]));
			var ncmTrack = new NcmTrack(track, (int)json["id"]);
			return ncmTrack;
		}

		private string[] ParseNames(JToken json) {
			return json.Select(t => (string)t["name"]).ToArray();
		}

		private (Lrc, int) ParseLyric(JToken json) {
			string lyric = (string)json["lyric"];
			var lrc = string.IsNullOrEmpty(lyric) ? null : Lrc.UnsafeParse(lyric);
			int version = (int)json["version"];
			return (lrc, version);
		}
	}
}
