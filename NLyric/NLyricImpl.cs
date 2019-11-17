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
	internal static class NLyricImpl {
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
		private static string _allCachesPath;
		private static AllCaches _allCaches;

		public static async Task ExecuteAsync(Arguments arguments) {
			await LoginIfNeedAsync(arguments);
			_allCachesPath = Path.Combine(arguments.Directory, ".nlyric");
			LoadLocalCaches();
			foreach (string audioPath in Directory.EnumerateFiles(arguments.Directory, "*", SearchOption.AllDirectories)) {
				string lrcPath;
				int? trackId;

				lrcPath = Path.ChangeExtension(audioPath, ".lrc");
				if (CanSkip(audioPath, lrcPath))
					continue;
				Logger.Instance.LogInfo($"开始搜索文件\"{Path.GetFileName(audioPath)}\"的歌词。");
				trackId = await TryGetMusicId(audioPath);
				// 同时尝试通过163Key和专辑获取歌曲信息
				if (trackId is null)
					Logger.Instance.LogWarning($"无法找到文件\"{Path.GetFileName(audioPath)}\"的网易云音乐ID！");
				else
					await WriteLrcAsync(trackId.Value, lrcPath);
				Logger.Instance.LogNewLine();
				Logger.Instance.LogNewLine();
			}
			SaveLocalCaches();
		}

		private static async Task LoginIfNeedAsync(Arguments arguments) {
			if (string.IsNullOrEmpty(arguments.Account) || string.IsNullOrEmpty(arguments.Password)) {
				for (int i = 0; i < 3; i++)
					Logger.Instance.LogInfo("登录可避免出现大部分API错误！！！当前是免登录状态，若软件出错请尝试登录！！！", ConsoleColor.Green);
				Logger.Instance.LogInfo("强烈建议登录使用软件：\"NLyric.exe -d C:\\Music -a example@example.com -p 123456\"", ConsoleColor.Green);
			}
			else {
				Logger.Instance.LogInfo("登录中...", ConsoleColor.Green);
				if (await CloudMusic.LoginAsync(arguments.Account, arguments.Password))
					Logger.Instance.LogInfo("登录成功！", ConsoleColor.Green);
				else {
					Logger.Instance.LogError("登录失败，输入任意键以免登录模式运行或重新运行尝试再次登录！");
					try {
						Console.ReadKey(true);
					}
					catch {
					}
				}
			}
			Logger.Instance.LogNewLine();
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
				if (!(ncmTrack is null)) {
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
				if (!(lyricCache is null) && lyricCache.CheckSum == lyricCheckSum) {
					// 歌词由NLyric创建
					if (ncmLyric.RawVersion <= lyricCache.RawVersion && ncmLyric.TranslatedVersion <= lyricCache.TranslatedVersion) {
						// 是最新版本
						Logger.Instance.LogInfo("本地歌词已是最新版本，正在跳过。", ConsoleColor.Yellow);
						return;
					}
					else {
						// 不是最新版本
						if (_lyricSettings.AutoUpdate)
							Logger.Instance.LogInfo("本地歌词不是最新版本，正在更新。", ConsoleColor.Green);
						else {
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
			if (!(lrc is null)) {
				string lyric;

				lyric = lrc.ToString();
				UpdateCache(ncmLyric, ComputeLyricCheckSum(lyric));
				File.WriteAllText(lrcPath, lyric);
				Logger.Instance.LogInfo("本地歌词下载完毕。", ConsoleColor.Magenta);
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
			if (track is null)
				throw new ArgumentNullException(nameof(track));
			if (audioPath is null)
				throw new ArgumentNullException(nameof(audioPath));

			string fileName;
			TrackCache trackCache;
			int trackId;
			NcmTrack ncmTrack;

			fileName = Path.GetFileName(audioPath);
			trackCache = album is null ? _allCaches.TrackCaches.Match(track, fileName) : _allCaches.TrackCaches.Match(track, album);
			// 有专辑信息就用专辑信息，没有专辑信息就用文件名
			if (!(trackCache is null))
				return new NcmTrack(track, trackCache.Id);
			// 先尝试从缓存获取歌曲
			if (The163KeyHelper.TryGetMusicId(audioPath, out trackId)) {
				// 尝试从163Key获取ID
				ncmTrack = new NcmTrack(track, trackId);
			}
			else {
				NcmAlbum ncmAlbum;

				ncmAlbum = null;
				if (!(album is null)) {
					// 存在专辑信息，尝试获取网易云音乐上对应的专辑
					AlbumCache albumCache;

					albumCache = _allCaches.AlbumCaches.Match(album);
					if (!(albumCache is null))
						ncmAlbum = new NcmAlbum(album, albumCache.Id);
					// 先尝试从缓存获取专辑
					if (ncmAlbum is null) {
						ncmAlbum = await MapToAsync(album);
						if (!(ncmAlbum is null))
							UpdateCache(album, ncmAlbum.Id);
					}
				}
				if (ncmAlbum is null) {
					// 没有对应的专辑信息，使用无专辑匹配
					ncmTrack = await MapToAsync(track);
				}
				else {
					// 网易云音乐收录了歌曲所在专辑
					NcmTrack[] ncmTracks;

					ncmTracks = (await GetTracksAsync(ncmAlbum)).Where(t => ComputeSimilarity(t.Name, track.Name, false) != 0).ToArray();
					// 获取网易云音乐上专辑收录的歌曲
					ncmTrack = MatchByUser(ncmTracks, track);
					if (ncmTrack is null)
						// 网易云音乐上的专辑可能没收录这个歌曲，不清楚为什么，但是确实存在这个情况，比如专辑id:3094396
						ncmTrack = await MapToAsync(track);
				}
			}
			if (ncmTrack is null)
				Logger.Instance.LogWarning("歌曲匹配失败！");
			else {
				Logger.Instance.LogInfo("歌曲匹配成功！");
				if (album is null)
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
			if (track is null)
				throw new ArgumentNullException(nameof(track));

			NcmTrack ncmTrack;

			Logger.Instance.LogInfo($"开始搜索歌曲\"{track}\"。");
			Logger.Instance.LogWarning("正在尝试带艺术家搜索，结果可能将过少！");
			ncmTrack = await MapToAsync(track, true);
			if (ncmTrack is null && _fuzzySettings.TryIgnoringArtists) {
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
			if (album is null)
				throw new ArgumentNullException(nameof(album));

			string replacedAlbumName;
			NcmAlbum ncmAlbum;

			replacedAlbumName = album.Name.ReplaceEx();
			if (_cachedNcmAlbums.TryGetValue(replacedAlbumName, out ncmAlbum))
				return ncmAlbum;
			Logger.Instance.LogInfo($"开始搜索专辑\"{album}\"。");
			Logger.Instance.LogWarning("正在尝试带艺术家搜索，结果可能将过少！");
			ncmAlbum = await MapToAsync(album, true);
			if (ncmAlbum is null && _fuzzySettings.TryIgnoringArtists) {
				Logger.Instance.LogWarning("正在尝试忽略艺术家搜索，结果可能将不精确！");
				ncmAlbum = await MapToAsync(album, false);
			}
			if (ncmAlbum is null) {
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
		private static void LoadLocalCaches() {
			if (File.Exists(_allCachesPath)) {
				_allCaches = JsonConvert.DeserializeObject<AllCaches>(File.ReadAllText(_allCachesPath));
				NormalizeAllCaches();
				Logger.Instance.LogInfo($"搜索缓存\"{_allCachesPath}\"加载成功。");
			}
			else {
				_allCaches = new AllCaches() {
					AlbumCaches = new List<AlbumCache>(),
					LyricCaches = new List<LyricCache>(),
					TrackCaches = new List<TrackCache>()
				};
			}
		}

		private static void SaveLocalCaches() {
			NormalizeAllCaches();
			SaveLocalCachesCore(_allCachesPath);
			Logger.Instance.LogInfo($"搜索缓存\"{_allCachesPath}\"已被保存。");
		}

		private static void NormalizeAllCaches() {
			_allCaches.AlbumCaches.Sort((x, y) => string.CompareOrdinal(x.Name, y.Name));
			_allCaches.TrackCaches.Sort((x, y) => string.CompareOrdinal(x.Name, y.Name));
			_allCaches.LyricCaches.Sort((x, y) => x.Id.CompareTo(y.Id));
			foreach (TrackCache cache in _allCaches.TrackCaches) {
				for (int i = 0; i < cache.Artists.Length; i++)
					cache.Artists[i] = cache.Artists[i].Trim();
				Array.Sort(cache.Artists, StringHelper.OrdinalComparer);
			}
		}

		private static void SaveLocalCachesCore(string cachePath) {
			File.SetAttributes(_allCachesPath, FileAttributes.Normal);
			File.WriteAllText(cachePath, JsonConvert.SerializeObject(_allCaches));
			File.SetAttributes(_allCachesPath, FileAttributes.Hidden);
		}

		private static void UpdateCache(Album album, int id) {
			AlbumCache cache;

			cache = _allCaches.AlbumCaches.Match(album);
			if (!(cache is null))
				return;
			_allCaches.AlbumCaches.Add(new AlbumCache(album, id));
			OnCacheUpdated();
		}

		private static void UpdateCache(Track track, Album album, int id) {
			TrackCache cache;

			cache = _allCaches.TrackCaches.Match(track, album);
			if (!(cache is null))
				return;
			_allCaches.TrackCaches.Add(new TrackCache(track, album, id));
			OnCacheUpdated();
		}

		private static void UpdateCache(Track track, string fileName, int id) {
			TrackCache cache;

			cache = _allCaches.TrackCaches.Match(track, fileName);
			if (!(cache is null))
				return;
			_allCaches.TrackCaches.Add(new TrackCache(track, fileName, id));
			OnCacheUpdated();
		}

		private static void UpdateCache(NcmLyric lyric, string checkSum) {
			int index;

			index = _allCaches.LyricCaches.FindIndex(t => t.IsMatched(lyric.Id));
			if (index != -1)
				_allCaches.LyricCaches.RemoveAt(index);
			_allCaches.LyricCaches.Add(new LyricCache(lyric, checkSum));
			OnCacheUpdated();
		}

		private static void OnCacheUpdated() {
			SaveLocalCachesCore(_allCachesPath);
		}
		#endregion

		#region match
		private static TSource MatchByUser<TSource, TTarget>(TSource[] sources, TTarget target) where TSource : class, ITrackOrAlbum where TTarget : class, ITrackOrAlbum {
			TSource result;

			if (sources.Length == 0)
				return null;
			result = MatchByUser(sources, target, false);

			if (result is null && _fuzzySettings.TryIgnoringExtraInfo)
				result = MatchByUser(sources, target, true);
			return result;
		}

		private static TSource MatchByUser<TSource, TTarget>(TSource[] sources, TTarget target, bool fuzzy) where TSource : class, ITrackOrAlbum where TTarget : class, ITrackOrAlbum {
			Dictionary<TSource, double> nameSimilarities;
			TSource result;

			if (sources.Length == 0)
				return null;
			result = MatchExactly(sources, target, fuzzy);
			if (!fuzzy || !(result is null))
				// 不是fuzzy模式或者result不为空，可以直接返回结果，不需要用户选择了
				return result;
			nameSimilarities = new Dictionary<TSource, double>();
			foreach (TSource source in sources)
				nameSimilarities[source] = ComputeSimilarity(source.Name, target.Name, fuzzy);
			return Select(sources.Where(t => nameSimilarities[t] > _matchSettings.MinimumSimilarity).OrderByDescending(t => t, new DictionaryComparer<TSource, double>(nameSimilarities)).ToArray(), target, nameSimilarities);
		}

		private static TSource MatchExactly<TSource, TTarget>(TSource[] sources, TTarget target, bool fuzzy) where TSource : class, ITrackOrAlbum where TTarget : class, ITrackOrAlbum {
			foreach (TSource source in sources) {
				string x;
				string y;

				x = source.Name;
				y = target.Name;
				if (fuzzy) {
					x = x.Fuzzy();
					y = y.Fuzzy();
				}
				if (x != y)
					goto not_equal;
				if (source.Artists.Length != target.Artists.Length)
					goto not_equal;
				for (int i = 0; i < source.Artists.Length; i++) {
					x = source.Artists[i];
					y = target.Artists[i];
					if (fuzzy) {
						x = x.Fuzzy();
						y = y.Fuzzy();
					}
					if (x != y)
						goto not_equal;
				}
				return source;
			not_equal:
				continue;
			}
			return null;
		}

		private static TSource Select<TSource, TTarget>(TSource[] sources, TTarget target, Dictionary<TSource, double> nameSimilarities) where TSource : class, ITrackOrAlbum where TTarget : class, ITrackOrAlbum {
			TSource result;

			if (sources.Length == 0)
				return null;
			Logger.Instance.LogInfo("请手动输入1,2,3...选择匹配的项，若不存在，请输入P。");
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
				if (userInput.ToUpperInvariant() == "P")
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
			if (!(result is null))
				Logger.Instance.LogInfo("已选择：" + result.ToString());
			return result;

			string TrackOrAlbumToString(ITrackOrAlbum trackOrAlbum) {
				if (trackOrAlbum.Artists.Length == 0)
					return trackOrAlbum.Name;
				return trackOrAlbum.Name + " by " + string.Join(",", trackOrAlbum.Artists);
			}
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
			if (!(lyric.Raw is null))
				NormalizeLyric(lyric.Raw, false);
			if (!(lyric.Translated is null))
				NormalizeLyric(lyric.Translated, _lyricSettings.SimplifyTranslated);
			foreach (string mode in _lyricSettings.Modes) {
				switch (mode.ToUpperInvariant()) {
				case "MERGED":
					if (lyric.Raw is null || lyric.Translated is null)
						continue;
					Logger.Instance.LogInfo("已获取混合歌词。");
					return MergeLyric(lyric.Raw, lyric.Translated);
				case "RAW":
					if (lyric.Raw is null)
						continue;
					Logger.Instance.LogInfo("已获取原始歌词。");
					return lyric.Raw;
				case "TRANSLATED":
					if (lyric.Translated is null)
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

		private static void NormalizeLyric(Lrc lrc, bool simplify) {
			Dictionary<TimeSpan, string> newLyrics;

			newLyrics = new Dictionary<TimeSpan, string>(lrc.Lyrics.Count);
			foreach (KeyValuePair<TimeSpan, string> lyric in lrc.Lyrics) {
				string value;

				value = lyric.Value.Trim('/', ' ');
				if (simplify)
					value = ChineseConverter.TraditionalToSimplified(value);
				newLyrics.Add(lyric.Key, value);
			}
			lrc.Lyrics = newLyrics;
		}

		private static Lrc MergeLyric(Lrc rawLrc, Lrc translatedLrc) {
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
