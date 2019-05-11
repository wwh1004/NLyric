using System;

namespace NLyric {
	internal static class Levenshtein {
		/// <summary>
		/// 计算相似度
		/// </summary>
		/// <param name="x"></param>
		/// <param name="y"></param>
		/// <returns></returns>
		public static double Compute(string x, string y) {
			int[,] matrix;
			int cost;

			matrix = new int[x.Length + 1, y.Length + 1];
			for (int i = 0; i <= x.Length; i++)
				matrix[i, 0] = i;
			for (int i = 0; i <= y.Length; i++)
				matrix[0, i] = i;
			for (int i = 1; i <= x.Length; i++)
				for (int j = 1; j <= y.Length; j++) {
					if (x[i - 1] == y[j - 1])
						cost = 0;
					else
						cost = 1;
					matrix[i, j] = Math.Min(Math.Min(matrix[i - 1, j - 1] + cost, matrix[i, j - 1] + 1), matrix[i - 1, j] + 1);
				}
			return 1 - ((double)matrix[x.Length, y.Length] / Math.Max(x.Length, y.Length));
		}

		/// <summary>
		/// 计算相似度
		/// </summary>
		/// <param name="x"></param>
		/// <param name="y"></param>
		/// <returns></returns>
		public static double Compute<T>(T[] x, T[] y, Comparison<T> comparison) {
			int[,] matrix;
			int cost;

			matrix = new int[x.Length + 1, y.Length + 1];
			for (int i = 0; i <= x.Length; i++)
				matrix[i, 0] = i;
			for (int i = 0; i <= y.Length; i++)
				matrix[0, i] = i;
			for (int i = 1; i <= x.Length; i++)
				for (int j = 1; j <= y.Length; j++) {
					if (comparison(x[i - 1], y[j - 1]) == 0)
						cost = 0;
					else
						cost = 1;
					matrix[i, j] = Math.Min(Math.Min(matrix[i - 1, j - 1] + cost, matrix[i, j - 1] + 1), matrix[i - 1, j] + 1);
				}
			return 1 - ((double)matrix[x.Length, y.Length] / Math.Max(x.Length, y.Length));
		}
	}
}
