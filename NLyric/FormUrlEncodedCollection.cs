using System.Collections.Generic;

namespace NLyric {
	internal sealed class FormUrlEncodedCollection : List<KeyValuePair<string, string>> {
		public void Add(string key, string value) {
			Add(new KeyValuePair<string, string>(key, value));
		}
	}
}
