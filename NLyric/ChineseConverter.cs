using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace NLyric {
	internal static class ChineseConverter {
		private static readonly Dictionary<char, char> _traditionalToSimplifiedMap;

		static ChineseConverter() {
			var assembly = Assembly.GetExecutingAssembly();
			using (var stream = assembly.GetManifestResourceStream("NLyric.TraditionalToSimplified.map"))
			using (var reader = new BinaryReader(stream)) {
				int count = (int)stream.Length / 4;
				_traditionalToSimplifiedMap = new Dictionary<char, char>(count);
				for (int i = 0; i < count; i++)
					_traditionalToSimplifiedMap.Add((char)reader.ReadUInt16(), (char)reader.ReadUInt16());
			}
		}

		public static string TraditionalToSimplified(string s) {
			if (s is null)
				return null;

			var sb = new StringBuilder(s);
			for (int i = 0; i < sb.Length; i++)
				if (_traditionalToSimplifiedMap.TryGetValue(sb[i], out char c))
					sb[i] = c;
			return sb.ToString();
		}
	}
}
