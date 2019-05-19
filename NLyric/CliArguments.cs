using System.Cli;
using System.IO;

namespace NLyric {
	internal sealed class CliArguments {
		private string _directory;

		[CliArgument("-d", IsRequired = true)]
		internal string CliDirectory {
			set {
				if (!System.IO.Directory.Exists(value))
					throw new DirectoryNotFoundException();

				_directory = Path.GetFullPath(value);
			}
		}

		public string Directory => _directory;
	}
}
