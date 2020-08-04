using System;

namespace NLyric {
	internal static class CRC32_ {
		private static readonly uint[] _table = GenerateTable(0xEDB88320);

		public static uint Compute(byte[] data) {
			if (data is null)
				throw new ArgumentNullException(nameof(data));

			uint crc32 = 0xFFFFFFFF;
			for (int i = 0; i < data.Length; i++)
				crc32 = (crc32 >> 8) ^ _table[(crc32 ^ data[i]) & 0xFF];
			return ~crc32;
		}

		private static uint[] GenerateTable(uint seed) {
			uint[] table = new uint[256];
			for (int i = 0; i < 256; i++) {
				uint crc = (uint)i;
				for (int j = 8; j > 0; j--) {
					if ((crc & 1) == 1)
						crc = (crc >> 1) ^ seed;
					else
						crc >>= 1;
				}
				table[i] = crc;
			}
			return table;
		}
	}
}
