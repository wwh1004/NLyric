using System;

namespace NLyric {
	internal static class Crc32 {
		private static readonly uint[] _table;

		static Crc32() {
			uint seed;

			_table = new uint[256];
			seed = 0xEDB88320;
			for (int i = 0; i < 256; i++) {
				uint crc;

				crc = (uint)i;
				for (int j = 8; j > 0; j--) {
					if ((crc & 1) == 1)
						crc = (crc >> 1) ^ seed;
					else
						crc >>= 1;
				}
				_table[i] = crc;
			}
		}

		public static uint Compute(byte[] data) {
			if (data == null)
				throw new ArgumentNullException(nameof(data));

			uint crc32;

			crc32 = 0xFFFFFFFF;
			for (int i = 0; i < data.Length; i++)
				crc32 = (crc32 >> 8) ^ _table[(crc32 ^ data[i]) & 0xFF];
			return ~crc32;
		}
	}
}
