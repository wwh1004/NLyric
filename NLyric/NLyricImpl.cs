using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NeteaseCloudMusicApi;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NLyric.Audio;
using NLyric.Database;
using NLyric.Lyrics;
using NLyric.Ncm;
using NLyric.Settings;
using TagLib;
using File = System.IO.File;

namespace NLyric {
	public static class NLyricImpl {
		private static readonly SearchSettings _searchSettings = AllSettings.Default.Search;
		private static readonly FuzzySettings _fuzzySettings = AllSettings.Default.Fuzzy;
		private static readonly MatchSettings _matchSettings = AllSettings.Default.Match;
		private static readonly LyricSettings _lyricSettings = AllSettings.Default.Lyric;
		private static readonly HashSet<string> _failMatchAlbums = new HashSet<string>();
		// AlbumName
		private static readonly Dictionary<int, NcmTrack[]> _cachedNcmTrackses = new Dictionary<int, NcmTrack[]>();
		// AlbumId -> Tracks
		private static readonly Dictionary<int, NcmLyric> _cachedNcmLyrics = new Dictionary<int, NcmLyric>();
		// TrackId -> Lyric
		private static NLyricDatabase _database;

		public static async Task ExecuteAsync(Arguments arguments) {
			FastConsole.WriteLine("程序会自动过滤相似度为0的结果与歌词未被收集的结果！！！", ConsoleColor.Green);
			var loginTask = LoginIfNeedAsync(arguments);
			string databasePath = Path.Combine(arguments.Directory, ".nlyric");
			LoadDatabase(databasePath);
			var audioInfos = LoadAllAudioInfos(arguments.Directory);
			var audioInfoCandidates = audioInfos.Where(t => t.TrackInfo is null).ToArray();
			await loginTask;
			// 登录同时进行
			if (!arguments.UpdateOnly) {
				if (arguments.UseBatch)
					_ = AccelerateAllTracksAsync(audioInfoCandidates);
				await LoadAllAudioInfoCandidates(audioInfoCandidates, _ => SaveDatabaseCore(databasePath));
			}
			audioInfos = audioInfos.Where(t => !(t.TrackInfo is null)).ToArray();
			if (arguments.UseBatch)
				_ = AccelerateAllLyricsAsync(audioInfos);
			await DownloadLyricsAsync(audioInfos);
			SaveDatabase(databasePath);
		}

		private static async Task LoginIfNeedAsync(Arguments arguments) {
			if (string.IsNullOrEmpty(arguments.Account) || string.IsNullOrEmpty(arguments.Password)) {
				FastConsole.WriteLine("登录可避免出现大部分API错误！！！当前是免登录状态，若软件出错请尝试登录！！！", ConsoleColor.Green);
				FastConsole.WriteLine("强烈建议登录使用软件：\"NLyric.exe -d C:\\Music -a example@example.com -p 123456\"", ConsoleColor.Green);
			}
			else {
				FastConsole.WriteLine("登录中...", ConsoleColor.Green);
				if (await CloudMusic.LoginAsync(arguments.Account, arguments.Password)) {
					FastConsole.WriteLine("登录成功！", ConsoleColor.Green);
				}
				else {
					FastConsole.WriteError("登录失败，输入任意键以免登录模式运行或重新运行尝试再次登录！");
					try {
						FastConsole.ReadKey(true);
					}
					catch {
					}
				}
			}
			FastConsole.WriteNewLine();
		}

		private static bool CanSkip(string audioPath, string lrcPath) {
			string extension = Path.GetExtension(audioPath);
			if (!IsAudioFile(extension))
				return true;
			if (File.Exists(lrcPath) && !_lyricSettings.AutoUpdate && !_lyricSettings.Overwriting) {
				FastConsole.WriteInfo($"文件\"{Path.GetFileName(audioPath)}\"的歌词已存在，并且自动更新与覆盖已被禁止，正在跳过。");
				return true;
			}
			return false;
		}

		private static bool IsAudioFile(string extension) {
			return _searchSettings.AudioExtensions.Any(s => extension.Equals(s, StringComparison.OrdinalIgnoreCase));
		}

		private static AudioInfo[] LoadAllAudioInfos(string directory) {
			return Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).Where(audioPath => {
				string lrcPath = Path.ChangeExtension(audioPath, ".lrc");
				return !CanSkip(audioPath, lrcPath);
			}).AsParallel().AsOrdered().Select(audioPath => {
				var audioFile = default(TagLib.File);
				var audioInfo = new AudioInfo {
					Path = audioPath
				};
				var tag = default(Tag);
				try {
					audioFile = TagLib.File.Create(audioPath);
					tag = audioFile.Tag;
					if (Album.HasAlbumInfo(tag))
						audioInfo.Album = new Album(tag, true);
					audioInfo.Track = new Track(tag);
				}
				catch (Exception ex) {
					FastConsole.WriteError("无效音频文件！");
					FastConsole.WriteException(ex);
					return null;
				}
				finally {
					audioFile?.Dispose();
				}
				TrackInfo trackInfo;
				lock (_database.TrackInfos)
					trackInfo = _database.TrackInfos.Match(audioInfo.Album, audioInfo.Track);
				if (!(trackInfo is null)) {
					audioInfo.TrackInfo = trackInfo;
					return audioInfo;
				}
				// 尝试从数据库获取歌曲
				if (The163KeyHelper.TryGetTrackId(tag, out int trackId)) {
					trackInfo = new TrackInfo(audioInfo.Track, audioInfo.Album, trackId);
					lock (_database.TrackInfos)
						_database.TrackInfos.Add(trackInfo);
					audioInfo.TrackInfo = trackInfo;
					return audioInfo;
				}
				// 尝试从163Key获取ID
				return audioInfo;
			}).Where(t => !(t is null)).ToArray();
		}

		private static async Task LoadAllAudioInfoCandidates(AudioInfo[] audioInfoCandidates, Action<AudioInfo> callback) {
			foreach (var candidate in audioInfoCandidates) {
				FastConsole.WriteInfo($"开始获取文件\"{Path.GetFileName(candidate.Path)}\"的网易云音乐ID。");
				TrackInfo trackInfo;
				try {
					trackInfo = await SearchTrackAsync(candidate.Album, candidate.Track);
				}
				catch (Exception ex) {
					FastConsole.WriteException(ex);
					trackInfo = null;
				}
				if (trackInfo is null) {
					FastConsole.WriteWarning($"无法找到文件\"{Path.GetFileName(candidate.Path)}\"的网易云音乐ID！");
				}
				else {
					FastConsole.WriteInfo($"已获取文件\"{Path.GetFileName(candidate.Path)}\"的网易云音乐ID: {trackInfo.Id}。");
					candidate.TrackInfo = new TrackInfo(candidate.Track, candidate.Album, trackInfo.Id);
					_database.TrackInfos.Add(candidate.TrackInfo);
				}
				callback?.Invoke(candidate);
				FastConsole.WriteNewLine();
			}
		}

		private static async Task DownloadLyricsAsync(AudioInfo[] audioInfos) {
			foreach (var audioInfo in audioInfos)
				await TryDownloadLyricAsync(audioInfo);
		}

		#region search
		/// <summary>
		/// 同时根据专辑信息以及歌曲信息获取网易云音乐上的歌曲
		/// </summary>
		/// <param name="album"></param>
		/// <param name="track"></param>
		/// <returns></returns>
		private static async Task<TrackInfo> SearchTrackAsync(Album album, Track track) {
			var albumInfo = album is null ? null : await SearchAlbumAsync(album);
			// 尝试获取专辑信息
			var ncmTrack = default(NcmTrack);
			if (!(albumInfo is null)) {
				// 网易云音乐收录了歌曲所在专辑	
				var ncmTracks = (await GetAlbumTracksAsync(albumInfo)).Where(t => ComputeSimilarity(t.Name, track.Name, false) != 0).ToArray();
				// 获取网易云音乐上专辑收录的歌曲
				ncmTrack = MatchByUser(ncmTracks, track);
			}
			else {
				ncmTrack = null;
			}
			if (ncmTrack is null)
				ncmTrack = await MapToAsync(track);
			// 没有对应的专辑信息，使用无专辑匹配，或者网易云音乐上的专辑可能没收录这个歌曲，不清楚为什么，但是确实存在这个情况，比如专辑id:3094396
			bool byUser;
			int trackId;
			if (ncmTrack is null) {
				byUser = GetIdByUser("歌曲", out trackId);
			}
			else {
				byUser = false;
				trackId = 0;
			}
			var trackInfo = default(TrackInfo);
			if (ncmTrack is null && !byUser) {
				trackInfo = null;
				FastConsole.WriteWarning("歌曲匹配失败！");
			}
			else {
				trackInfo = new TrackInfo(track, album, byUser ? trackId : ncmTrack.Id);
				_database.TrackInfos.Add(trackInfo);
				FastConsole.WriteInfo("歌曲匹配成功！");
			}
			return trackInfo;
		}

		/// <summary>
		/// 根据专辑信息获取网易云音乐上的专辑
		/// </summary>
		/// <param name="album"></param>
		/// <returns></returns>
		private static async Task<AlbumInfo> SearchAlbumAsync(Album album) {
			var albumInfo = _database.AlbumInfos.Match(album);
			if (!(albumInfo is null))
				return albumInfo;
			// 先尝试从数据库获取专辑
			string replacedAlbumName = album.Name.ReplaceEx();
			if (_failMatchAlbums.Contains(replacedAlbumName))
				return null;
			// 防止不停重复匹配一个专辑
			var ncmAlbum = await MapToAsync(album);
			bool byUser;
			int albumId;
			if (ncmAlbum is null) {
				byUser = GetIdByUser("专辑", out albumId);
			}
			else {
				byUser = false;
				albumId = 0;
			}
			if (ncmAlbum is null && !byUser) {
				_failMatchAlbums.Add(replacedAlbumName);
				FastConsole.WriteWarning("专辑匹配失败！");
			}
			else {
				albumInfo = new AlbumInfo(album, byUser ? albumId : ncmAlbum.Id);
				_database.AlbumInfos.Add(albumInfo);
				FastConsole.WriteInfo("专辑匹配成功！");
			}
			return albumInfo;
		}

		private static async Task<NcmTrack[]> GetAlbumTracksAsync(AlbumInfo albumInfo) {
			if (!_cachedNcmTrackses.TryGetValue(albumInfo.Id, out var ncmTracks)) {
				var list = new List<NcmTrack>();
				foreach (var item in await CloudMusic.GetTracksAsync(albumInfo.Id)) {
					if ((await GetLyricAsync(item.Id)).IsCollected)
						list.Add(item);
				}
				ncmTracks = list.ToArray();
				lock (((ICollection)_cachedNcmTrackses).SyncRoot)
					_cachedNcmTrackses[albumInfo.Id] = ncmTracks;
			}
			return ncmTracks;
		}
		#endregion

		#region map
		/// <summary>
		/// 获取网易云音乐上的歌曲，自动尝试带艺术家与不带艺术家搜索
		/// </summary>
		/// <param name="track"></param>
		/// <returns></returns>
		private static async Task<NcmTrack> MapToAsync(Track track) {
			if (track is null)
				throw new ArgumentNullException(nameof(track));

			FastConsole.WriteInfo($"开始搜索歌曲\"{track}\"。");
			FastConsole.WriteWarning("正在尝试带艺术家搜索，结果可能将过少！");
			var ncmTrack = await MapToAsync(track, true);
			if (ncmTrack is null && _fuzzySettings.TryIgnoringArtists) {
				FastConsole.WriteWarning("正在尝试忽略艺术家搜索，结果可能将不精确！");
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

			FastConsole.WriteInfo($"开始搜索专辑\"{album}\"。");
			FastConsole.WriteWarning("正在尝试带艺术家搜索，结果可能将过少！");
			var ncmAlbum = await MapToAsync(album, true);
			if (ncmAlbum is null && _fuzzySettings.TryIgnoringArtists) {
				FastConsole.WriteWarning("正在尝试忽略艺术家搜索，结果可能将不精确！");
				ncmAlbum = await MapToAsync(album, false);
			}
			return ncmAlbum;
		}

		/// <summary>
		/// 获取网易云音乐上的歌曲
		/// </summary>
		/// <param name="track"></param>
		/// <param name="withArtists">是否带艺术家搜索</param>
		/// <returns></returns>
		private static async Task<NcmTrack> MapToAsync(Track track, bool withArtists) {
			var ncmTracks = default(NcmTrack[]);
			try {
				ncmTracks = await CloudMusic.SearchTrackAsync(track, _searchSettings.Limit, withArtists);
			}
			catch (KeywordForbiddenException ex1) {
				FastConsole.WriteError(ex1.Message);
				return null;
			}
			catch (Exception ex2) {
				FastConsole.WriteException(ex2);
				return null;
			}
			var list = new List<NcmTrack>();
			foreach (var item in ncmTracks.Where(t => ComputeSimilarity(t.Name, track.Name, false) != 0)) {
				if ((await GetLyricAsync(item.Id)).IsCollected)
					list.Add(item);
			}
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
			var ncmAlbums = default(NcmAlbum[]);
			try {
				ncmAlbums = await CloudMusic.SearchAlbumAsync(album, _searchSettings.Limit, withArtists);
			}
			catch (KeywordForbiddenException ex1) {
				FastConsole.WriteError(ex1.Message);
				return null;
			}
			catch (Exception ex2) {
				FastConsole.WriteException(ex2);
				return null;
			}
			ncmAlbums = ncmAlbums.Where(t => ComputeSimilarity(t.Name, album.Name, false) != 0).ToArray();
			return MatchByUser(ncmAlbums, album);
		}
		#endregion

		#region database
		private static void LoadDatabase(string databasePath) {
			if (File.Exists(databasePath)) {
				_database = JsonConvert.DeserializeObject<NLyricDatabase>(File.ReadAllText(databasePath));
				if (!_database.CheckFormatVersion())
					throw new InvalidOperationException("尝试加载新格式数据库。");

				if (_database.IsOldFormat()) {
					FastConsole.WriteWarning("不兼容的老格式数据库，将被覆盖重建！");
				}
				else {
					SortDatabase();
					FastConsole.WriteInfo($"搜索数据库\"{databasePath}\"加载成功。");
					FastConsole.WriteNewLine();
					return;
				}
			}

			_database = new NLyricDatabase() {
				AlbumInfos = new List<AlbumInfo>(),
				TrackInfos = new List<TrackInfo>(),
				FormatVersion = 1
			};
			if (File.Exists(databasePath))
				File.Delete(databasePath);
			SaveDatabaseCore(databasePath);
			File.SetAttributes(databasePath, FileAttributes.Hidden);
		}

		private static void SaveDatabase(string databasePath) {
			SortDatabase();
			SaveDatabaseCore(databasePath);
			FastConsole.WriteInfo($"搜索数据库\"{databasePath}\"已被保存。");
		}

		private static void SortDatabase() {
			_database.AlbumInfos.Sort((x, y) => string.CompareOrdinal(x.Name, y.Name));
			_database.TrackInfos.Sort((x, y) => string.CompareOrdinal(x.Name, y.Name));
		}

		private static void SaveDatabaseCore(string databasePath) {
			using (var stream = new FileStream(databasePath, FileMode.OpenOrCreate))
			using (var writer = new StreamWriter(stream))
				writer.Write(FormatJson(JsonConvert.SerializeObject(_database)));
		}

		private static string FormatJson(string json) {
			using (var writer = new StringWriter())
			using (var jsonWriter = new JsonTextWriter(writer) { Formatting = Formatting.Indented })
			using (var reader = new StringReader(json))
			using (var jsonReader = new JsonTextReader(reader)) {
				jsonWriter.WriteToken(jsonReader);
				return writer.ToString();
			}
		}
		#endregion

		#region match
		private static TSource MatchByUser<TSource, TTarget>(TSource[] sources, TTarget target) where TSource : class, ITrackOrAlbum where TTarget : class, ITrackOrAlbum {
			if (sources.Length == 0)
				return null;
			var result = MatchByUser(sources, target, false);
			if (result is null && _fuzzySettings.TryIgnoringExtraInfo)
				result = MatchByUser(sources, target, true);
			return result;
		}

		private static TSource MatchByUser<TSource, TTarget>(TSource[] sources, TTarget target, bool fuzzy) where TSource : class, ITrackOrAlbum where TTarget : class, ITrackOrAlbum {
			if (sources.Length == 0)
				return null;

			var result = MatchExactly(sources, target, fuzzy);
			if (!fuzzy || !(result is null))
				return result;
			// 不是fuzzy模式或者result不为空，可以直接返回结果，不需要用户选择了

			var nameSimilarities = new Dictionary<TSource, double>();
			foreach (var source in sources)
				nameSimilarities[source] = ComputeSimilarity(source.Name, target.Name, fuzzy);
			return Select(sources.Where(t => nameSimilarities[t] > _matchSettings.MinimumSimilarity).OrderByDescending(t => nameSimilarities[t]).ToArray(), target, nameSimilarities);
		}

		private static TSource MatchExactly<TSource, TTarget>(TSource[] sources, TTarget target, bool fuzzy) where TSource : class, ITrackOrAlbum where TTarget : class, ITrackOrAlbum {
			foreach (var source in sources) {
				string x = source.Name;
				string y = target.Name;
				if (fuzzy) {
					x = x.Fuzzy();
					y = y.Fuzzy();
				}

				if (x != y)
					goto not_equal;
				if (source.Artists.Count != target.Artists.Count)
					goto not_equal;

				for (int i = 0; i < source.Artists.Count; i++) {
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
			if (sources.Length == 0)
				return null;

			FastConsole.WriteInfo("请手动输入1,2,3...选择匹配的项，若不存在，请直接按下回车键。");
			FastConsole.WriteInfo("对比项：" + TrackOrAlbumToString(target));
			for (int i = 0; i < sources.Length; i++) {
				double nameSimilarity = nameSimilarities[sources[i]];
				string text = $"{i + 1}. {sources[i]} (s:{nameSimilarity:F2})";
				if (nameSimilarity >= 0.85)
					FastConsole.WriteLine(text, ConsoleColor.Green);
				else if (nameSimilarity >= 0.5)
					FastConsole.WriteLine(text, ConsoleColor.Yellow);
				else
					FastConsole.WriteInfo(text);
			}

			var result = default(TSource);
			do {
				string userInput = FastConsole.ReadLine().Trim();
				if (userInput.Length == 0)
					break;
				if (int.TryParse(userInput, out int index)) {
					index -= 1;
					if (index >= 0 && index < sources.Length) {
						result = sources[index];
						break;
					}
				}
				FastConsole.WriteWarning("输入有误，请重新输入！");
			} while (true);

			if (!(result is null))
				FastConsole.WriteInfo("已选择：" + result.ToString());
			return result;

			string TrackOrAlbumToString(ITrackOrAlbum trackOrAlbum) {
				if (trackOrAlbum.Artists.Count == 0)
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

		private static bool GetIdByUser(string s, out int id) {
			FastConsole.WriteInfo($"请输入{s}的网易云音乐ID，若不存在，请直接按下回车键。");
			do {
				string userInput = FastConsole.ReadLine().Trim();
				if (userInput.Length == 0)
					break;
				if (int.TryParse(userInput, out id))
					return true;
				FastConsole.WriteWarning("输入有误，请重新输入！");
			} while (true);
			id = 0;
			return false;
		}
		#endregion

		#region lyric
		private static async Task<bool> TryDownloadLyricAsync(AudioInfo audioInfo) {
			string lrcPath = Path.ChangeExtension(audioInfo.Path, ".lrc");
			bool hasLrcFile = File.Exists(lrcPath);
			var trackInfo = audioInfo.TrackInfo;
			NcmLyric ncmLyric;
			try {
				ncmLyric = await GetLyricAsync(trackInfo.Id);
			}
			catch (Exception ex) {
				FastConsole.WriteException(ex);
				return false;
			}
			FastConsole.WriteInfo($"正在尝试下载\"{Path.GetFileName(audioInfo.Path)} ({audioInfo.Track})\"的歌词。");
			if (hasLrcFile) {
				// 如果歌词存在，判断是否需要覆盖或更新
				var lyricInfo = trackInfo.Lyric;
				string lyricCheckSum = hasLrcFile ? ComputeLyricCheckSum(File.ReadAllBytes(lrcPath)) : null;
				if (!(lyricInfo is null) && lyricInfo.CheckSum == lyricCheckSum) {
					// 歌词由NLyric创建
					if (ncmLyric.RawVersion <= lyricInfo.RawVersion && ncmLyric.TranslatedVersion <= lyricInfo.TranslatedVersion) {
						// 是最新版本
						FastConsole.WriteInfo("本地歌词已是最新版本，正在跳过。");
						return false;
					}
					else {
						// 不是最新版本
						if (_lyricSettings.AutoUpdate) {
							FastConsole.WriteLine("本地歌词不是最新版本，正在更新。", ConsoleColor.Green);
						}
						else {
							FastConsole.WriteLine("本地歌词不是最新版本但是自动更新被禁止，正在跳过。", ConsoleColor.Yellow);
							return false;
						}
					}
				}
				else {
					// 歌词非NLyric创建
					if (_lyricSettings.Overwriting) {
						FastConsole.WriteLine("本地歌词非NLyric创建，正在更新。", ConsoleColor.Yellow);
					}
					else {
						FastConsole.WriteLine("本地歌词非NLyric创建但是覆盖被禁止，正在跳过。", ConsoleColor.Yellow);
						return false;
					}
				}
			}
			var lrc = ToLrc(ncmLyric);
			if (!(lrc is null)) {
				// 歌词已收录，不是纯音乐
				string lyric = lrc.ToString();
				try {
					File.WriteAllText(lrcPath, lyric, _lyricSettings.Encoding);
				}
				catch (Exception ex) {
					FastConsole.WriteException(ex);
					return false;
				}
				trackInfo.Lyric = new LyricInfo(ncmLyric, ComputeLyricCheckSum(_lyricSettings.Encoding.GetBytes(lyric)));
				FastConsole.WriteLine("本地歌词下载完毕。", ConsoleColor.Magenta);
			}
			return true;
		}

		private static async Task<NcmLyric> GetLyricAsync(int trackId) {
			if (!_cachedNcmLyrics.TryGetValue(trackId, out var lyric)) {
				lyric = await CloudMusic.GetLyricAsync(trackId);
				lock (((ICollection)_cachedNcmLyrics).SyncRoot)
					_cachedNcmLyrics[trackId] = lyric;
			}
			return lyric;
		}

		private static Lrc ToLrc(NcmLyric lyric) {
			if (!lyric.IsCollected) {
				FastConsole.WriteWarning("当前歌曲的歌词未被收录！");
				return null;
			}
			if (lyric.IsAbsoluteMusic) {
				FastConsole.WriteWarning("当前歌曲是纯音乐无歌词！");
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
					FastConsole.WriteInfo("已获取混合歌词。");
					return MergeLyric(lyric.Raw, lyric.Translated);

				case "RAW":
					if (lyric.Raw is null)
						continue;
					FastConsole.WriteInfo("已获取原始歌词。");
					return lyric.Raw;

				case "TRANSLATED":
					if (lyric.Translated is null)
						continue;
					FastConsole.WriteInfo("已获取翻译歌词。");
					return lyric.Translated;

				default:
					throw new ArgumentOutOfRangeException(nameof(mode));
				}
			}

			FastConsole.WriteWarning("获取歌词失败（可能歌曲是纯音乐但是未被网易云音乐标记为纯音乐）。");
			return null;
		}

		private static void NormalizeLyric(Lrc lrc, bool simplify) {
			var newLyrics = new Dictionary<TimeSpan, string>(lrc.Lyrics.Count);
			foreach (var lyric in lrc.Lyrics) {
				string value = lyric.Value.Trim('/', ' ');
				if (simplify)
					value = ChineseConverter.TraditionalToSimplified(value);
				newLyrics.Add(lyric.Key, value);
			}
			lrc.Lyrics = newLyrics;
		}

		private static Lrc MergeLyric(Lrc rawLrc, Lrc translatedLrc) {
			var mergedLrc = new Lrc {
				Offset = rawLrc.Offset,
				Title = rawLrc.Title
			};

			foreach (var rawLyric in rawLrc.Lyrics)
				mergedLrc.Lyrics.Add(rawLyric.Key, rawLyric.Value);

			foreach (var translatedLyric in translatedLrc.Lyrics) {
				if (translatedLyric.Value.Length == 0)
					continue;
				// 如果翻译歌词是空字符串，跳过

				if (!string.IsNullOrEmpty(translatedLyric.Value) && !mergedLrc.Lyrics.ContainsKey(translatedLyric.Key)) {
					// 如果有翻译歌词并且没有对应的未翻译歌词，直接添加
					mergedLrc.Lyrics.Add(translatedLyric.Key, translatedLyric.Value);
					continue;
				}

				string rawLyric = mergedLrc.Lyrics[translatedLyric.Key];
				if (rawLyric.Length != 0)
					mergedLrc.Lyrics[translatedLyric.Key] = $"{rawLyric} 「{translatedLyric.Value}」";
				// 如果未翻译歌词是空字符串，表示上一句歌词的结束，那么跳过
			}

			return mergedLrc;
		}

		private static string ComputeLyricCheckSum(byte[] lyric) {
			return CRC32.Compute(lyric).ToString("X8");
		}
		#endregion

		#region accelerate
		private static Task AccelerateAllTracksAsync(AudioInfo[] audioInfos) {
			// TODO
			return Task.CompletedTask;
		}

		private static async Task AccelerateAllLyricsAsync(AudioInfo[] audioInfos) {
			const int STEP = 100;

			int[] trackIds = audioInfos.Select(t => t.TrackInfo.Id).ToArray();
			for (int i = 0; i < trackIds.Length; i += STEP) {
				var trackIdMap = new Dictionary<string, int>(STEP);
				var queries = new Dictionary<string, string>(STEP);
				int kMax = i + STEP <= trackIds.Length ? STEP : trackIds.Length % STEP;
				for (int k = 0; k < kMax; k++) {
					string route = "/api/song/lyric" + new string('/', k);
					trackIdMap[route] = trackIds[i + k];
					queries[route] = JsonConvert.SerializeObject(new Dictionary<string, object> {
						["id"] = trackIds[i + k],
						["lv"] = -1,
						["kv"] = -1,
						["tv"] = -1
					});
				}
				var (isOk, json) = await CloudMusic.Api.RequestAsync(CloudMusicApiProviders.Batch, queries);
				if (!isOk) {
					FastConsole.WriteError($"[Experimental] 歌词 {i}+{STEP} 加速失败！");
					continue;
				}
				lock (((ICollection)_cachedNcmLyrics).SyncRoot) {
					foreach (var item in trackIdMap) {
						int trackId = item.Value;
						if (!(json[item.Key] is JObject lyricJson)) {
							FastConsole.WriteError($"[Experimental] 歌词 {trackId} at {i}+{STEP} 加速失败！");
							continue;
						}
						_cachedNcmLyrics[trackId] = CloudMusic.ParseLyric(trackId, lyricJson);
					}
				}
			}
		}
		#endregion

		private sealed class AudioInfo {
			public string Path { get; set; }

			public Album Album { get; set; }

			public Track Track { get; set; }

			public TrackInfo TrackInfo { get; set; }
		}
	}
}
