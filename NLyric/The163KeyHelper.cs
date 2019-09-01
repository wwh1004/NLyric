using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;

namespace NLyric {
	/// <summary>
	/// 通过163Key直接获取歌曲ID
	/// </summary>
	internal static class The163KeyHelper {
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

		public static bool TryGetMusicId(string filePath, out int trackId) {
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
			if (byt163Key is null) {
				trackId = 0;
				return false;
			}
			trackId = GetMusicId(byt163Key);
			return true;
		}

		private static byte[] Get163Key(string filePath, bool isMp3) {
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

		private static int GetMusicId(byte[] byt163Key) {
			byt163Key = Convert.FromBase64String(Encoding.UTF8.GetString(byt163Key));
			using (ICryptoTransform cryptoTransform = _aes.CreateDecryptor())
				byt163Key = cryptoTransform.TransformFinalBlock(byt163Key, 0, byt163Key.Length);
			return (int)JObject.Parse(Encoding.UTF8.GetString(byt163Key).Substring(6))["musicId"];
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
