using System.Cli;
using System.IO;

namespace NLyric {
	internal sealed class CliArguments {
		private string _directory;
		private bool _overwriting;

		[CliArgument("-d", IsRequired = true)]
		internal string CliDirectory {
			set {
				if (!System.IO.Directory.Exists(value))
					throw new DirectoryNotFoundException();
				_directory = value;
			}
		}

		[CliArgument("--overwriting")]
		internal bool CliOverwriting {
			set => _overwriting = value;
		}

		public string Directory => _directory;

		public bool Overwriting => _overwriting;
	}
}
