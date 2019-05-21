using System.Cli;
using System.IO;

namespace NLyric {
	internal sealed class Arguments {
		private string _directory;

		[Argument("-d", IsRequired = true, Type = "DIR", Description = "存放音乐的文件夹，可以是相对路径或者绝对路径")]
		public string Directory {
			get => _directory;
			set {
				if (!System.IO.Directory.Exists(value))
					throw new DirectoryNotFoundException();

				_directory = Path.GetFullPath(value);
			}
		}
	}
}
