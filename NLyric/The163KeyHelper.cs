using System;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;
using TagLib;

namespace NLyric {
	/// <summary>
	/// 通过163Key直接获取歌曲ID
	/// </summary>
	internal static class The163KeyHelper {
		private static readonly Aes _aes = Create163Aes();

		private static Aes Create163Aes() {
			var aes = Aes.Create();
			aes.BlockSize = 128;
			aes.Key = Encoding.UTF8.GetBytes(@"#14ljk_!\]&0U<'(");
			aes.Mode = CipherMode.ECB;
			aes.Padding = PaddingMode.PKCS7;
			return aes;
		}

		/// <summary>
		/// 尝试获取网易云音乐ID
		/// </summary>
		/// <param name="tag"></param>
		/// <param name="trackId"></param>
		/// <returns></returns>
		public static bool TryGetTrackId(Tag tag, out int trackId) {
			if (tag is null)
				throw new ArgumentNullException(nameof(tag));

			trackId = 0;
			string the163Key = tag.Comment;
			if (!Is163KeyCandidate(the163Key))
				the163Key = tag.Description;
			if (!Is163KeyCandidate(the163Key))
				return false;
			try {
				the163Key = the163Key.Substring(22);
				byte[] byt163Key = Convert.FromBase64String(the163Key);
				using (var cryptoTransform = _aes.CreateDecryptor())
					byt163Key = cryptoTransform.TransformFinalBlock(byt163Key, 0, byt163Key.Length);
				trackId = (int)JObject.Parse(Encoding.UTF8.GetString(byt163Key).Substring(6))["musicId"];
			}
			catch {
				return false;
			}
			return true;
		}

		private static bool Is163KeyCandidate(string s) {
			return !string.IsNullOrEmpty(s) && s.StartsWith("163 key(Don't modify):", StringComparison.Ordinal);
		}
	}
}
