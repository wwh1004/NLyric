using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NLyric.AudioInfo;
using NLyric.Ncm;

namespace NLyric {
	internal static class CliWorker {
		private static readonly Dictionary<string, (bool, NcmAlbum)> _cachedNcmAlbums = new Dictionary<string, (bool, NcmAlbum)>();
		private static readonly Dictionary<NcmAlbum, NcmTrack[]> _cachedNcmTrackses = new Dictionary<NcmAlbum, NcmTrack[]>();

		public static async Task ExecuteAsync(CliArguments arguments) {
			foreach (string filePath in Directory.EnumerateFiles(arguments.Directory, "*", SearchOption.AllDirectories)) {
				string extension;
				ATL.Track atlTrack;
				Album album;
				Track track;
				NcmTrack ncmTrack;

				extension = Path.GetExtension(filePath);
				if (!Settings.Default.Search.AudioExtensions.Any(s => extension.Equals(s, StringComparison.OrdinalIgnoreCase)))
					continue;
				atlTrack = new ATL.Track(filePath);
				album = Album.HasAlbumInfo(atlTrack) ? new Album(atlTrack, true) : null;
				track = new Track(atlTrack);
				try {
					ncmTrack = await MapToAsync(track, album);
				}
				catch (Exception ex) {
					Logger.Instance.LogException(ex);
				}
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
			if (ncmTrack == null) {
				Logger.Instance.LogInfo("歌曲匹配失败");
				Logger.Instance.LogNewLine();
				return null;
			}
			Logger.Instance.LogInfo("歌曲匹配成功");
			Logger.Instance.LogNewLine();
			return ncmTrack;
		}

		private static async Task<NcmTrack[]> GetTracksAsync(NcmAlbum ncmAlbum) {
			NcmTrack[] ncmTracks;

			if (!_cachedNcmTrackses.TryGetValue(ncmAlbum, out ncmTracks)) {
				ncmTracks = (await CloudMusic.GetTracksAsync(ncmAlbum)).ToArray();
				_cachedNcmTrackses[ncmAlbum] = ncmTracks;
			}
			return ncmTracks;
		}

		private static async Task<NcmTrack> MapToAsync(Track track) {
			if (track == null)
				throw new ArgumentNullException(nameof(track));

			NcmTrack ncmTrack;

			ncmTrack = await MapToAsync(track, true);
			if (ncmTrack == null)
				ncmTrack = await MapToAsync(track, false);
			if (ncmTrack == null) {
				Logger.Instance.LogInfo("歌曲匹配失败");
				Logger.Instance.LogNewLine();
				return null;
			}
			Logger.Instance.LogInfo("歌曲匹配成功");
			Logger.Instance.LogNewLine();
			return ncmTrack;
		}

		private static async Task<NcmTrack> MapToAsync(Track track, bool withArtists) {
			IEnumerable<NcmTrack> ncmTracks;

			ncmTracks = (await CloudMusic.SearchTrackAsync(track, withArtists)).Where(t => ComputeSimilarity(t.Name, track.Name) != 0);
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
			ncmAlbum = await MapToAsync(album, true);
			if (ncmAlbum == null)
				ncmAlbum = await MapToAsync(album, false);
			if (ncmAlbum == null) {
				Logger.Instance.LogInfo("专辑匹配失败");
				Logger.Instance.LogNewLine();
				_cachedNcmAlbums[replacedAlbumName] = (false, null);
				return null;
			}
			Logger.Instance.LogInfo("专辑匹配成功");
			Logger.Instance.LogNewLine();
			_cachedNcmAlbums[replacedAlbumName] = (true, ncmAlbum);
			return ncmAlbum;
		}

		private static async Task<NcmAlbum> MapToAsync(Album album, bool withArtists) {
			IEnumerable<NcmAlbum> ncmAlbums;

			ncmAlbums = (await CloudMusic.SearchAlbumAsync(album, withArtists)).Where(t => ComputeSimilarity(t.Name, album.Name) != 0);
			return MatchByUser(ncmAlbums, album);
		}

		private static TSource MatchByUser<TSource, TTarget>(IEnumerable<TSource> sources, TTarget target) where TSource : class, ITrackOrAlbum where TTarget : class, ITrackOrAlbum {
			TSource result;
			bool isExact;

			if (!sources.Any())
				return null;
			result = Match(sources, target, out isExact);
			if (result != null) {
				// 自动匹配成功
				if (isExact)
					// 如果是完全匹配，不需要用户再次确认
					return result;
				if (Confirm())
					return result;
			}
			return Select(sources.OrderByDescending(t => t, new TrackOrAlbumComparer(target)).ToArray(), target);
			// 自动匹配失败，让用户自己选择搜索结果，自动匹配失败的原因可能是 Settings.Match.MinimumSimilarity 设置太大了
		}

		private static TSource Match<TSource, TTarget>(IEnumerable<TSource> sources, TTarget target, out bool isExact) where TSource : class, ITrackOrAlbum where TTarget : class, ITrackOrAlbum {
			foreach (TSource source in sources) {
				if (ComputeSimilarity(source.Name, target.Name) < Settings.Default.Match.MinimumSimilarity)
					continue;
				foreach (string ncmArtist in source.Artists)
					foreach (string artist in target.Artists) {
						double similarity;

						similarity = ComputeSimilarity(ncmArtist, artist);
						if (similarity >= Settings.Default.Match.MinimumSimilarity) {
							Logger.Instance.LogInfo(
								"自动匹配结果：" + Environment.NewLine +
								"网易云音乐：" + source.ToString() + Environment.NewLine +
								"本地：" + target.ToString() + Environment.NewLine +
								"相似度：" + similarity.ToString() + Environment.NewLine);
							Logger.Instance.LogNewLine();
							isExact = similarity == 1;
							return source;
						}
					}
			}
			isExact = false;
			return null;
		}

		private static bool Confirm() {
			Logger.Instance.LogInfo("不完全相似，请手动确认是否使用自动匹配结果");
			Logger.Instance.LogInfo("请手动输入Yes或No");
			try {
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
			finally {
				Logger.Instance.LogNewLine();
			}
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
			Logger.Instance.LogNewLine();
			return result;

			string TrackOrAlbumToString(ITrackOrAlbum trackOrAlbum) {
				if (trackOrAlbum.Artists.Length == 0)
					return trackOrAlbum.Name;
				return trackOrAlbum.Name + " by " + string.Join(",", trackOrAlbum.Artists);
			}
		}

		private static double ComputeSimilarity(string x, string y) {
			x = x.ReplaceEx();
			y = y.ReplaceEx();
			return Levenshtein.Compute(x, y);
		}

		private sealed class TrackOrAlbumComparer : IComparer<ITrackOrAlbum> {
			private readonly ITrackOrAlbum _contrast;
			private readonly Dictionary<ITrackOrAlbum, double> _similarities;

			public TrackOrAlbumComparer(ITrackOrAlbum contrast) {
				if (contrast == null)
					throw new ArgumentNullException(nameof(contrast));

				_contrast = contrast;
				_similarities = new Dictionary<ITrackOrAlbum, double>();
			}

			public int Compare(ITrackOrAlbum x, ITrackOrAlbum y) {
				return GetSimilarity(x).CompareTo(GetSimilarity(y));
			}

			private double GetSimilarity(ITrackOrAlbum trackOrAlbum) {
				double similarity;

				if (!_similarities.TryGetValue(trackOrAlbum, out similarity)) {
					similarity = ComputeSimilarity(trackOrAlbum.Name, _contrast.Name);
					_similarities[trackOrAlbum] = similarity;
				}
				return similarity;
			}
		}
	}
}
