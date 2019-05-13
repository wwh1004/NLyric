using System.Collections.Generic;
using System.Extensions;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace NLyric.Ncm {
	/// <summary>
	/// 网易云音乐API，内容不完整，可根据 https://binaryify.github.io/NeteaseCloudMusicApi 自己添加
	/// </summary>
	public static class NcmApi {
		private const string SEARCH_URL = "http://musicapi.leanapp.cn/search";
		private const string ALBUM_URL = "http://musicapi.leanapp.cn/album";
		private const string LYRIC_URL = "http://music.163.com/api/song/lyric";

		/// <summary>
		/// 搜索类型
		/// </summary>
		public enum SearchType {
			Track = 1,
			Album = 10,
			Artist = 100,
			Playlist = 1000
		}

		public static async Task<JToken> SearchAsync(IEnumerable<string> keywords, SearchType type, int limit) {
			FormUrlEncodedCollection parameters;

			parameters = new FormUrlEncodedCollection {
				{ "keywords", string.Join(" ", keywords) },
				{ "type", ((int)type).ToString() },
				{ "limit", limit.ToString() }
			};
			using (HttpClient client = new HttpClient())
			using (HttpResponseMessage response = await client.SendAsync(HttpMethod.Get, SEARCH_URL, parameters, null)) {
				JObject json;

				json = JObject.Parse(await response.Content.ReadAsStringAsync());
				if ((int)json["code"] != 200)
					throw new HttpRequestException();
				return json["result"];
			}
		}

		public static async Task<JToken> GetAlbumAsync(int id) {
			FormUrlEncodedCollection parameters;

			parameters = new FormUrlEncodedCollection {
				{ "id", id.ToString() }
			};
			using (HttpClient client = new HttpClient())
			using (HttpResponseMessage response = await client.SendAsync(HttpMethod.Get, ALBUM_URL, parameters, null)) {
				JObject json;

				json = JObject.Parse(await response.Content.ReadAsStringAsync());
				if ((int)json["code"] != 200)
					throw new HttpRequestException();
				return json;
			}
		}

		public static async Task<JToken> GetLyricAsync(int id) {
			FormUrlEncodedCollection parameters;

			parameters = new FormUrlEncodedCollection {
				{ "id", id.ToString() },
				{ "lv", "-1" },
				{ "tv", "-1" }
			};
			using (HttpClient client = new HttpClient())
			using (HttpResponseMessage response = await client.SendAsync(HttpMethod.Get, LYRIC_URL, parameters, null)) {
				JObject json;

				json = JObject.Parse(await response.Content.ReadAsStringAsync());
				if ((int)json["code"] != 200)
					throw new HttpRequestException();
				return json;
			}
		}
	}
}
