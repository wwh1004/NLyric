namespace NLyric.AudioInfo {
	internal interface ITrackOrAlbum {
		string Name { get; }

		string[] Artists { get; }
	}
}
