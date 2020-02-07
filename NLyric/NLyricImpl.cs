using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NLyric.Audio;
using NLyric.Database;
using NLyric.Lyrics;
using NLyric.Ncm;
using NLyric.Settings;
using TagLib;
using File = System.IO.File;

namespace NLyric {
	internal static class NLyricImpl {
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
			Task loginTask;
			string databasePath;
			AudioInfo[] audioInfos;

			loginTask = LoginIfNeedAsync(arguments);
			databasePath = Path.Combine(arguments.Directory, ".nlyric");
			LoadDatabase(databasePath);
			audioInfos = Directory.EnumerateFiles(arguments.Directory, "*", SearchOption.AllDirectories).Where(audioPath => {
				string lrcPath;

				lrcPath = Path.ChangeExtension(audioPath, ".lrc");
				return !CanSkip(audioPath, lrcPath);
			}).AsParallel().AsOrdered().Select(audioPath => {
				TagLib.File audioFile;
				AudioInfo audioInfo;

				audioFile = null;
				audioInfo = new AudioInfo {
					Path = audioPath
				};
				try {
					audioFile = TagLib.File.Create(audioPath);
					audioInfo.Tag = audioFile.Tag;
					if (Album.HasAlbumInfo(audioInfo.Tag))
						audioInfo.Album = new Album(audioInfo.Tag, true);
					audioInfo.Track = new Track(audioInfo.Tag);
				}
				catch (Exception ex) {
					FastConsole.WriteError("无效音频文件！");
					FastConsole.WriteException(ex);
				}
				finally {
					audioFile?.Dispose();
				}
				return audioInfo;
			}).ToArray();
			await loginTask;
			// 登录同时进行
			foreach (AudioInfo audioInfo in audioInfos) {
				TrackInfo trackInfo;

				FastConsole.WriteInfo($"开始搜索文件\"{Path.GetFileName(audioInfo.Path)}\"的歌词。");
				trackInfo = await SearchTrackAsync(audioInfo.Tag);
				if (trackInfo is null)
					FastConsole.WriteWarning($"无法找到文件\"{Path.GetFileName(audioInfo.Path)}\"的网易云音乐ID！");
				else {
					FastConsole.WriteInfo($"已获取文件\"{Path.GetFileName(audioInfo.Path)}\"的网易云音乐ID: {trackInfo.Id}。");
					await TryDownloadLyricAsync(trackInfo, Path.ChangeExtension(audioInfo.Path, ".lrc"));
				}
				SaveDatabaseCore(databasePath);
				FastConsole.WriteNewLine();
				FastConsole.WriteNewLine();
			}
			SaveDatabase(databasePath);
		}

		private static async Task LoginIfNeedAsync(Arguments arguments) {
			if (string.IsNullOrEmpty(arguments.Account) || string.IsNullOrEmpty(arguments.Password)) {
				for (int i = 0; i < 3; i++)
					FastConsole.WriteLine("登录可避免出现大部分API错误！！！当前是免登录状态，若软件出错请尝试登录！！！", ConsoleColor.Green);
				FastConsole.WriteLine("强烈建议登录使用软件：\"NLyric.exe -d C:\\Music -a example@example.com -p 123456\"", ConsoleColor.Green);
			}
			else {
				FastConsole.WriteLine("登录中...", ConsoleColor.Green);
				if (await CloudMusic.LoginAsync(arguments.Account, arguments.Password))
					FastConsole.WriteLine("登录成功！", ConsoleColor.Green);
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
			string extension;

			extension = Path.GetExtension(audioPath);
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

		/// <summary>
		/// 同时根据专辑信息以及歌曲信息获取网易云音乐上的歌曲
		/// </summary>
		/// <param name="tag"></param>
		/// <returns></returns>
		private static async Task<TrackInfo> SearchTrackAsync(Tag tag) {
			Track track;
			Album album;

			track = new Track(tag);
			album = Album.HasAlbumInfo(tag) ? new Album(tag, true) : null;
			// 获取Tag信息
			try {
				return await SearchTrackAsync(tag, track, album);
			}
			catch (Exception ex) {
				FastConsole.WriteException(ex);
			}
			return null;
		}

		/// <summary>
		/// 同时根据专辑信息以及歌曲信息获取网易云音乐上的歌曲
		/// </summary>
		/// <param name="tag"></param>
		/// <param name="track"></param>
		/// <param name="album"></param>
		/// <returns></returns>
		private static async Task<TrackInfo> SearchTrackAsync(Tag tag, Track track, Album album) {
			TrackInfo trackInfo;
			int trackId;
			NcmTrack ncmTrack;
			bool byUser;

			trackInfo = _database.TrackInfos.Match(track, album);
			if (!(trackInfo is null))
				return trackInfo;
			// 先尝试从数据库获取歌曲
			if (The163KeyHelper.TryGetTrackId(tag, out trackId)) {
				// 尝试从163Key获取ID成功
				ncmTrack = new NcmTrack(track, trackId);
			}
			else {
				// 不存在163Key
				AlbumInfo albumInfo;

				albumInfo = album is null ? null : await SearchAlbumAsync(album);
				// 尝试获取专辑信息
				if (!(albumInfo is null)) {
					// 网易云音乐收录了歌曲所在专辑
					NcmTrack[] ncmTracks;

					ncmTracks = (await GetAlbumTracksAsync(albumInfo)).Where(t => ComputeSimilarity(t.Name, track.Name, false) != 0).ToArray();
					// 获取网易云音乐上专辑收录的歌曲
					ncmTrack = MatchByUser(ncmTracks, track);
				}
				else
					ncmTrack = null;
				if (ncmTrack is null)
					// 没有对应的专辑信息，使用无专辑匹配，或者网易云音乐上的专辑可能没收录这个歌曲，不清楚为什么，但是确实存在这个情况，比如专辑id:3094396
					ncmTrack = await MapToAsync(track);
			}
			if (ncmTrack is null)
				byUser = GetIdByUser("歌曲", out trackId);
			else {
				byUser = false;
				trackId = 0;
			}
			if (ncmTrack is null && !byUser)
				FastConsole.WriteWarning("歌曲匹配失败！");
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
			AlbumInfo albumInfo;
			string replacedAlbumName;
			NcmAlbum ncmAlbum;
			bool byUser;
			int albumId;

			albumInfo = _database.AlbumInfos.Match(album);
			if (!(albumInfo is null))
				return albumInfo;
			// 先尝试从数据库获取专辑
			replacedAlbumName = album.Name.ReplaceEx();
			if (_failMatchAlbums.Contains(replacedAlbumName))
				return null;
			// 防止不停重复匹配一个专辑
			ncmAlbum = await MapToAsync(album);
			if (ncmAlbum is null)
				byUser = GetIdByUser("专辑", out albumId);
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
			NcmTrack[] ncmTracks;

			if (!_cachedNcmTrackses.TryGetValue(albumInfo.Id, out ncmTracks)) {
				List<NcmTrack> list;

				list = new List<NcmTrack>();
				foreach (NcmTrack item in await CloudMusic.GetTracksAsync(albumInfo.Id))
					if ((await GetLyricAsync(item.Id)).IsCollected)
						list.Add(item);
				ncmTracks = list.ToArray();
				_cachedNcmTrackses[albumInfo.Id] = ncmTracks;
			}
			return ncmTracks;
		}

		private static async Task<bool> TryDownloadLyricAsync(TrackInfo trackInfo, string lrcPath) {
			bool hasLrcFile;
			string lyricCheckSum;
			NcmLyric ncmLyric;
			Lrc lrc;

			hasLrcFile = File.Exists(lrcPath);
			lyricCheckSum = hasLrcFile ? ComputeLyricCheckSum(File.ReadAllText(lrcPath)) : null;
			try {
				ncmLyric = await GetLyricAsync(trackInfo.Id);
			}
			catch (Exception ex) {
				FastConsole.WriteException(ex);
				return false;
			}
			if (hasLrcFile) {
				// 如果歌词存在，判断是否需要覆盖或更新
				LyricInfo lyricInfo;

				lyricInfo = trackInfo.Lyric;
				if (!(lyricInfo is null) && lyricInfo.CheckSum == lyricCheckSum) {
					// 歌词由NLyric创建
					if (ncmLyric.RawVersion <= lyricInfo.RawVersion && ncmLyric.TranslatedVersion <= lyricInfo.TranslatedVersion) {
						// 是最新版本
						FastConsole.WriteInfo("本地歌词已是最新版本，正在跳过。");
						return false;
					}
					else {
						// 不是最新版本
						if (_lyricSettings.AutoUpdate)
							FastConsole.WriteLine("本地歌词不是最新版本，正在更新。", ConsoleColor.Green);
						else {
							FastConsole.WriteLine("本地歌词不是最新版本但是自动更新被禁止，正在跳过。", ConsoleColor.Yellow);
							return false;
						}
					}
				}
				else {
					// 歌词非NLyric创建
					if (_lyricSettings.Overwriting)
						FastConsole.WriteLine("本地歌词非NLyric创建，正在更新。", ConsoleColor.Yellow);
					else {
						FastConsole.WriteLine("本地歌词非NLyric创建但是覆盖被禁止，正在跳过。", ConsoleColor.Yellow);
						return false;
					}
				}
			}
			lrc = ToLrc(ncmLyric);
			if (!(lrc is null)) {
				// 歌词已收录，不是纯音乐
				string lyric;

				lyric = lrc.ToString();
				try {
					File.WriteAllText(lrcPath, lyric);
				}
				catch (Exception ex) {
					FastConsole.WriteException(ex);
					return false;
				}
				trackInfo.Lyric = new LyricInfo(ncmLyric, ComputeLyricCheckSum(lyric));
				FastConsole.WriteLine("本地歌词下载完毕。", ConsoleColor.Magenta);
			}
			return true;
		}

		#region map
		/// <summary>
		/// 获取网易云音乐上的歌曲，自动尝试带艺术家与不带艺术家搜索
		/// </summary>
		/// <param name="track"></param>
		/// <returns></returns>
		private static async Task<NcmTrack> MapToAsync(Track track) {
			if (track is null)
				throw new ArgumentNullException(nameof(track));

			NcmTrack ncmTrack;

			FastConsole.WriteInfo($"开始搜索歌曲\"{track}\"。");
			FastConsole.WriteWarning("正在尝试带艺术家搜索，结果可能将过少！");
			ncmTrack = await MapToAsync(track, true);
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

			NcmAlbum ncmAlbum;

			FastConsole.WriteInfo($"开始搜索专辑\"{album}\"。");
			FastConsole.WriteWarning("正在尝试带艺术家搜索，结果可能将过少！");
			ncmAlbum = await MapToAsync(album, true);
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
			NcmTrack[] ncmTracks;
			List<NcmTrack> list;

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
			list = new List<NcmTrack>();
			foreach (NcmTrack item in ncmTracks.Where(t => ComputeSimilarity(t.Name, track.Name, false) != 0))
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
				if (_database.IsOldFormat())
					FastConsole.WriteWarning("不兼容的老格式数据库，将被覆盖重建！");
				else {
					SortDatabase();
					FastConsole.WriteInfo($"搜索数据库\"{databasePath}\"加载成功。");
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
			using (FileStream stream = new FileStream(databasePath, FileMode.OpenOrCreate))
			using (StreamWriter writer = new StreamWriter(stream))
				writer.Write(FormatJson(JsonConvert.SerializeObject(_database)));
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
			TSource result;

			if (sources.Length == 0)
				return null;
			FastConsole.WriteInfo("请手动输入1,2,3...选择匹配的项，若不存在，请直接按下回车键。");
			FastConsole.WriteInfo("对比项：" + TrackOrAlbumToString(target));
			for (int i = 0; i < sources.Length; i++) {
				double nameSimilarity;
				string text;

				nameSimilarity = nameSimilarities[sources[i]];
				text = $"{i + 1}. {sources[i]} (s:{nameSimilarity.ToString("F2")})";
				if (nameSimilarity >= 0.85)
					FastConsole.WriteLine(text, ConsoleColor.Green);
				else if (nameSimilarity >= 0.5)
					FastConsole.WriteLine(text, ConsoleColor.Yellow);
				else
					FastConsole.WriteInfo(text);
			}
			result = null;
			do {
				string userInput;
				int index;

				userInput = FastConsole.ReadLine().Trim();
				if (userInput.Length == 0)
					break;
				if (int.TryParse(userInput, out index)) {
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
				string userInput;

				userInput = FastConsole.ReadLine().Trim();
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

		private sealed class AudioInfo {
			public string Path { get; set; }

			public Tag Tag { get; set; }

			public Album Album { get; set; }

			public Track Track { get; set; }
		}
	}
}
