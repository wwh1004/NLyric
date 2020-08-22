using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace NLyric {
	internal static class ChineseConverter {
		private static readonly Dictionary<char, char> _traditionalToSimplifiedMap = GetTraditionalToSimplifiedMap();

		private static Dictionary<char, char> GetTraditionalToSimplifiedMap() {
			var assembly = Assembly.GetExecutingAssembly();
			using (var stream = assembly.GetManifestResourceStream("NLyric.TraditionalToSimplified.map"))
			using (var reader = new BinaryReader(stream)) {
				int count = (int)stream.Length / 4;
				var map = new Dictionary<char, char>(count);
				for (int i = 0; i < count; i++)
					map.Add((char)reader.ReadUInt16(), (char)reader.ReadUInt16());
				return map;
			}
		}

		public static string TraditionalToSimplified(string s) {
			if (s is null)
				return null;

			var sb = new StringBuilder(s);
			for (int i = 0; i < sb.Length; i++) {
				if (_traditionalToSimplifiedMap.TryGetValue(sb[i], out char c))
					sb[i] = c;
			}
			return sb.ToString();
		}
	}
}
