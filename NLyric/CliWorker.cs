using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using NLyric.AudioInfo;
using NLyric.Lyrics;
using NLyric.Ncm;

namespace NLyric {
	internal static class CliWorker {
		private static readonly FuzzySettings _fuzzySettings = Settings.Default.Fuzzy;
		private static readonly LyricSettings _lyricSettings = Settings.Default.Lyric;
		private static readonly Dictionary<string, (bool, NcmAlbum)> _cachedNcmAlbums = new Dictionary<string, (bool, NcmAlbum)>();
		private static readonly Dictionary<NcmAlbum, NcmTrack[]> _cachedNcmTrackses = new Dictionary<NcmAlbum, NcmTrack[]>();

		public static async Task ExecuteAsync(CliArguments arguments) {
			foreach (string filePath in Directory.EnumerateFiles(arguments.Directory, "*", SearchOption.AllDirectories)) {
				string extension;
				int? trackId;
				Lrc lrc;

				extension = Path.GetExtension(filePath);
				if (!Settings.Default.Search.AudioExtensions.Any(s => extension.Equals(s, StringComparison.OrdinalIgnoreCase)))
					continue;
				if (!arguments.Overwriting && File.Exists(Path.ChangeExtension(filePath, ".lrc"))) {
					Logger.Instance.LogInfo($"文件\"{Path.GetFileName(filePath)}\"的歌词已存在，若要强制覆盖，请使用命令行参数--overwriting");
					continue;
				}
				Logger.Instance.LogInfo($"开始搜索文件\"{Path.GetFileName(filePath)}\"的歌词");
				trackId = The163KeyHelper.TryGetMusicId(filePath);
				if (trackId != null)
					Logger.Instance.LogInfo($"已通过163Key获取文件\"{Path.GetFileName(filePath)}\"的网易云音乐ID: {trackId}");
				if (trackId == null) {
					ATL.Track atlTrack;
					Album album;
					Track track;
					NcmTrack ncmTrack;

					atlTrack = new ATL.Track(filePath);
					album = Album.HasAlbumInfo(atlTrack) ? new Album(atlTrack, true) : null;
					track = new Track(atlTrack);
					try {
						ncmTrack = await MapToAsync(track, album);
						if (ncmTrack != null) {
							trackId = ncmTrack.Id;
							Logger.Instance.LogInfo($"已通过匹配获取文件\"{Path.GetFileName(filePath)}\"的网易云音乐ID: {trackId}");
						}
					}
					catch (Exception ex) {
						Logger.Instance.LogException(ex);
					}
				}
				if (trackId == null) {
					Logger.Instance.LogWarning($"无法找到文件\"{Path.GetFileName(filePath)}\"的网易云音乐ID");
					Logger.Instance.LogNewLine();
					continue;
				}
				lrc = null;
				try {
					lrc = await GetLrcAsync(trackId.Value);
				}
				catch (Exception ex) {
					Logger.Instance.LogException(ex);
				}
				if (lrc != null)
					File.WriteAllText(Path.ChangeExtension(filePath, ".lrc"), lrc.ToString());
				Logger.Instance.LogNewLine();
			}
		}

		private static async Task<NcmTrack> MapToAsync(Track track, Album album) {
			if (track == null)
				throw new ArgumentNullException(nameof(track));

			NcmAlbum ncmAlbum;
			NcmTrack[] ncmTracks;
			NcmTrack ncmTrack;

			ncmAlbum = album == null ? null : await MapToAsync(album);
			if (ncmAlbum == null)
				// 没有对应的专辑信息，使用无专辑匹配
				return await MapToAsync(track);
			ncmTracks = await GetTracksAsync(ncmAlbum);
			// 获取网易云音乐上专辑收录的歌曲
			ncmTrack = MatchByUser(ncmTracks, track);
			if (ncmTrack == null)
				// 网易云音乐上的专辑可能没收录这个歌曲，不清楚为什么，但是确实存在这个情况，比如专辑id:3094396
				return await MapToAsync(track);
			Logger.Instance.LogInfo("歌曲匹配成功");
			return ncmTrack;
		}

		private static async Task<NcmTrack[]> GetTracksAsync(NcmAlbum ncmAlbum) {
			NcmTrack[] ncmTracks;

			if (!_cachedNcmTrackses.TryGetValue(ncmAlbum, out ncmTracks)) {
				ncmTracks = (await CloudMusic.GetTracksAsync(ncmAlbum.Id)).ToArray();
				_cachedNcmTrackses[ncmAlbum] = ncmTracks;
			}
			return ncmTracks;
		}

		private static async Task<NcmTrack> MapToAsync(Track track) {
			if (track == null)
				throw new ArgumentNullException(nameof(track));

			NcmTrack ncmTrack;

			Logger.Instance.LogInfo($"开始搜索歌曲\"{track}\"");
			ncmTrack = await MapToAsync(track, true);
			if (ncmTrack == null && _fuzzySettings.TryIgnoringArtists) {
				Logger.Instance.LogWarning("正在尝试忽略艺术家搜索，结果可能将不精确");
				ncmTrack = await MapToAsync(track, false);
			}
			if (ncmTrack == null) {
				Logger.Instance.LogInfo("歌曲匹配失败");
				return null;
			}
			Logger.Instance.LogInfo("歌曲匹配成功");
			return ncmTrack;
		}

		private static async Task<NcmTrack> MapToAsync(Track track, bool withArtists) {
			NcmTrack[] ncmTracks;

			ncmTracks = (await CloudMusic.SearchTrackAsync(track, withArtists)).Where(t => ComputeSimilarity(t.Name, track.Name, false) != 0).ToArray();
			return MatchByUser(ncmTracks, track);
		}

		private static async Task<NcmAlbum> MapToAsync(Album album) {
			if (album == null)
				throw new ArgumentNullException(nameof(album));

			string replacedAlbumName;
			bool hasValue;
			NcmAlbum ncmAlbum;

			replacedAlbumName = album.Name.ReplaceEx();
			_cachedNcmAlbums.TryGetValue(replacedAlbumName, out (bool, NcmAlbum) tuple);
			(hasValue, ncmAlbum) = tuple;
			if (hasValue)
				return ncmAlbum;
			Logger.Instance.LogInfo($"开始搜索专辑\"{album}\"");
			ncmAlbum = await MapToAsync(album, true);
			if (ncmAlbum == null && _fuzzySettings.TryIgnoringArtists) {
				Logger.Instance.LogWarning("正在尝试忽略艺术家搜索，结果可能将不精确");
				ncmAlbum = await MapToAsync(album, false);
			}
			if (ncmAlbum == null) {
				Logger.Instance.LogInfo("专辑匹配失败");
				_cachedNcmAlbums[replacedAlbumName] = (true, null);
				return null;
			}
			Logger.Instance.LogInfo("专辑匹配成功");
			_cachedNcmAlbums[replacedAlbumName] = (true, ncmAlbum);
			return ncmAlbum;
		}

		private static async Task<NcmAlbum> MapToAsync(Album album, bool withArtists) {
			NcmAlbum[] ncmAlbums;

			ncmAlbums = (await CloudMusic.SearchAlbumAsync(album, withArtists)).Where(t => ComputeSimilarity(t.Name, album.Name, false) != 0).ToArray();
			return MatchByUser(ncmAlbums, album);
		}

		private static TSource MatchByUser<TSource, TTarget>(TSource[] sources, TTarget target) where TSource : class, ITrackOrAlbum where TTarget : class, ITrackOrAlbum {
			TSource result;

			result = MatchByUser(sources, target, false);
			if (result == null && _fuzzySettings.TryIgnoringExtraInfo)
				result = MatchByUser(sources, target, true);
			return result;
		}

		private static TSource MatchByUser<TSource, TTarget>(TSource[] sources, TTarget target, bool fuzzy) where TSource : class, ITrackOrAlbum where TTarget : class, ITrackOrAlbum {
			Dictionary<TSource, double> nameSimilarities;
			TSource result;
			bool isExact;

			if (!sources.Any())
				return null;
			nameSimilarities = new Dictionary<TSource, double>();
			foreach (TSource source in sources)
				nameSimilarities[source] = ComputeSimilarity(source.Name, target.Name, fuzzy);
			result = Match(sources, target, nameSimilarities, out isExact);
			if (result != null) {
				// 自动匹配成功
				if (isExact)
					// 如果是完全匹配，不需要用户再次确认
					return result;
				if (Confirm())
					return result;
			}
			return fuzzy ? Select(sources.OrderByDescending(t => t, new DictionaryComparer<TSource, double>(nameSimilarities)).ToArray(), target) : null;
			// fuzzy为true时是第二次搜索了，再让用户再次手动从搜索结果中选择，自动匹配失败的原因可能是 Settings.Match.MinimumSimilarity 设置太大了
		}

		private static TSource Match<TSource, TTarget>(IEnumerable<TSource> sources, TTarget target, Dictionary<TSource, double> nameSimilarities, out bool isExact) where TSource : class, ITrackOrAlbum where TTarget : class, ITrackOrAlbum {
			foreach (TSource source in sources) {
				double nameSimilarity;

				nameSimilarity = nameSimilarities[source];
				if (nameSimilarity < Settings.Default.Match.MinimumSimilarity)
					continue;
				foreach (string ncmArtist in source.Artists)
					foreach (string artist in target.Artists)
						if (ComputeSimilarity(ncmArtist, artist, false) >= Settings.Default.Match.MinimumSimilarity) {
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

		private static bool Confirm() {
			Logger.Instance.LogInfo("不完全相似，请手动确认是否使用自动匹配结果");
			Logger.Instance.LogInfo("请手动输入Yes或No");
			do {
				string userInput;

				userInput = Console.ReadLine().Trim().ToUpperInvariant();
				switch (userInput) {
				case "YES":
					return true;
				case "NO":
					return false;
				}
				Logger.Instance.LogInfo("输入有误，请重新输入");
			} while (true);
		}

		private static TSource Select<TSource, TTarget>(TSource[] sources, TTarget target) where TSource : class, ITrackOrAlbum where TTarget : class, ITrackOrAlbum {
			TSource result;

			Logger.Instance.LogInfo("请手动输入1,2,3...选择匹配的项，若不存在，请输入Pass");
			Logger.Instance.LogInfo("对比项：" + TrackOrAlbumToString(target));
			for (int i = 0; i < sources.Length; i++)
				Logger.Instance.LogInfo((i + 1).ToString() + ". " + sources[i].ToString());
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
				Logger.Instance.LogInfo("输入有误，请重新输入");
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

		private static double ComputeSimilarity(string x, string y, bool fuzzy) {
			x = x.ToHalfWidth().ReplaceEx();
			y = y.ToHalfWidth().ReplaceEx();
			if (fuzzy) {
				x = x.Fuzzy();
				y = y.Fuzzy();
			}
			return Levenshtein.Compute(x, y);
		}

		private static async Task<Lrc> GetLrcAsync(int trackId) {
			bool hasLyric;
			Lrc rawLrc;
			Lrc translatedLrc;

			(hasLyric, rawLrc, translatedLrc) = await CloudMusic.GetLyricAsync(trackId);
			if (!hasLyric) {
				Logger.Instance.LogInfo("当前歌曲的歌词未被收录");
				return null;
			}
			if (rawLrc == null && translatedLrc == null) {
				Logger.Instance.LogInfo("当前歌曲是纯音乐无歌词");
				return null;
			}
			foreach (string mode in _lyricSettings.Modes) {
				switch (mode.ToUpperInvariant()) {
				case "MERGED":
					if (rawLrc == null || translatedLrc == null)
						continue;
					Logger.Instance.LogInfo("已获取混合歌词");
					return LrcMerger.Merge(rawLrc, translatedLrc);
				case "RAW":
					if (rawLrc == null)
						continue;
					Logger.Instance.LogInfo("已获取原始歌词");
					return rawLrc;
				case "TRANSLATED":
					if (translatedLrc == null)
						continue;
					Logger.Instance.LogInfo("已获取翻译歌词");
					return translatedLrc;
				default:
					throw new ArgumentOutOfRangeException(nameof(mode));
				}
			}
			return null;
		}

		private sealed class DictionaryComparer<TKey, TValue> : IComparer<TKey> where TValue : IComparable<TValue> {
			private readonly Dictionary<TKey, TValue> _dictionary;

			public DictionaryComparer(Dictionary<TKey, TValue> dictionary) {
				if (dictionary == null)
					throw new ArgumentNullException(nameof(dictionary));

				_dictionary = dictionary;
			}

			public int Compare(TKey x, TKey y) {
				return _dictionary[x].CompareTo(_dictionary[y]);
			}
		}

		private static class The163KeyHelper {
			private static readonly byte[] _163Start = Encoding.UTF8.GetBytes("163 key(Don't modify):");
			private static readonly byte[] _163EndMp3 = { 0x54, 0x41, 0x4C, 0x42 };
			private static readonly byte[] _163EndFlac = { 0x0, 0x0, 0x0, 0x45 };
			private static readonly Aes _aes;

			static The163KeyHelper() {
				_aes = Aes.Create();
				_aes.BlockSize = 128;
				_aes.Key = Encoding.UTF8.GetBytes(@"#14ljk_!\]&0U<'(");
				_aes.Mode = CipherMode.ECB;
				_aes.Padding = PaddingMode.PKCS7;
			}

			public static int? TryGetMusicId(string filePath) {
				string extension;
				byte[] byt163Key;

				extension = Path.GetExtension(filePath);
				switch (extension.ToUpperInvariant()) {
				case ".FLAC":
					byt163Key = Get163Key(filePath, false);
					break;
				case ".MP3":
					byt163Key = Get163Key(filePath, true);
					break;
				default:
					byt163Key = null;
					break;
				}
				if (byt163Key == null)
					return null;
				return GetMusicId(byt163Key);
			}

			public static byte[] Get163Key(string filePath, bool isMp3) {
				byte[] bytFile;
				int startIndex;
				int endIndex;
				byte[] byt163Key;

				bytFile = new byte[0x4000];
				using (FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
					stream.Read(bytFile, 0, bytFile.Length);
				startIndex = GetIndex(bytFile, _163Start, 0);
				if (startIndex == -1)
					return null;
				if (isMp3)
					endIndex = GetIndex(bytFile, _163EndMp3, startIndex);
				else
					endIndex = GetIndex(bytFile, _163EndFlac, startIndex) - 1;
				if (endIndex == -1)
					return null;
				byt163Key = new byte[endIndex - startIndex - _163Start.Length];
				Buffer.BlockCopy(bytFile, startIndex + _163Start.Length, byt163Key, 0, byt163Key.Length);
				return byt163Key;
			}

			public static int GetMusicId(byte[] byt163Key) {
				byt163Key = Convert.FromBase64String(Encoding.UTF8.GetString(byt163Key));
				byt163Key = _aes.CreateDecryptor().TransformFinalBlock(byt163Key, 0, byt163Key.Length);
				return int.Parse(Encoding.UTF8.GetString(byt163Key, 17, GetIndex(byt163Key, 0x2C, 17) - 17));
			}

			private static int GetIndex(byte[] src, byte dest, int startIndex) {
				return GetIndex(src, dest, startIndex, src.Length - 1);
			}

			private static int GetIndex(byte[] src, byte dest, int startIndex, int endIndex) {
				for (int i = startIndex; i < endIndex + 1; i++)
					if (src[i] == dest)
						return i;
				return -1;
			}

			private static int GetIndex(byte[] src, byte[] dest, int startIndex) {
				return GetIndex(src, dest, startIndex, src.Length - dest.Length);
			}

			private static int GetIndex(byte[] src, byte[] dest, int startIndex, int endIndex) {
				int j;

				for (int i = startIndex; i < endIndex + 1; i++)
					if (src[i] == dest[0]) {
						for (j = 1; j < dest.Length; j++)
							if (src[i + j] != dest[j])
								break;
						if (j == dest.Length)
							return i;
					}
				return -1;
			}
		}
	}
}
