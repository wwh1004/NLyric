using System;
using System.Collections.Generic;

namespace NLyric {
	internal sealed class DictionaryComparer<TKey, TValue> : IComparer<TKey> where TValue : IComparable<TValue> {
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
}
