using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NLyric.Audio;
using NLyric.Caches;
using NLyric.Lyrics;
using NLyric.Ncm;
using NLyric.Settings;

namespace NLyric {
	internal static partial class CliWorker {
		private static readonly SearchSettings _searchSettings = AllSettings.Default.Search;
		private static readonly FuzzySettings _fuzzySettings = AllSettings.Default.Fuzzy;
		private static readonly MatchSettings _matchSettings = AllSettings.Default.Match;
		private static readonly LyricSettings _lyricSettings = AllSettings.Default.Lyric;
		private static readonly Dictionary<string, NcmAlbum> _cachedNcmAlbums = new Dictionary<string, NcmAlbum>();
		// AlbumName -> Album
		private static readonly Dictionary<int, NcmTrack[]> _cachedNcmTrackses = new Dictionary<int, NcmTrack[]>();
		// AlbumId -> Tracks
		private static readonly Dictionary<int, NcmLyric> _cachedNcmLyrics = new Dictionary<int, NcmLyric>();
		// TrackId -> Lyric
		private static AllCaches _allCaches;

		public static async Task ExecuteAsync(CliArguments arguments) {
			Logger.Instance.LogInfo("程序会自动过滤相似度为0的结果与歌词未被收集的结果！！！", ConsoleColor.Green);
			Logger.Instance.LogNewLine();
			LoadLocalCaches(arguments.Directory);
			foreach (string audioPath in Directory.EnumerateFiles(arguments.Directory, "*", SearchOption.AllDirectories)) {
				string lrcPath;
				int? trackId;

				lrcPath = Path.ChangeExtension(audioPath, ".lrc");
				if (CanSkip(audioPath, lrcPath))
					continue;
				Logger.Instance.LogInfo($"开始搜索文件\"{Path.GetFileName(audioPath)}\"的歌词。");
				trackId = await TryGetMusicId(audioPath);
				// 同时尝试通过163Key和专辑获取歌曲信息
				if (trackId == null)
					Logger.Instance.LogWarning($"无法找到文件\"{Path.GetFileName(audioPath)}\"的网易云音乐ID！");
				else
					await WriteLrcAsync(trackId.Value, lrcPath);
				Logger.Instance.LogNewLine();
				Logger.Instance.LogNewLine();
			}
			SaveLocalCaches(arguments.Directory);
		}

		private static bool CanSkip(string audioPath, string lrcPath) {
			string extension;

			extension = Path.GetExtension(audioPath);
			if (!IsAudioFile(extension))
				return true;
			if (File.Exists(lrcPath) && !_lyricSettings.AutoUpdate && !_lyricSettings.Overwriting) {
				Logger.Instance.LogInfo($"文件\"{Path.GetFileName(audioPath)}\"的歌词已存在，并且自动更新与覆盖已被禁止，正在跳过。");
				return true;
			}
			return false;
		}

		private static bool IsAudioFile(string extension) {
			return _searchSettings.AudioExtensions.Any(s => extension.Equals(s, StringComparison.OrdinalIgnoreCase));
		}

		private static async Task<int?> TryGetMusicId(string audioPath) {
			int? trackId;
			ATL.Track atlTrack;
			Track track;
			Album album;
			NcmTrack ncmTrack;

			trackId = null;
			atlTrack = new ATL.Track(audioPath);
			track = new Track(atlTrack);
			album = Album.HasAlbumInfo(atlTrack) ? new Album(atlTrack, true) : null;
			try {
				// 歌曲无163Key，通过自己的算法匹配
				ncmTrack = await MapToAsync(track, album, audioPath);
				if (ncmTrack != null) {
					trackId = ncmTrack.Id;
					Logger.Instance.LogInfo($"已获取文件\"{Path.GetFileName(audioPath)}\"的网易云音乐ID: {trackId}。");
				}
			}
			catch (Exception ex) {
				Logger.Instance.LogException(ex);
			}
			return trackId;
		}

		private static async Task WriteLrcAsync(int trackId, string lrcPath) {
			LyricCache lyricCache;
			bool hasLrcFile;
			string lyricCheckSum;
			NcmLyric ncmLyric;
			Lrc lrc;

			lyricCache = _allCaches.LyricCaches.Match(trackId);
			hasLrcFile = File.Exists(lrcPath);
			lyricCheckSum = hasLrcFile ? ComputeLyricCheckSum(File.ReadAllText(lrcPath)) : null;
			try {
				ncmLyric = await GetLyricAsync(trackId);
			}
			catch (Exception ex) {
				Logger.Instance.LogException(ex);
				return;
			}
			if (hasLrcFile) {
				// 如果歌词存在，判断是否需要覆盖或更新
				if (lyricCache != null && lyricCache.CheckSum == lyricCheckSum) {
					// 歌词由NLyric创建
					if (ncmLyric.RawVersion <= lyricCache.RawVersion && ncmLyric.TranslatedVersion <= lyricCache.TranslatedVersion) {
						// 是最新版本
						Logger.Instance.LogInfo("本地歌词已是最新版本，正在跳过。", ConsoleColor.Green);
						return;
					}
					else {
						// 不是最新版本
						if (!_lyricSettings.AutoUpdate) {
							Logger.Instance.LogInfo("本地歌词不是最新版本但是自动更新被禁止，正在跳过。", ConsoleColor.Yellow);
							return;
						}
					}
				}
				else {
					// 歌词非NLyric创建
					if (!_lyricSettings.Overwriting) {
						Logger.Instance.LogInfo("本地歌词已存在并且非NLyric创建，正在跳过。", ConsoleColor.Yellow);
						return;
					}
				}
			}
			lrc = ToLrc(ncmLyric);
			if (lrc != null) {
				string lyric;

				lyric = lrc.ToString();
				UpdateCache(ncmLyric, ComputeLyricCheckSum(lyric));
				File.WriteAllText(lrcPath, lyric);
				Logger.Instance.LogInfo("本地歌词下载完毕。");
			}
		}

		#region mapping
		/// <summary>
		/// 同时根据专辑信息以及歌曲信息获取网易云音乐上的歌曲
		/// </summary>
		/// <param name="track"></param>
		/// <param name="album"></param>
		/// <param name="audioPath"></param>
		/// <returns></returns>
		private static async Task<NcmTrack> MapToAsync(Track track, Album album, string audioPath) {
			if (track == null)
				throw new ArgumentNullException(nameof(track));
			if (audioPath == null)
				throw new ArgumentNullException(nameof(audioPath));

			string fileName;
			TrackCache trackCache;
			int trackId;
			NcmTrack ncmTrack;

			fileName = Path.GetFileName(audioPath);
			trackCache = album == null ? _allCaches.TrackCaches.Match(track, fileName) : _allCaches.TrackCaches.Match(track, album);
			// 有专辑信息就用专辑信息，没有专辑信息就用文件名
			if (trackCache != null)
				return new NcmTrack(track, trackCache.Id);
			// 先尝试从缓存获取歌曲
			if (The163KeyHelper.TryGetMusicId(audioPath, out trackId)) {
				// 尝试从163Key获取ID
				ncmTrack = new NcmTrack(track, trackId);
			}
			else {
				NcmAlbum ncmAlbum;

				ncmAlbum = null;
				if (album != null) {
					// 存在专辑信息，尝试获取网易云音乐上对应的专辑
					AlbumCache albumCache;

					albumCache = _allCaches.AlbumCaches.Match(album);
					if (albumCache != null)
						ncmAlbum = new NcmAlbum(album, albumCache.Id);
					// 先尝试从缓存获取专辑
					if (ncmAlbum == null) {
						ncmAlbum = await MapToAsync(album);
						if (ncmAlbum != null)
							UpdateCache(album, ncmAlbum.Id);
					}
				}
				if (ncmAlbum == null) {
					// 没有对应的专辑信息，使用无专辑匹配
					ncmTrack = await MapToAsync(track);
				}
				else {
					// 网易云音乐收录了歌曲所在专辑
					NcmTrack[] ncmTracks;

					ncmTracks = (await GetTracksAsync(ncmAlbum)).Where(t => ComputeSimilarity(t.Name, track.Name, false) != 0).ToArray();
					// 获取网易云音乐上专辑收录的歌曲
					ncmTrack = MatchByUser(ncmTracks, track);
					if (ncmTrack == null)
						// 网易云音乐上的专辑可能没收录这个歌曲，不清楚为什么，但是确实存在这个情况，比如专辑id:3094396
						ncmTrack = await MapToAsync(track);
				}
			}
			if (ncmTrack == null)
				Logger.Instance.LogWarning("歌曲匹配失败！");
			else {
				Logger.Instance.LogInfo("歌曲匹配成功！");
				if (album == null)
					UpdateCache(track, fileName, ncmTrack.Id);
				else
					UpdateCache(track, album, ncmTrack.Id);
			}
			return ncmTrack;
		}

		private static async Task<NcmTrack[]> GetTracksAsync(NcmAlbum ncmAlbum) {
			NcmTrack[] ncmTracks;

			if (!_cachedNcmTrackses.TryGetValue(ncmAlbum.Id, out ncmTracks)) {
				List<NcmTrack> list;

				list = new List<NcmTrack>();
				foreach (NcmTrack item in await CloudMusic.GetTracksAsync(ncmAlbum.Id))
					if ((await GetLyricAsync(item.Id)).IsCollected)
						list.Add(item);
				ncmTracks = list.ToArray();
				_cachedNcmTrackses[ncmAlbum.Id] = ncmTracks;
			}
			return ncmTracks;
		}

		/// <summary>
		/// 获取网易云音乐上的歌曲，自动尝试带艺术家与不带艺术家搜索
		/// </summary>
		/// <param name="track"></param>
		/// <returns></returns>
		private static async Task<NcmTrack> MapToAsync(Track track) {
			if (track == null)
				throw new ArgumentNullException(nameof(track));

			NcmTrack ncmTrack;

			Logger.Instance.LogInfo($"开始搜索歌曲\"{track}\"。");
			Logger.Instance.LogWarning("正在尝试带艺术家搜索，结果可能将过少！");
			ncmTrack = await MapToAsync(track, true);
			if (ncmTrack == null && _fuzzySettings.TryIgnoringArtists) {
				Logger.Instance.LogWarning("正在尝试忽略艺术家搜索，结果可能将不精确！");
				ncmTrack = await MapToAsync(track, false);
			}
			return ncmTrack;
		}

		/// <summary>
		/// 获取网易云音乐上的专辑，自动尝试带艺术家与不带艺术家搜索
		/// </summary>
		/// <param name="album"></param>
		/// <returns></returns>
		private static async Task<NcmAlbum> MapToAsync(Album album) {
			if (album == null)
				throw new ArgumentNullException(nameof(album));

			string replacedAlbumName;
			NcmAlbum ncmAlbum;

			replacedAlbumName = album.Name.ReplaceEx();
			if (_cachedNcmAlbums.TryGetValue(replacedAlbumName, out ncmAlbum))
				return ncmAlbum;
			Logger.Instance.LogInfo($"开始搜索专辑\"{album}\"。");
			Logger.Instance.LogWarning("正在尝试带艺术家搜索，结果可能将过少！");
			ncmAlbum = await MapToAsync(album, true);
			if (ncmAlbum == null && _fuzzySettings.TryIgnoringArtists) {
				Logger.Instance.LogWarning("正在尝试忽略艺术家搜索，结果可能将不精确！");
				ncmAlbum = await MapToAsync(album, false);
			}
			if (ncmAlbum == null) {
				Logger.Instance.LogWarning("专辑匹配失败！");
				_cachedNcmAlbums[replacedAlbumName] = null;
				return null;
			}
			Logger.Instance.LogInfo("专辑匹配成功！");
			_cachedNcmAlbums[replacedAlbumName] = ncmAlbum;
			return ncmAlbum;
		}

		/// <summary>
		/// 获取网易云音乐上的歌曲
		/// </summary>
		/// <param name="track"></param>
		/// <param name="withArtists">是否带艺术家搜索</param>
		/// <returns></returns>
		private static async Task<NcmTrack> MapToAsync(Track track, bool withArtists) {
			List<NcmTrack> list;
			NcmTrack[] ncmTracks;

			list = new List<NcmTrack>();
			foreach (NcmTrack item in (await CloudMusic.SearchTrackAsync(track, _searchSettings.Limit, withArtists)).Where(t => ComputeSimilarity(t.Name, track.Name, false) != 0))
				if ((await GetLyricAsync(item.Id)).IsCollected)
					list.Add(item);
			ncmTracks = list.ToArray();
			return MatchByUser(ncmTracks, track);
		}

		/// <summary>
		/// 获取网易云音乐上的专辑
		/// </summary>
		/// <param name="album"></param>
		/// <param name="withArtists">是否带艺术家搜索</param>
		/// <returns></returns>
		private static async Task<NcmAlbum> MapToAsync(Album album, bool withArtists) {
			NcmAlbum[] ncmAlbums;

			ncmAlbums = (await CloudMusic.SearchAlbumAsync(album, _searchSettings.Limit, withArtists)).Where(t => ComputeSimilarity(t.Name, album.Name, false) != 0).ToArray();
			return MatchByUser(ncmAlbums, album);
		}
		#endregion

		#region local cache
		private static void LoadLocalCaches(string directoryPath) {
			string cachePath;

			cachePath = Path.Combine(directoryPath, ".nlyric");
			if (File.Exists(cachePath)) {
				_allCaches = JsonConvert.DeserializeObject<AllCaches>(File.ReadAllText(cachePath));
				Logger.Instance.LogInfo($"搜索缓存\"{cachePath}\"已被加载。");
			}
			else
				_allCaches = new AllCaches() {
					AlbumCaches = new List<AlbumCache>(),
					LyricCaches = new List<LyricCache>(),
					TrackCaches = new List<TrackCache>()
				};
		}

		private static void SaveLocalCaches(string directoryPath) {
			string cachePath;

			cachePath = Path.Combine(directoryPath, ".nlyric");
			_allCaches.AlbumCaches.Sort((x, y) => x.Name.CompareTo(y.Name));
			_allCaches.TrackCaches.Sort((x, y) => x.Name.CompareTo(y.Name));
			_allCaches.LyricCaches.Sort((x, y) => x.Id.CompareTo(y.Id));
			File.WriteAllText(cachePath, FormatJson(JsonConvert.SerializeObject(_allCaches)));
			Logger.Instance.LogInfo($"搜索缓存\"{cachePath}\"已被保存。");
		}

		private static string FormatJson(string json) {
			using (StringWriter writer = new StringWriter())
			using (JsonTextWriter jsonWriter = new JsonTextWriter(writer) { Formatting = Formatting.Indented })
			using (StringReader reader = new StringReader(json))
			using (JsonTextReader jsonReader = new JsonTextReader(reader)) {
				jsonWriter.WriteToken(jsonReader);
				return writer.ToString();
			}
		}

		private static void UpdateCache(Album album, int id) {
			AlbumCache cache;

			cache = _allCaches.AlbumCaches.Match(album);
			if (cache == null)
				_allCaches.AlbumCaches.Add(new AlbumCache(album, id));
		}

		private static void UpdateCache(Track track, Album album, int id) {
			TrackCache cache;

			cache = _allCaches.TrackCaches.Match(track, album);
			if (cache == null)
				_allCaches.TrackCaches.Add(new TrackCache(track, album, id));
		}

		private static void UpdateCache(Track track, string fileName, int id) {
			TrackCache cache;

			cache = _allCaches.TrackCaches.Match(track, fileName);
			if (cache == null)
				_allCaches.TrackCaches.Add(new TrackCache(track, fileName, id));
		}

		private static void UpdateCache(NcmLyric lyric, string checkSum) {
			int index;

			index = _allCaches.LyricCaches.FindIndex(t => t.IsMatched(lyric.Id));
			if (index != -1)
				_allCaches.LyricCaches.RemoveAt(index);
			_allCaches.LyricCaches.Add(new LyricCache(lyric, checkSum));
		}
		#endregion

		#region match
		private static TSource MatchByUser<TSource, TTarget>(TSource[] sources, TTarget target) where TSource : class, ITrackOrAlbum where TTarget : class, ITrackOrAlbum {
			TSource result;

			if (sources.Length == 0)
				return null;
			result = MatchByUser(sources, target, false);
			if (result == null && _fuzzySettings.TryIgnoringExtraInfo)
				result = MatchByUser(sources, target, true);
			return result;
		}

		private static TSource MatchByUser<TSource, TTarget>(TSource[] sources, TTarget target, bool fuzzy) where TSource : class, ITrackOrAlbum where TTarget : class, ITrackOrAlbum {
			Dictionary<TSource, double> nameSimilarities;
			TSource result;
			bool isExact;

			if (sources.Length == 0)
				return null;
			nameSimilarities = new Dictionary<TSource, double>();
			foreach (TSource source in sources)
				nameSimilarities[source] = ComputeSimilarity(source.Name, target.Name, fuzzy);
			result = Match(sources, target, nameSimilarities, out isExact);
			if (result != null && (isExact || Confirm("不完全相似，是否使用自动匹配结果？")))
				// 自动匹配成功，如果是完全匹配，不需要用户再次确认，反正由用户再次确认
				return result;
			return fuzzy ? Select(sources.OrderByDescending(t => t, new DictionaryComparer<TSource, double>(nameSimilarities)).ToArray(), target, nameSimilarities) : null;
			// fuzzy为true时是第二次搜索了，再让用户再次手动从搜索结果中选择，自动匹配失败的原因可能是 Settings.Match.MinimumSimilarity 设置太大了
		}

		private static TSource Match<TSource, TTarget>(IEnumerable<TSource> sources, TTarget target, Dictionary<TSource, double> nameSimilarities, out bool isExact) where TSource : class, ITrackOrAlbum where TTarget : class, ITrackOrAlbum {
			foreach (TSource source in sources) {
				double nameSimilarity;

				nameSimilarity = nameSimilarities[source];
				if (nameSimilarity < _matchSettings.MinimumSimilarity)
					continue;
				foreach (string ncmArtist in source.Artists)
					foreach (string artist in target.Artists)
						if (ComputeSimilarity(ncmArtist, artist, false) >= _matchSettings.MinimumSimilarity) {
							Logger.Instance.LogInfo(
								"自动匹配结果：" + Environment.NewLine +
								"网易云音乐：" + source.ToString() + Environment.NewLine +
								"本地：" + target.ToString() + Environment.NewLine +
								"相似度：" + nameSimilarity.ToString());
							isExact = nameSimilarity == 1;
							return source;
						}
			}
			isExact = false;
			return null;
		}

		private static TSource Select<TSource, TTarget>(TSource[] sources, TTarget target, Dictionary<TSource, double> nameSimilarities) where TSource : class, ITrackOrAlbum where TTarget : class, ITrackOrAlbum {
			TSource result;

			Logger.Instance.LogInfo("请手动输入1,2,3...选择匹配的项，若不存在，请输入Pass。");
			Logger.Instance.LogInfo("对比项：" + TrackOrAlbumToString(target));
			for (int i = 0; i < sources.Length; i++) {
				double nameSimilarity;
				string text;

				nameSimilarity = nameSimilarities[sources[i]];
				text = $"{i + 1}. {sources[i]} (s:{nameSimilarity.ToString("F2")})";
				if (nameSimilarity >= 0.85)
					Logger.Instance.LogInfo(text, ConsoleColor.Green);
				else if (nameSimilarity >= 0.5)
					Logger.Instance.LogInfo(text, ConsoleColor.Yellow);
				else
					Logger.Instance.LogInfo(text);
			}
			result = null;
			do {
				string userInput;
				int index;

				userInput = Console.ReadLine().Trim();
				if (userInput.ToUpperInvariant() == "PASS")
					break;
				if (int.TryParse(userInput, out index)) {
					index -= 1;
					if (index >= 0 && index < sources.Length) {
						result = sources[index];
						break;
					}
				}
				Logger.Instance.LogWarning("输入有误，请重新输入！");
			} while (true);
			if (result != null)
				Logger.Instance.LogInfo("已选择：" + result.ToString());
			return result;

			string TrackOrAlbumToString(ITrackOrAlbum trackOrAlbum) {
				if (trackOrAlbum.Artists.Length == 0)
					return trackOrAlbum.Name;
				return trackOrAlbum.Name + " by " + string.Join(",", trackOrAlbum.Artists);
			}
		}

		private static bool Confirm(string text) {
			Logger.Instance.LogInfo(text);
			Logger.Instance.LogInfo("请手动输入Yes或No。");
			do {
				string userInput;

				userInput = Console.ReadLine().Trim().ToUpperInvariant();
				switch (userInput) {
				case "YES":
					return true;
				case "NO":
					return false;
				}
				Logger.Instance.LogWarning("输入有误，请重新输入！");
			} while (true);
		}

		private static double ComputeSimilarity(string x, string y, bool fuzzy) {
			x = x.ReplaceEx();
			y = y.ReplaceEx();
			if (fuzzy) {
				x = x.Fuzzy();
				y = y.Fuzzy();
			}
			x = x.Trim();
			y = y.Trim();
			return Levenshtein.Compute(x, y);
		}
		#endregion

		#region lyrics
		private static async Task<NcmLyric> GetLyricAsync(int trackId) {
			NcmLyric lyric;

			if (!_cachedNcmLyrics.TryGetValue(trackId, out lyric)) {
				lyric = await CloudMusic.GetLyricAsync(trackId);
				_cachedNcmLyrics[trackId] = lyric;
			}
			return lyric;
		}

		private static Lrc ToLrc(NcmLyric lyric) {
			if (!lyric.IsCollected) {
				Logger.Instance.LogWarning("当前歌曲的歌词未被收录！");
				return null;
			}
			if (lyric.IsAbsoluteMusic) {
				Logger.Instance.LogWarning("当前歌曲是纯音乐无歌词！");
				return null;
			}
			foreach (string mode in _lyricSettings.Modes) {
				switch (mode.ToUpperInvariant()) {
				case "MERGED":
					if (lyric.Raw == null || lyric.Translated == null)
						continue;
					Logger.Instance.LogInfo("已获取混合歌词。");
					return MergeLyric(lyric.Raw, lyric.Translated);
				case "RAW":
					if (lyric.Raw == null)
						continue;
					Logger.Instance.LogInfo("已获取原始歌词。");
					return lyric.Raw;
				case "TRANSLATED":
					if (lyric.Translated == null)
						continue;
					Logger.Instance.LogInfo("已获取翻译歌词。");
					return lyric.Translated;
				default:
					throw new ArgumentOutOfRangeException(nameof(mode));
				}
			}
			Logger.Instance.LogWarning("获取歌词失败（可能歌曲是纯音乐但是未被网易云音乐标记为纯音乐）。");
			return null;
		}

		private static Lrc MergeLyric(Lrc rawLrc, Lrc translatedLrc) {
			if (rawLrc == null)
				throw new ArgumentNullException(nameof(rawLrc));
			if (translatedLrc == null)
				throw new ArgumentNullException(nameof(translatedLrc));

			Lrc mergedLrc;

			mergedLrc = new Lrc {
				Offset = rawLrc.Offset,
				Title = rawLrc.Title
			};
			foreach (KeyValuePair<TimeSpan, string> rawLyric in rawLrc.Lyrics)
				mergedLrc.Lyrics.Add(rawLyric.Key, rawLyric.Value);
			foreach (KeyValuePair<TimeSpan, string> translatedLyric in translatedLrc.Lyrics) {
				string rawLyric;

				if (translatedLyric.Value.Length == 0)
					// 如果翻译歌词是空字符串，跳过
					continue;
				if (!mergedLrc.Lyrics.ContainsKey(translatedLyric.Key)) {
					// 如果没有对应的未翻译字符串，直接添加
					mergedLrc.Lyrics.Add(translatedLyric.Key, translatedLyric.Value);
					continue;
				}
				rawLyric = mergedLrc.Lyrics[translatedLyric.Key];
				if (rawLyric.Length == 0)
					// 如果未翻译歌词是空字符串，表示上一句歌词的结束，那么跳过
					continue;
				mergedLrc.Lyrics[translatedLyric.Key] = $"{rawLyric} 「{translatedLyric.Value}」";
			}
			return mergedLrc;
		}

		private static string ComputeLyricCheckSum(string lyric) {
			return Crc32.Compute(Encoding.Unicode.GetBytes(lyric)).ToString("X8");
		}
		#endregion
	}
}
