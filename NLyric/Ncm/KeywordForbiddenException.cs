using System;
using System.Runtime.Serialization;

namespace NLyric.Ncm {
	/// <summary>
	/// 关键词被禁止
	/// </summary>
	[Serializable]
	public sealed class KeywordForbiddenException : Exception {
		public KeywordForbiddenException() {
		}

		public KeywordForbiddenException(string text) : base($"\"{text}\" 中有关键词被屏蔽") {
		}

		private KeywordForbiddenException(SerializationInfo info, StreamingContext context) : base(info, context) {
		}
	}
}
